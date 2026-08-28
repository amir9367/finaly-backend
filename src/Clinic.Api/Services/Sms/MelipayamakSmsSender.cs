using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Clinic.Api.Services.Sms;

/// <summary>
/// Melipayamak REST gateway (https://rest.payamak-panel.com/api/SendSMS/SendSMS).
/// Credentials come from configuration: Sms:Melipayamak:Username / Password / Origin.
/// </summary>
public class MelipayamakSmsSender(HttpClient http, IConfiguration config, ILogger<MelipayamakSmsSender> logger) : ISmsSender
{
    private const string Endpoint = "https://rest.payamak-panel.com/api/SendSMS/SendSMS";

    public async Task<string?> SendAsync(string phone, string body, CancellationToken ct = default)
    {
        var username = config["Sms:Melipayamak:Username"];
        var password = config["Sms:Melipayamak:Password"];
        var origin = config["Sms:Melipayamak:Origin"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(origin))
            throw new InvalidOperationException(
                "Melipayamak credentials are not configured (Sms:Melipayamak:Username/Password/Origin).");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["to"] = phone,
            ["from"] = origin,
            ["text"] = body,
            ["isflash"] = "false",
        });

        using var response = await http.PostAsync(Endpoint, content, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Melipayamak HTTP {Status}: {Body}", (int)response.StatusCode, raw);
            throw new InvalidOperationException($"Melipayamak returned HTTP {(int)response.StatusCode}.");
        }

        JsonElement json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(raw);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Melipayamak returned a non-JSON body: {raw[..Math.Min(raw.Length, 200)]}");
        }

        var retStatus = json.TryGetProperty("RetStatus", out var rs) && rs.ValueKind == JsonValueKind.Number ? rs.GetInt32() : -1;
        var value = json.TryGetProperty("Value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        // RetStatus 1 == queued successfully.
        if (retStatus != 1)
            throw new InvalidOperationException($"Melipayamak rejected the message (RetStatus={retStatus}).");

        return value;
    }
}
