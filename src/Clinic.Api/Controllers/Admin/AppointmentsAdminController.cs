using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Clinic.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/admin/appointments")]
public class AppointmentsAdminController(AppDbContext db, IBookingService booking) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> Query(
        [FromQuery] Guid? doctorId,
        [FromQuery] string? status,
        [FromQuery] string? fromJalali,
        [FromQuery] string? toJalali,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        if (take is < 1 or > 1000) take = 200;

        // No Include: the projection below selects only the doctor's name, so EF
        // joins it itself — an Include here would be ignored anyway.
        var query = db.Appointments.AsNoTracking().AsQueryable();

        if (doctorId is { } doctorFilter)
            query = query.Where(a => a.DoctorId == doctorFilter);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out var statusFilter)
            && Enum.IsDefined(statusFilter)) // reject numeric/out-of-range junk like "7"
            query = query.Where(a => a.Status == statusFilter);

        if (!string.IsNullOrWhiteSpace(fromJalali) && JalaliDate.TryParse(fromJalali, out var fromTehran))
            query = query.Where(a => a.StartsAt >= JalaliDate.UtcFromTehran(fromTehran.Date));

        if (!string.IsNullOrWhiteSpace(toJalali) && JalaliDate.TryParse(toJalali, out var toTehran))
            query = query.Where(a => a.StartsAt < JalaliDate.UtcFromTehran(toTehran.Date.AddDays(1)));

        var rows = await query
            .OrderBy(a => a.StartsAt)
            .Take(take)
            .Select(a => new { Appointment = a, DoctorName = a.Doctor != null ? a.Doctor.FullName : "" })
            .ToListAsync(ct);

        return rows.Select(r => r.Appointment.ToDto(r.DoctorName)).ToList();
    }

    /// <summary>Cancels on behalf of the clinic — the patient receives an SMS notice.</summary>
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult<AppointmentDto>> Cancel(Guid id, AdminCancelRequest? request, CancellationToken ct) =>
        await booking.AdminCancelAsync(id, request?.Reason, ct);

    [HttpPut("{id:guid}/reschedule")]
    public async Task<ActionResult<AppointmentDto>> Reschedule(Guid id, RescheduleRequest request, CancellationToken ct) =>
        await booking.RescheduleAsync(id, request, ct);
}
