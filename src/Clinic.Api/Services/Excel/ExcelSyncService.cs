using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Services.Excel;

public interface IExcelSyncService
{
    byte[] TemplateBytes();
    Task<ImportResultDto> ImportAsync(Stream xlsx, string fileName, CancellationToken ct = default);
    Task<byte[]> ExportAsync(CancellationToken ct = default);
    Task<byte[]> ExportFilteredAsync(Guid? doctorId, string? status, CancellationToken ct = default);
}

/// <summary>
/// Two-way Excel sync: import upserts rows into the database (never blindly
/// overwriting online bookings), export regenerates the sheet from the
/// database. Every import is audited in excel_sync_logs.
/// </summary>
public class ExcelSyncService(AppDbContext db) : IExcelSyncService
{
    public byte[] TemplateBytes() => ExcelWorkbook.BuildTemplate();

    public async Task<ImportResultDto> ImportAsync(Stream xlsx, string fileName, CancellationToken ct = default)
    {
        var outcome = ExcelWorkbook.Parse(xlsx);

        // The audit column is varchar(260): a longer client filename would fail
        // the log insert after the data had already committed.
        var safeFileName = Path.GetFileName(fileName ?? string.Empty);
        if (safeFileName.Length > 260)
            safeFileName = safeFileName[..260];

        // Inactive doctors stay out of the sync: rows naming them are reported
        // as unknown instead of resurrecting deactivated doctors' schedules.
        var doctors = await db.Doctors.AsNoTracking()
            .Where(d => d.IsActive)
            .ToListAsync(ct);
        var doctorByName = doctors
            .GroupBy(d => NameKey(d.FullName))
            .ToDictionary(g => g.Key, g => g.First());

        int imported = 0, updated = 0, skipped = 0;
        var problems = new List<RowProblem>(outcome.Problems);

        // Pass 1 — resolve each row's doctor and UTC times up front, so every
        // appointment lookup below can be served from ONE batched read instead
        // of two queries per row.
        var planned = new List<(ParsedRow Row, Doctor Doctor, DateTime StartsAtUtc, DateTime EndsAtUtc)>(outcome.Rows.Count);
        foreach (var row in outcome.Rows)
        {
            if (!doctorByName.TryGetValue(NameKey(row.DoctorName), out var doctor))
            {
                skipped++;
                problems.Add(new RowProblem(row.RowNumber,
                    $"Doctor '{row.DoctorName}' was not found — create this doctor in the admin panel first."));
                continue;
            }

            var duration = Math.Max(row.DurationMinutes ?? doctor.DefaultVisitMinutes, 5);
            var startsAtUtc = JalaliDate.UtcFromTehran(row.Date.ToDateTime(row.Start));
            planned.Add((row, doctor, startsAtUtc, startsAtUtc.AddMinutes(duration)));
        }

        // Pass 2 — single read covering every appointment of the involved
        // doctors that falls anywhere inside the file's overall time span.
        // Tracked on purpose: matched rows may be updated below.
        var exactByKey = new Dictionary<(Guid DoctorId, DateTime StartsAt), Appointment>();
        var bookedByDoctor = new Dictionary<Guid, List<Appointment>>();
        if (planned.Count > 0)
        {
            var doctorIds = planned.Select(p => p.Doctor.Id).Distinct().ToList();
            var spanStart = planned.Min(p => p.StartsAtUtc);
            var spanEnd = planned.Max(p => p.EndsAtUtc);

            var candidates = await db.Appointments
                .Where(a => doctorIds.Contains(a.DoctorId) && a.StartsAt < spanEnd && a.EndsAt > spanStart)
                .ToListAsync(ct);

            foreach (var group in candidates.GroupBy(a => (a.DoctorId, a.StartsAt)))
            {
                // A start time can hold several rows over time (a cancelled booking
                // plus a fresh one); prefer the active one for conflict detection.
                exactByKey[group.Key] =
                    group.FirstOrDefault(a => a.Status == AppointmentStatus.Booked) ?? group.First();
            }

            foreach (var group in candidates
                         .Where(a => a.Status == AppointmentStatus.Booked)
                         .GroupBy(a => a.DoctorId))
            {
                bookedByDoctor[group.Key] = group.ToList();
            }
        }

        // Slots claimed by this very import — lets an internally overlapping file
        // report row problems instead of failing on the exclusion constraint at save.
        var addedByDoctor = new Dictionary<Guid, List<(DateTime StartsAtUtc, DateTime EndsAtUtc)>>();

        foreach (var (row, doctor, startsAtUtc, endsAtUtc) in planned)
        {
            if (exactByKey.TryGetValue((doctor.Id, startsAtUtc), out var existing))
            {
                // Never silently overwrite a live booking from another channel:
                // a stale workbook must not reassign panel-created bookings any
                // more than online ones.
                var samePatient =
                    PhoneNormalizer.Normalize(existing.PatientPhone) == PhoneNormalizer.Normalize(row.Phone);
                if (existing.Status == AppointmentStatus.Booked
                    && existing.Source != AppointmentSource.ExcelImport
                    && !samePatient)
                {
                    skipped++;
                    problems.Add(new RowProblem(row.RowNumber,
                        $"Slot {row.Date:yyyy/MM/dd} {row.Start:HH:mm} conflicts with an existing booking — row skipped."));
                    continue;
                }

                existing.PatientName = row.PatientName;
                existing.PatientPhone = PhoneNormalizer.Normalize(row.Phone);
                existing.NationalCode = row.NationalCode;
                existing.InsuranceType = row.InsuranceType;
                existing.Status = row.Status;
                existing.Notes = row.Notes;
                updated++;
                continue;
            }

            bookedByDoctor.TryGetValue(doctor.Id, out var bookedList);
            var clash = bookedList?.Any(b => b.StartsAt < endsAtUtc && b.EndsAt > startsAtUtc) == true;

            if (!clash && addedByDoctor.TryGetValue(doctor.Id, out var addedList))
                clash = addedList.Any(b => b.StartsAtUtc < endsAtUtc && b.EndsAtUtc > startsAtUtc);

            if (clash)
            {
                skipped++;
                problems.Add(new RowProblem(row.RowNumber,
                    $"Slot {row.Date:yyyy/MM/dd} {row.Start:HH:mm} overlaps an existing appointment — row skipped."));
                continue;
            }

            db.Appointments.Add(new Appointment
            {
                ShortCode = Codes.NewShortCode(),
                DoctorId = doctor.Id,
                PatientName = row.PatientName,
                PatientPhone = PhoneNormalizer.Normalize(row.Phone),
                NationalCode = row.NationalCode,
                InsuranceType = row.InsuranceType,
                StartsAt = startsAtUtc,
                EndsAt = endsAtUtc,
                Status = row.Status,
                Source = AppointmentSource.ExcelImport,
                Notes = row.Notes,
            });
            imported++;

            if (!addedByDoctor.TryGetValue(doctor.Id, out var added))
            {
                added = [];
                addedByDoctor[doctor.Id] = added;
            }
            added.Add((startsAtUtc, endsAtUtc));
        }

        var log = new ExcelSyncLog
        {
            FileName = safeFileName,
            TotalRows = outcome.Rows.Count + outcome.Problems.Count,
            Imported = imported,
            Updated = updated,
            Skipped = skipped,
            RowErrorsJson = System.Text.Json.JsonSerializer.Serialize(
                problems.Select(p => new ImportRowErrorDto(p.RowNumber, p.Message))),
        };
        db.ExcelSyncLogs.Add(log);
        await db.SaveChangesAsync(ct); // appointments + audit log saved together

        return new ImportResultDto(log.TotalRows, imported, updated, skipped,
            problems.Select(p => new ImportRowErrorDto(p.RowNumber, p.Message)).ToList(), log.Id);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct = default) => await ExportFilteredAsync(null, null, ct);

    public async Task<byte[]> ExportFilteredAsync(Guid? doctorId, string? status, CancellationToken ct = default)
    {
        var doctors = await db.Doctors.AsNoTracking().ToDictionaryAsync(d => d.Id, ct);
        var query = db.Appointments.AsNoTracking().AsQueryable();
        if (doctorId.HasValue) query = query.Where(a => a.DoctorId == doctorId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppointmentStatus>(status, true, out var st) && Enum.IsDefined(st))
            query = query.Where(a => a.Status == st);
        var appointments = await query.OrderBy(a => a.StartsAt).ToListAsync(ct);

        var rows = appointments.Select(a =>
        {
            doctors.TryGetValue(a.DoctorId, out var doctor);
            var duration = (int)(a.EndsAt - a.StartsAt).TotalMinutes;
            return new ExportRow(
                doctor?.FullName ?? "(unknown doctor)",
                doctor?.Specialty ?? "",
                a.StartsAt,
                duration,
                a.PatientName,
                a.PatientPhone,
                a.NationalCode ?? "",
                a.InsuranceType,
                a.Status,
                a.Notes ?? a.CancelReason);
        });

        return ExcelWorkbook.Export(rows);
    }

    private static string NameKey(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
