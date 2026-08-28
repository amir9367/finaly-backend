using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Dtos;
using Clinic.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

/// <summary>Public, unauthenticated endpoints used by the booking page.</summary>
[ApiController]
[Route("api/doctors")]
public class DoctorsController(AppDbContext db, IAvailabilityService availability) : ControllerBase
{
    [EnableRateLimiting("public-read")]
    [HttpGet]
    public async Task<ActionResult<List<DoctorDto>>> GetActiveDoctors(CancellationToken ct) =>
        await db.Doctors.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.FullName)
            .Select(d => new DoctorDto(d.Id, d.FullName, d.Specialty, d.Location, d.DefaultVisitMinutes))
            .ToListAsync(ct);

    /// <summary>Days, working hours and busy intervals for the booking window (next ≤14 days).</summary>
    [EnableRateLimiting("public-read")]
    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<List<DayAvailabilityDto>>> GetAvailability(Guid id, CancellationToken ct)
    {
        var days = await availability.GetWindowAsync(id, ct);
        return days.Select(d =>
        {
            var utcMidnight = JalaliDate.UtcFromTehran(d.DateIran.ToDateTime(TimeOnly.MinValue));
            return new DayAvailabilityDto(
                JalaliDate.ToJalaliDate(utcMidnight),
                d.DateIran.ToString("yyyy-MM-dd"),
                JalaliDate.WeekdayNamesFa[d.IranianWeekday],
                JalaliDate.WeekdayNamesEn[d.IranianWeekday],
                ToIntervals(d.WorkingHours),
                ToIntervals(d.Busy));
        }).ToList();
    }

    private static List<IntervalDto> ToIntervals(IEnumerable<TimeWindow> windows) =>
        windows.Select(w => new IntervalDto(w.From.ToString("HH:mm"), w.To.ToString("HH:mm"))).ToList();
}
