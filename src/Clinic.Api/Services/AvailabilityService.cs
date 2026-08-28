using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Clinic.Api.Services;

public sealed record TimeWindow(TimeOnly From, TimeOnly To);

public sealed record AvailabilityDay(
    DateOnly DateIran,
    int IranianWeekday,
    List<TimeWindow> WorkingHours,
    List<TimeWindow> Busy);

public interface IAvailabilityService
{
    /// <summary>How many days ahead bookings are allowed (default 14).</summary>
    int WindowDays { get; }

    /// <summary>
    /// Availability of one doctor from today (Tehran) through the booking window.
    /// Only days that have working hours or bookings are returned.
    /// </summary>
    Task<List<AvailabilityDay>> GetWindowAsync(Guid doctorId, CancellationToken ct = default);

    /// <summary>True when [startsAtUtc, endsAtUtc) lies fully inside one active schedule window.</summary>
    Task<bool> IsInsideWorkingHoursAsync(Doctor doctor, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken ct = default);
}

public class AvailabilityService(AppDbContext db, IConfiguration config) : IAvailabilityService
{
    public int WindowDays => int.TryParse(config["Booking:WindowDays"], out var days) && days > 0 ? days : 14;

    public async Task<List<AvailabilityDay>> GetWindowAsync(Guid doctorId, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.AsNoTracking()
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive, ct)
            ?? throw new NotFoundException("Doctor not found.");

        var today = DateOnly.FromDateTime(JalaliDate.NowTehran.Date);
        var lastDate = today.AddDays(WindowDays - 1);
        var windowStartUtc = JalaliDate.UtcFromTehran(today.ToDateTime(TimeOnly.MinValue));
        var windowEndUtc = JalaliDate.UtcFromTehran(lastDate.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var booked = await db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                        && a.Status == AppointmentStatus.Booked
                        && a.StartsAt < windowEndUtc
                        && a.EndsAt > windowStartUtc)
            .Select(a => new { a.StartsAt, a.EndsAt })
            .ToListAsync(ct);

        var result = new List<AvailabilityDay>(WindowDays);
        for (var date = today; date <= lastDate; date = date.AddDays(1))
        {
            var weekday = JalaliDate.IranianWeekday(date.DayOfWeek);

            var workingHours = doctor.Schedules
                .Where(s => s.IsActive && s.Weekday == weekday)
                .Select(s => new TimeWindow(s.StartTime, s.EndTime))
                .OrderBy(w => w.From)
                .ToList();

            var dayStartUtc = JalaliDate.UtcFromTehran(date.ToDateTime(TimeOnly.MinValue));
            var dayEndUtc = JalaliDate.UtcFromTehran(date.AddDays(1).ToDateTime(TimeOnly.MinValue));

            var busy = booked
                .Where(a => a.StartsAt < dayEndUtc && a.EndsAt > dayStartUtc)
                .Select(a => ClipToLocalDay(a.StartsAt, a.EndsAt, date))
                .OrderBy(w => w.From)
                .ToList();

            if (workingHours.Count > 0 || busy.Count > 0)
                result.Add(new AvailabilityDay(date, weekday, workingHours, busy));
        }

        return result;
    }

    public Task<bool> IsInsideWorkingHoursAsync(Doctor doctor, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken ct = default)
    {
        var startLocal = JalaliDate.UtcToTehran(startsAtUtc);
        var endLocal = JalaliDate.UtcToTehran(endsAtUtc);
        if (startLocal.Date != endLocal.Date) return Task.FromResult(false);

        var weekday = JalaliDate.IranianWeekday(startLocal.DayOfWeek);
        var start = TimeOnly.FromDateTime(startLocal);
        var end = TimeOnly.FromDateTime(endLocal);

        var fits = doctor.Schedules.Any(s =>
            s.IsActive
            && s.Weekday == weekday
            && SlotMath.FitsInWindow(start, end, s.StartTime, s.EndTime));

        return Task.FromResult(fits);
    }

    private static TimeWindow ClipToLocalDay(DateTime startsAtUtc, DateTime endsAtUtc, DateOnly date)
    {
        var fromLocal = JalaliDate.UtcToTehran(startsAtUtc);
        var toLocal = JalaliDate.UtcToTehran(endsAtUtc);
        var from = DateOnly.FromDateTime(fromLocal) == date ? TimeOnly.FromDateTime(fromLocal) : TimeOnly.MinValue;
        var to = DateOnly.FromDateTime(toLocal) == date ? TimeOnly.FromDateTime(toLocal) : TimeOnly.MaxValue;
        return new TimeWindow(from, to);
    }
}
