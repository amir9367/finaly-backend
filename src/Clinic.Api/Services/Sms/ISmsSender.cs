namespace Clinic.Api.Services.Sms;

/// <summary>Low-level SMS transport. Implementations must throw on failure.</summary>
public interface ISmsSender
{
    /// <summary>Sends one message; returns the provider message id when available.</summary>
    Task<string?> SendAsync(string phone, string body, CancellationToken ct = default);
}
