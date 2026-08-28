using Clinic.Api.Data;
using Clinic.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/sms")]
public class SmsAdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("logs")]
    public async Task<ActionResult<List<SmsLogDto>>> Logs([FromQuery] int take = 100, CancellationToken ct = default)
    {
        if (take is < 1 or > 500) take = 100;
        var logs = await db.SmsLogs.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
        return logs.Select(s => s.ToDto()).ToList();
    }
}
