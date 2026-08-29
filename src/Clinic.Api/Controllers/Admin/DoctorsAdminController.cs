using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/doctors")]
public class DoctorsAdminController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DoctorAdminDto>>> GetAll(CancellationToken ct)
    {
        var doctors = await db.Doctors.AsNoTracking()
            .Include(d => d.Schedules)
            .OrderBy(d => d.FullName)
            .ToListAsync(ct);
        return doctors.Select(d => d.ToAdminDto()).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<DoctorAdminDto>> Create(UpsertDoctorRequest request, CancellationToken ct)
    {
        var doctor = new Doctor
        {
            FullName = request.FullName.Trim(),
            Specialty = request.Specialty?.Trim() ?? "",
            Location = request.Location?.Trim() ?? "",
            DefaultVisitMinutes = request.DefaultVisitMinutes,
            IsActive = request.IsActive,
            Schedules = ParseSchedules(request.Schedules),
        };

        if (await db.Doctors.AnyAsync(d => d.FullName.ToLower() == doctor.FullName.ToLower(), ct))
            throw new ConflictException("A doctor with this exact name already exists (Excel import matches by name).");

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, doctor.ToAdminDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DoctorAdminDto>> Update(Guid id, UpsertDoctorRequest request, CancellationToken ct)
    {
        var doctor = await db.Doctors.Include(d => d.Schedules).FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Doctor not found.");

        doctor.FullName = request.FullName.Trim();
        doctor.Specialty = request.Specialty?.Trim() ?? "";
        doctor.Location = request.Location?.Trim() ?? "";
        doctor.DefaultVisitMinutes = request.DefaultVisitMinutes;
        doctor.IsActive = request.IsActive;

        // Replace the weekly schedule wholesale — the panel edits it as one unit.
        // Must use AddRange explicitly so EF tracks new entities as Added (INSERT),
        // not Modified (UPDATE). Assigning to the navigation property alone causes
        // DbUpdateConcurrencyException because EF generates UPDATE for non-zero GUIDs.
        db.DoctorSchedules.RemoveRange(doctor.Schedules);
        var newSchedules = ParseSchedules(request.Schedules);
        foreach (var s in newSchedules) s.DoctorId = doctor.Id;
        db.DoctorSchedules.AddRange(newSchedules);
        doctor.Schedules = newSchedules;

        await db.SaveChangesAsync(ct);
        return doctor.ToAdminDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doctor = await db.Doctors.Include(d => d.Schedules).FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException("Doctor not found.");
        db.DoctorSchedules.RemoveRange(doctor.Schedules);
        db.Doctors.Remove(doctor);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static List<DoctorSchedule> ParseSchedules(List<ScheduleInputDto>? inputs)
    {
        var schedules = new List<DoctorSchedule>();
        if (inputs is null) return schedules;

        foreach (var input in inputs)
        {
            if (!TimeOnly.TryParseExact(input.StartTime, "HH:mm", out var start)
                || !TimeOnly.TryParseExact(input.EndTime, "HH:mm", out var end))
                throw new ValidationException(
                    $"Invalid schedule time '{input.StartTime}-{input.EndTime}' — use HH:mm.");

            if (end <= start)
                throw new ValidationException(
                    $"Schedule window '{input.StartTime}-{input.EndTime}' ends before it starts.");

            schedules.Add(new DoctorSchedule
            {
                Weekday = input.Weekday,
                StartTime = start,
                EndTime = end,
            });
        }

        return schedules;
    }
}
