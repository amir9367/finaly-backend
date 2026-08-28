using System.ComponentModel.DataAnnotations;
using Clinic.Api.Domain;

namespace Clinic.Api.Dtos;

// ---------- Public ----------

public record DoctorDto(Guid Id, string FullName, string Specialty, string Location, int DefaultVisitMinutes);

public record IntervalDto(string From, string To);

public record DayAvailabilityDto(
    string DateJalali,
    string DateIso,
    string WeekdayFa,
    string WeekdayEn,
    List<IntervalDto> WorkingHours,
    List<IntervalDto> Busy);

public record BookAppointmentRequest(
    [Required] Guid DoctorId,
    // Letters/marks (incl. Persian) + a few separators only — no digits, URLs
    // or control text: this name is echoed into SMS sent from the clinic's
    // sender line, so free-form text would enable smishing. ‌ = ZWNJ.
    [Required, MaxLength(120),
     RegularExpression(@"^[\p{L}\p{M}][\p{L}\p{M}\s.\-'’‌]{1,119}$",
         ErrorMessage = "Patient name may contain letters, spaces and . - ' only")]
    string PatientName,
    [Required] string PatientPhone,
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "کد ملی باید ۱۰ رقم باشد")]
    string NationalCode,
    [Required] InsuranceType InsuranceType,
    [Required] string StartJalali,
    [MaxLength(500)] string? Notes);

public record LookupByCodeRequest([Required] string ShortCode, [Required] string Phone);
public record CancelByPhoneRequest([Required] string Phone);

// The phone is required again at confirm time: the code alone must never be
// sufficient to cancel someone else's booking.
public record CancelConfirmRequest([Required] string Phone, [Required] string Code);

public record AppointmentDto(
    Guid Id,
    string ShortCode,
    Guid DoctorId,
    string DoctorName,
    string PatientName,
    string PatientPhone,
    string? NationalCode,
    string? InsuranceType,
    string StartJalali,
    string EndJalali,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    string Source,
    string? Notes,
    DateTime? CancelledAt,
    string? CancelReason,
    DateTime CreatedAt);

// ---------- Admin ----------

public record LoginRequest([Required] string Username, [Required] string Password);
public record LoginResponse(string AccessToken);

public record ScheduleInputDto(
    [Range(0, 6)] int Weekday,
    [Required, RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Times must be HH:mm")] string StartTime,
    [Required, RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Times must be HH:mm")] string EndTime);

public record UpsertDoctorRequest(
    [Required, MaxLength(150)] string FullName,
    [MaxLength(150)] string Specialty,
    [MaxLength(100)] string? Location,
    [Range(5, 240)] int DefaultVisitMinutes,
    bool IsActive = true,
    List<ScheduleInputDto>? Schedules = null);

public record ScheduleDto(int Weekday, string StartTime, string EndTime, bool IsActive);

public record DoctorAdminDto(
    Guid Id,
    string FullName,
    string Specialty,
    string Location,
    int DefaultVisitMinutes,
    bool IsActive,
    DateTime CreatedAt,
    List<ScheduleDto> Schedules);

public record AdminCancelRequest(string? Reason);
public record RescheduleRequest([Required] string NewStartJalali);

public record ImportRowErrorDto(int Row, string Error);

public record ImportResultDto(
    int TotalRows,
    int Imported,
    int Updated,
    int Skipped,
    List<ImportRowErrorDto> Errors,
    Guid SyncLogId);

public record SmsLogDto(
    Guid Id,
    Guid? AppointmentId,
    string Phone,
    string Type,
    string Body,
    string Status,
    string? ProviderMessageId,
    string? Error,
    DateTime CreatedAt,
    DateTime? SentAt);

public record DashboardStatsDto(
    int TodayAppointments,
    int Next7DaysAppointments,
    int ActiveDoctors,
    int FailedSms24h);

// ---------- Mapping ----------

public static class Mappings
{
    private static string? InsuranceFa(InsuranceType? t) => t switch
    {
        InsuranceType.Basic => "پایه",
        InsuranceType.Supplementary => "تکمیلی",
        _ => null
    };

    public static AppointmentDto ToDto(this Appointment a, string doctorName) => new(
        a.Id,
        a.ShortCode,
        a.DoctorId,
        doctorName,
        a.PatientName,
        a.PatientPhone,
        a.NationalCode,
        InsuranceFa(a.InsuranceType),
        Common.JalaliDate.ToJalaliDateTime(a.StartsAt),
        Common.JalaliDate.ToJalaliDateTime(a.EndsAt),
        a.StartsAt,
        a.EndsAt,
        a.Status.ToString(),
        a.Source.ToString(),
        a.Notes,
        a.CancelledAt,
        a.CancelReason,
        a.CreatedAt);

    public static DoctorAdminDto ToAdminDto(this Doctor d) => new(
        d.Id,
        d.FullName,
        d.Specialty,
        d.Location,
        d.DefaultVisitMinutes,
        d.IsActive,
        d.CreatedAt,
        d.Schedules
            .OrderBy(s => s.Weekday).ThenBy(s => s.StartTime)
            .Select(s => new ScheduleDto(s.Weekday, s.StartTime.ToString("HH:mm"), s.EndTime.ToString("HH:mm"), s.IsActive))
            .ToList());

    public static SmsLogDto ToDto(this SmsLog s) => new(
        s.Id, s.AppointmentId, s.Phone, s.Type.ToString(), s.Body,
        s.Status.ToString(), s.ProviderMessageId, s.Error, s.CreatedAt, s.SentAt);
}
