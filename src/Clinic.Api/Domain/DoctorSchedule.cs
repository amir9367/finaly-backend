namespace Clinic.Api.Domain;

/// <summary>
/// Weekly recurring working hours. <see cref="Weekday"/> uses the Iranian week:
/// 0 = Saturday … 6 = Friday.
/// </summary>
public class DoctorSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public int Weekday { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}
