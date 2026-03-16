using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Notifications.Channels.BuiltIn;

/// <summary>
/// Notification channel that sends a JSON POST to a configured webhook URL.
/// Optionally signs the payload with HMAC-SHA256.
/// </summary>
[NotificationChannel("umbracoAutomate.webhook", "Webhook",
    Description = "Sends a JSON POST to a webhook URL when a run fails.",
    Icon = "icon-webhook")]
public sealed class WebhookNotificationChannel : NotificationChannelBase<WebhookNotificationChannelSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookNotificationChannel"/> class.
    /// </summary>
    public WebhookNotificationChannel(
        NotificationChannelInfrastructure infrastructure,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotificationChannel> logger)
        : base(infrastructure)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task NotifyAsync(
        NotificationMessage message,
        WebhookNotificationChannelSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            _logger.LogWarning("Webhook notification channel has no URL configured, skipping");
            return;
        }

        var client = _httpClientFactory.CreateClient("UmbracoAutomate");
        var payload = JsonSerializer.Serialize(message.Data ?? message);

        var request = new HttpRequestMessage(HttpMethod.Post, settings.Url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(settings.Secret))
        {
            var signature = ComputeHmacSha256(payload, settings.Secret);
            request.Headers.Add("X-Automate-Signature", $"sha256={signature}");
        }

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Webhook notification to {Url} returned {StatusCode}",
                settings.Url, response.StatusCode);
        }
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
