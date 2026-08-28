using Clinic.Api.Data;
using Clinic.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clinic.Api.Services.Sms;

/// <summary>
/// Orchestrates SMS delivery: persists a <see cref="SmsLog"/> then sends in the
/// background via <see cref="ISmsSender"/> and records the outcome. Callers
/// never block and bookings never fail because of an SMS problem.
/// </summary>
public class SmsService(IServiceProvider serviceProvider, ILogger<SmsService> logger) : ISmsService
{
    // Bounds concurrent provider calls so a flood of bookings cannot open
    // unbounded parallel HTTP connections to Melipayamak.
    private static readonly SemaphoreSlim DeliveryGate = new(4, 4);

    public void Queue(SmsType type, Guid appointmentId, string phone, string body) =>
        _ = Task.Run(() => DeliverAsync(type, appointmentId, phone, body));

    private async Task DeliverAsync(SmsType type, Guid appointmentId, string phone, string body)
    {
        await DeliveryGate.WaitAsync();
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

            var log = new SmsLog
            {
                AppointmentId = appointmentId,
                Phone = phone,
                Type = type,
                // The real body goes to the provider below; the audit row never
                // stores a usable OTP code.
                Body = MaskSecrets(type, body),
                Status = SmsStatus.Pending,
            };
            db.SmsLogs.Add(log);
            await db.SaveChangesAsync();

            try
            {
                log.ProviderMessageId = await sender.SendAsync(phone, body);
                log.Status = SmsStatus.Sent;
                log.SentAt = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                log.Status = SmsStatus.Failed;
                log.Error = "Cancelled.";
            }
            catch (Exception ex)
            {
                log.Status = SmsStatus.Failed;
                log.Error = ex.Message[..Math.Min(ex.Message.Length, 500)];
                logger.LogError(ex, "SMS to {Phone} failed (appointment {AppointmentId})", phone, appointmentId);
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Last-resort guard: never let background delivery crash the process.
            logger.LogError(ex, "Unexpected failure while delivering SMS to {Phone}", phone);
        }
        finally
        {
            DeliveryGate.Release();
        }
    }

    private static string MaskSecrets(SmsType type, string body) =>
        type == SmsType.CancelOtp
            ? System.Text.RegularExpressions.Regex.Replace(body, @"\b\d{6}\b", "••••••")
            : body;
}

public interface ISmsService
{
    /// <summary>Persists and asynchronously sends an SMS.</summary>
    void Queue(SmsType type, Guid appointmentId, string phone, string body);
}

public static class SmsQueryableExtensions
{
    public static IQueryable<SmsLog> RecentFirst(this IQueryable<SmsLog> query) =>
        query.OrderByDescending(s => s.CreatedAt);
}
