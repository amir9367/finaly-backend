using Clinic.Api.Common;
using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Dtos;
using Clinic.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Controllers;

/// <summary>Public booking endpoints used by the patient-facing page.</summary>
[ApiController]
[Route("api/appointments")]
public class AppointmentsController(IBookingService booking, AppDbContext db) : ControllerBase
{
    /// <summary>Books an appointment. Sends a confirmation SMS to the patient.</summary>
    [EnableRateLimiting("public-book")]
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Book(BookAppointmentRequest request, CancellationToken ct)
    {
        var dto = await booking.BookAsync(request, AppointmentSource.Online, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>
    /// Finds a booking by its tracking code + phone. A POST keeps the phone out
    /// of URLs (and therefore out of access logs and browser history).
    /// </summary>
    [EnableRateLimiting("public-lookup")]
    [HttpPost("lookup")]
    public async Task<ActionResult<AppointmentDto>> Lookup(LookupByCodeRequest request, CancellationToken ct)
    {
        var normalized = PhoneNormalizer.Normalize(request.Phone ?? "");
        var shortCode = request.ShortCode?.Trim().ToUpperInvariant() ?? "";

        var appointment = await db.Appointments.AsNoTracking()
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.ShortCode == shortCode, ct);

        if (appointment is null || PhoneNormalizer.Normalize(appointment.PatientPhone) != normalized)
            throw new NotFoundException("No appointment matches this code and phone number.");

        return appointment.ToDto(appointment.Doctor?.FullName ?? "");
    }

    /// <summary>Step 1 of self-cancel: sends an OTP to the booking's phone.</summary>
    [EnableRateLimiting("otp-request")]
    [HttpPost("{id:guid}/cancel/request")]
    public async Task<IActionResult> RequestCancel(Guid id, CancelByPhoneRequest request, CancellationToken ct)
    {
        await booking.RequestCancelOtpAsync(id, request.Phone, ct);
        return NoContent();
    }

    /// <summary>Step 2 of self-cancel: verifies phone + OTP and cancels.</summary>
    [EnableRateLimiting("otp-confirm")]
    [HttpPost("{id:guid}/cancel/confirm")]
    public async Task<ActionResult<AppointmentDto>> ConfirmCancel(Guid id, CancelConfirmRequest request, CancellationToken ct) =>
        await booking.ConfirmCancelByPatientAsync(id, request.Phone, request.Code, ct);
}
