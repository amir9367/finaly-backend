namespace Clinic.Api.Domain;

/// <summary>One-time SMS code used by patients to cancel their own booking.</summary>
public class PhoneOtp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string Phone { get; set; } = null!;
    /// <summary>SHA-256 hex of the 6-digit code.</summary>
    public string CodeHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool Used { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
