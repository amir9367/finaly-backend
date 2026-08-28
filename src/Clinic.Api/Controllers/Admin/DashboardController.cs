using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/dashboard")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(JalaliDate.NowTehran.Date);
        var todayStartUtc = JalaliDate.UtcFromTehran(today.ToDateTime(TimeOnly.MinValue));
        var tomorrowStartUtc = JalaliDate.UtcFromTehran(today.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var weekEndUtc = JalaliDate.UtcFromTehran(today.AddDays(7).ToDateTime(TimeOnly.MinValue));
        var dayAgoUtc = DateTime.UtcNow.AddDays(-1);

        var booked = db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Booked);

        var stats = new DashboardStatsDto(
            await booked.CountAsync(a => a.StartsAt >= todayStartUtc && a.StartsAt < tomorrowStartUtc, ct),
            await booked.CountAsync(a => a.StartsAt >= todayStartUtc && a.StartsAt < weekEndUtc, ct),
            await db.Doctors.CountAsync(d => d.IsActive, ct),
            await db.SmsLogs.CountAsync(s => s.Status == SmsStatus.Failed && s.CreatedAt >= dayAgoUtc, ct));

        return stats;
    }
}
