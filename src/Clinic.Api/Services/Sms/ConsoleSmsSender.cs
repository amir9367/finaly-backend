using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Clinic.Api.Services.Sms;

/// <summary>
/// Development transport: writes the SMS to the application log so the whole
/// system can be exercised without provider credentials. Startup refuses this
/// sender outside Development; OTP codes are masked even here so logs stay
/// safe to share.
/// </summary>
public class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task<string?> SendAsync(string phone, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[SMS -> {Phone}] {Body}", phone, MaskOtp(body));
        return Task.FromResult<string?>(null);
    }

    private static string MaskOtp(string body) =>
        Regex.Replace(body, @"\b\d{6}\b", "••••••");
}
