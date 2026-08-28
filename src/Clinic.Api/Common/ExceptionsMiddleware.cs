using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinic.Api.Common;

/// <summary>
/// Maps domain exceptions to consistent JSON error responses:
/// { "error": "message" }. Also converts PostgreSQL exclusion violations
/// (double-booking races) into HTTP 409.
/// </summary>
public class ExceptionsMiddleware(RequestDelegate next, ILogger<ExceptionsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex) { await WriteAsync(context, StatusCodes.Status404NotFound, ex.Message); }
        catch (ConflictException ex) { await WriteAsync(context, StatusCodes.Status409Conflict, ex.Message); }
        catch (ValidationException ex) { await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message); }
        catch (UnauthorizedException ex) { await WriteAsync(context, StatusCodes.Status401Unauthorized, ex.Message); }
        catch (PostgresException pe) when (pe.SqlState == "23P01")
        {
            await WriteAsync(context, StatusCodes.Status409Conflict,
                "This time slot was just booked by someone else. Please choose another time.");
        }
        catch (DbUpdateException due) when (due.InnerException is PostgresException pg && pg.SqlState == "23P01")
        {
            await WriteAsync(context, StatusCodes.Status409Conflict,
                "This time slot was just booked by someone else. Please choose another time.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Path}", context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "Unexpected server error.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
