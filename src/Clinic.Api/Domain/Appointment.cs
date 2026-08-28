namespace Clinic.Api.Domain;

/// <summary>
/// An appointment. <see cref="StartsAt"/> and <see cref="EndsAt"/> are always UTC.
/// The database enforces non-overlap for Booked appointments of the same doctor
/// via a PostgreSQL exclusion constraint.
/// </summary>
public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Short human-readable tracking code shown to patients (e.g. "K7M3XQ2A").</summary>
    public string ShortCode { get; set; } = null!;

    public Guid DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public string PatientName { get; set; } = null!;
    public string PatientPhone { get; set; } = null!;
    public string? NationalCode { get; set; } // کد ملی ۱۰ رقمی
    public InsuranceType? InsuranceType { get; set; } // پایه / تکمیلی

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public AppointmentSource Source { get; set; } = AppointmentSource.Online;

    public string? Notes { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SmsLog> SmsLogs { get; set; } = new();
}
