using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Authenticates webhooks by validating an HMAC-SHA256 signature in the
/// <c>X-Webhook-Signature</c> header (format: <c>sha256=&lt;hex&gt;</c>).
/// Compatible with GitHub's <c>X-Hub-Signature-256</c> scheme.
/// </summary>
public sealed class HmacSha256WebhookAuthenticator : IWebhookAuthenticator
{
    internal const string SignatureHeaderName = "X-Webhook-Signature";

    /// <inheritdoc />
    public string Alias => "hmac-sha256";

    /// <inheritdoc />
    public string Name => "HMAC-SHA256 Signature";

    /// <inheritdoc />
    public bool Validate(WebhookAuthenticationContext context)
    {
        var header = context.Request.Headers[SignatureHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = header["sha256=".Length..];

        // SHA-256 = 32 bytes = 64 hex chars.
        if (providedHex.Length != 64)
        {
            return false;
        }

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(context.Secret);
        var payloadBytes = Encoding.UTF8.GetBytes(context.Body ?? string.Empty);
        var expectedBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
