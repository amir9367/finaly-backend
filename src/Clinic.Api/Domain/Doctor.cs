namespace Clinic.Api.Domain;

public class Doctor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = null!;
    public string Specialty { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int DefaultVisitMinutes { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<DoctorSchedule> Schedules { get; set; } = new();
}
