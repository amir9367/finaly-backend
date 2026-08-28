namespace Clinic.Api.Domain;

/// <summary>
/// Audit row for every SMS the system attempts (booking confirmation,
/// cancellation notice or self-cancel OTP), with its delivery outcome.
/// </summary>
public class SmsLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string Phone { get; set; } = null!;
    public SmsType Type { get; set; }

    /// <summary>Message body; OTP codes are masked before persisting.</summary>
    public string Body { get; set; } = null!;

    public SmsStatus Status { get; set; } = SmsStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
