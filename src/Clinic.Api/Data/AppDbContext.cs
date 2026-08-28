using Clinic.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<PhoneOtp> PhoneOtps => Set<PhoneOtp>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<ExcelSyncLog> ExcelSyncLogs => Set<ExcelSyncLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Doctor>(e =>
        {
            e.Property(d => d.FullName).HasMaxLength(150).IsRequired();
            e.Property(d => d.Specialty).HasMaxLength(150);
            e.Property(d => d.Location).HasMaxLength(100);
            e.HasIndex(d => d.FullName);
        });

        modelBuilder.Entity<DoctorSchedule>(e =>
        {
            e.HasIndex(s => new { s.DoctorId, s.Weekday });
            e.HasOne(s => s.Doctor)
                .WithMany(d => d.Schedules)
                .HasForeignKey(s => s.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Appointment>(e =>
        {
            e.Property(a => a.ShortCode).HasMaxLength(16).IsRequired();
            e.Property(a => a.PatientName).HasMaxLength(120).IsRequired();
            e.Property(a => a.PatientPhone).HasMaxLength(20).IsRequired();
            e.Property(a => a.NationalCode).HasMaxLength(10);
            e.Property(a => a.InsuranceType).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Notes).HasMaxLength(500);
            e.HasIndex(a => a.ShortCode).IsUnique();
            e.HasIndex(a => a.PatientPhone); // per-phone active-booking cap
            e.HasIndex(a => new { a.DoctorId, a.StartsAt });
            e.HasIndex(a => new { a.Status, a.StartsAt }); // exclusion-constraint filter + dashboard range scans
            e.HasOne(a => a.Doctor)
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PhoneOtp>(e =>
        {
            e.Property(o => o.Phone).HasMaxLength(20).IsRequired();
            e.Property(o => o.CodeHash).HasMaxLength(64).IsRequired();
            e.HasIndex(o => o.AppointmentId);
        });

        modelBuilder.Entity<SmsLog>(e =>
        {
            e.Property(s => s.Phone).HasMaxLength(20).IsRequired();
            e.Property(s => s.Body).HasMaxLength(500).IsRequired();
            e.Property(s => s.Error).HasMaxLength(500);
            e.HasIndex(s => s.CreatedAt);
            e.HasOne(s => s.Appointment)
                .WithMany(a => a.SmsLogs)
                .HasForeignKey(s => s.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AdminUser>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<ExcelSyncLog>(e =>
        {
            e.Property(l => l.FileName).HasMaxLength(260).IsRequired();
        });
    }
}
