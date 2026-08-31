using System.Security.Cryptography;
using System.Text;
using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Clinic.Api.Services.Sms;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Services;

public interface IBookingService
{
    Task<AppointmentDto> BookAsync(BookAppointmentRequest request, AppointmentSource source, CancellationToken ct = default);
    Task RequestCancelOtpAsync(Guid appointmentId, string phone, CancellationToken ct = default);
    Task<AppointmentDto> ConfirmCancelByPatientAsync(Guid appointmentId, string phone, string code, CancellationToken ct = default);
    Task<AppointmentDto> AdminCancelAsync(Guid appointmentId, string? reason, CancellationToken ct = default);
    Task<AppointmentDto> RescheduleAsync(Guid appointmentId, RescheduleRequest request, CancellationToken ct = default);
}

/// <summary>
/// All booking mutations. The database exclusion constraint is the final
/// arbiter against double-booking; the explicit conflict checks below exist to
/// return friendly 409s before hitting it.
/// </summary>
public class BookingService(
    AppDbContext db,
    IAvailabilityService availability,
    ISmsService sms) : IBookingService
{
    private const int MaxOtpAttempts = 5;          // plan.md §8
    private const int OtpResendCooldownSeconds = 60;
    private const int MaxOtpsPerAppointmentPerDay = 10;
    private const int MaxActiveBookingsPerPhone = 3;

    public async Task<AppointmentDto> BookAsync(BookAppointmentRequest request, AppointmentSource source, CancellationToken ct = default)
    {
        var phone = PhoneNormalizer.Normalize(request.PatientPhone);
        if (phone.Length < 7 || phone.Length > 16)
            throw new ValidationException("A valid patient phone number is required.");

        // Anonymous bookings are the only unauthenticated write in the system;
        // without a cap one phone number could drain every slot for two weeks.
        if (source == AppointmentSource.Online)
        {
            var activeForPhone = await db.Appointments.CountAsync(a =>
                a.PatientPhone == phone
                && a.Status == AppointmentStatus.Booked
                && a.StartsAt >= DateTime.UtcNow, ct);
            if (activeForPhone >= MaxActiveBookingsPerPhone)
                throw new ValidationException(
                    $"This phone number already has {MaxActiveBookingsPerPhone} active bookings. " +
                    "Please cancel one before booking again.");
        }

        // Read-only load — nothing on the doctor is mutated during booking.
        var doctor = await db.Doctors.AsNoTracking()
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == request.DoctorId && d.IsActive, ct)
            ?? throw new NotFoundException("Doctor not found or inactive.");

        if (string.IsNullOrWhiteSpace(request.StartJalali)
            || !request.StartJalali!.Trim().Contains(' ')
            || !JalaliDate.TryParse(request.StartJalali, out var startTehran))
        {
            throw new ValidationException("startJalali must look like '1405/06/04 14:30'.");
        }

        var startsAtUtc = JalaliDate.UtcFromTehran(startTehran);
        var endsAtUtc = startsAtUtc.AddMinutes(doctor.DefaultVisitMinutes);

        ValidateTiming(startsAtUtc);

        if (!await availability.IsInsideWorkingHoursAsync(doctor, startsAtUtc, endsAtUtc, ct))
            throw new ValidationException("The requested time is outside this doctor's working hours.");

        await EnsureNoConflictAsync(doctor.Id, startsAtUtc, endsAtUtc, excludeAppointmentId: null, ct);

        // Validate NationalCode and InsuranceType (also validated by DataAnnotations, but double-check for direct calls)
        var nationalCode = request.NationalCode?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(nationalCode, @"^\d{10}$"))
            throw new ValidationException("کد ملی باید ۱۰ رقم باشد.");

        var appointment = new Appointment
        {
            ShortCode = Codes.NewShortCode(),
            DoctorId = doctor.Id,
            PatientName = request.PatientName.Trim(),
            PatientPhone = phone,
            NationalCode = nationalCode,
            InsuranceType = request.InsuranceType,
            StartsAt = startsAtUtc,
            EndsAt = endsAtUtc,
            Status = AppointmentStatus.Booked,
            Source = source,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct); // DB exclusion constraint rejects true races

        sms.Queue(SmsType.BookingConfirmation, appointment.Id, phone,
            SmsTemplates.BookingConfirmation(
                appointment.PatientName,
                doctor.FullName,
                JalaliDate.ToJalaliDateTime(startsAtUtc),
                appointment.ShortCode,
                doctor.Location));

        return appointment.ToDto(doctor.FullName);
    }

    public async Task RequestCancelOtpAsync(Guid appointmentId, string phone, CancellationToken ct = default)
    {
        var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Booked)
            throw new ValidationException("This appointment is not active.");

        if (PhoneNormalizer.Normalize(phone) != appointment.PatientPhone)
            throw new UnauthorizedException("This phone number does not match the booking.");

        // Throttle OTP issuance: a resend cooldown stops SMS-bombing the
        // patient's phone; the daily cap bounds the cost even across cooldowns.
        var latestOtp = await db.PhoneOtps
            .Where(o => o.AppointmentId == appointmentId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (latestOtp is not null && latestOtp.CreatedAt > DateTime.UtcNow.AddSeconds(-OtpResendCooldownSeconds))
            throw new ValidationException("Please wait a minute before requesting another code.");

        var dayAgoUtc = DateTime.UtcNow.AddDays(-1);
        var issuedLastDay = await db.PhoneOtps
            .CountAsync(o => o.AppointmentId == appointmentId && o.CreatedAt >= dayAgoUtc, ct);
        if (issuedLastDay >= MaxOtpsPerAppointmentPerDay)
            throw new ValidationException("Too many verification codes requested for this appointment. Please contact the clinic.");

        // Invalidate previous unused codes for this appointment.
        var staleCodes = await db.PhoneOtps.Where(o => o.AppointmentId == appointmentId && !o.Used).ToListAsync(ct);
        foreach (var stale in staleCodes) stale.Used = true;

        var code = Codes.NewSixDigitCode();
        db.PhoneOtps.Add(new PhoneOtp
        {
            AppointmentId = appointmentId,
            Phone = appointment.PatientPhone,
            CodeHash = Sha256Hex(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
        });
        await db.SaveChangesAsync(ct);

        sms.Queue(SmsType.CancelOtp, appointment.Id, appointment.PatientPhone, SmsTemplates.CancelOtp(code));
    }

    public async Task<AppointmentDto> ConfirmCancelByPatientAsync(Guid appointmentId, string phone, string code, CancellationToken ct = default)
    {
        var appointment = await db.Appointments
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Booked)
            throw new ValidationException("This appointment is not active.");

        // The OTP alone must never be enough: require the caller to also know
        // the booking's phone number (plan.md §8 — cancel needs code + phone).
        var normalizedPhone = PhoneNormalizer.Normalize(phone ?? "");
        if (normalizedPhone.Length < 7 || PhoneNormalizer.Normalize(appointment.PatientPhone) != normalizedPhone)
            throw new UnauthorizedException("This phone number does not match the booking.");

        var otpId = await db.PhoneOtps
            .Where(o => o.AppointmentId == appointmentId && !o.Used)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(ct);

        if (otpId == Guid.Empty)
            throw new UnauthorizedException("No verification code was requested for this appointment.");

        // Consume one attempt ATOMICALLY: the conditional UPDATE serializes
        // concurrent confirm calls on the database side. Reading Attempts into
        // memory and saving it back would let N parallel requests all pass the
        // "attempts < max" check first, multiplying the brute-force budget.
        // plan.md §8 caps a code at 5 verification attempts — enforced here.
        var claimed = await db.PhoneOtps
            .Where(o => o.Id == otpId && !o.Used && o.Attempts < MaxOtpAttempts && o.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.Attempts, o => o.Attempts + 1), ct);

        if (claimed == 0)
        {
            // Tell the patient WHY: expired codes are re-requestable, exhausted
            // ones mean someone (hopefully them) mistyped five times.
            var state = await db.PhoneOtps.AsNoTracking()
                .Where(o => o.Id == otpId)
                .Select(o => new { o.ExpiresAt })
                .FirstAsync(ct);
            if (state.ExpiresAt <= DateTime.UtcNow)
                throw new UnauthorizedException("The verification code has expired. Please request a new one.");
            throw new UnauthorizedException("Too many incorrect codes. Please request a new one.");
        }

        var storedHash = await db.PhoneOtps.AsNoTracking()
            .Where(o => o.Id == otpId)
            .Select(o => o.CodeHash)
            .FirstAsync(ct);
        if (!ConstantTimeEquals(Sha256Hex(code?.Trim() ?? ""), storedHash))
            throw new UnauthorizedException("Incorrect verification code.");

        await db.PhoneOtps
            .Where(o => o.Id == otpId)
            .ExecuteUpdateAsync(u => u.SetProperty(o => o.Used, true), ct);

        appointment.Status = AppointmentStatus.CancelledByPatient;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancelReason = "Cancelled by patient";
        await db.SaveChangesAsync(ct);

        return appointment.ToDto(appointment.Doctor?.FullName ?? "");
    }

    public async Task<AppointmentDto> AdminCancelAsync(Guid appointmentId, string? reason, CancellationToken ct = default)
    {
        var appointment = await db.Appointments
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Booked)
            throw new ValidationException("Only booked appointments can be cancelled.");

        appointment.Status = AppointmentStatus.CancelledByClinic;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancelReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by clinic" : reason.Trim();
        await db.SaveChangesAsync(ct);

        sms.Queue(SmsType.CancellationNotice, appointment.Id, appointment.PatientPhone,
            SmsTemplates.CancellationNotice(
                appointment.PatientName,
                appointment.Doctor?.FullName ?? "",
                JalaliDate.ToJalaliDateTime(appointment.StartsAt),
                appointment.CancelReason));

        return appointment.ToDto(appointment.Doctor?.FullName ?? "");
    }

    public async Task<AppointmentDto> RescheduleAsync(Guid appointmentId, RescheduleRequest request, CancellationToken ct = default)
    {
        var appointment = await db.Appointments
            .Include(a => a.Doctor)
            .ThenInclude(d => d!.Schedules)
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Booked || appointment.Doctor is null)
            throw new ValidationException("Only booked appointments can be rescheduled.");

        if (string.IsNullOrWhiteSpace(request.NewStartJalali)
            || !request.NewStartJalali!.Trim().Contains(' ')
            || !JalaliDate.TryParse(request.NewStartJalali, out var startTehran))
        {
            throw new ValidationException("newStartJalali must look like '1405/06/05 10:30'.");
        }

        var startsAtUtc = JalaliDate.UtcFromTehran(startTehran);
        var endsAtUtc = startsAtUtc.AddMinutes(appointment.Doctor.DefaultVisitMinutes);

        ValidateTiming(startsAtUtc);

        if (!await availability.IsInsideWorkingHoursAsync(appointment.Doctor, startsAtUtc, endsAtUtc, ct))
            throw new ValidationException("The requested time is outside this doctor's working hours.");

        await EnsureNoConflictAsync(appointment.DoctorId, startsAtUtc, endsAtUtc, appointmentId, ct);

        appointment.StartsAt = startsAtUtc;
        appointment.EndsAt = endsAtUtc;
        await db.SaveChangesAsync(ct);

        return appointment.ToDto(appointment.Doctor.FullName);
    }

    private void ValidateTiming(DateTime startsAtUtc)
    {
        var nowTehran = JalaliDate.NowTehran;
        var nowUtc = JalaliDate.UtcFromTehran(nowTehran);
        if (startsAtUtc < nowUtc.AddMinutes(-1))
            throw new ValidationException("Cannot book a time in the past.");

        var today = DateOnly.FromDateTime(nowTehran.Date);
        var maxDate = today.AddDays(availability.WindowDays - 1);
        var requestedDate = DateOnly.FromDateTime(JalaliDate.UtcToTehran(startsAtUtc).Date);
        if (requestedDate > maxDate)
            throw new ValidationException($"Bookings are only allowed within the next {availability.WindowDays} days.");
    }

    private async Task EnsureNoConflictAsync(Guid doctorId, DateTime startsAtUtc, DateTime endsAtUtc, Guid? excludeAppointmentId, CancellationToken ct)
    {
        var clash = await db.Appointments.AsNoTracking().AnyAsync(a =>
            a.DoctorId == doctorId
            && a.Status == AppointmentStatus.Booked
            && a.StartsAt < endsAtUtc
            && a.EndsAt > startsAtUtc
            && (excludeAppointmentId == null || a.Id != excludeAppointmentId), ct);

        if (clash)
            throw new ConflictException("That time has already been booked. Please choose another slot.");
    }

    internal static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool ConstantTimeEquals(string leftHex, string rightHex)
    {
        var left = Convert.FromHexString(leftHex);
        var right = Convert.FromHexString(rightHex);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}