using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Authenticates webhooks by validating an HMAC-SHA256 signature in the
/// <c>X-Webhook-Signature</c> header (format: <c>sha256=&lt;hex&gt;</c>).
/// Compatible with GitHub's <c>X-Hub-Signature-256</c> scheme.
/// </summary>
[WebhookAuthenticator(WellKnownAlias, "HMAC-SHA256 Signature")]
public sealed class HmacSha256WebhookAuthenticator : WebhookAuthenticatorBase<HmacSha256WebhookAuthenticatorSettings>
{
    /// <summary>
    /// Well-known alias for this authenticator.
    /// </summary>
    public const string WellKnownAlias = "hmac-sha256";

    internal const string SignatureHeaderName = "X-Webhook-Signature";

    /// <inheritdoc />
    protected override bool Validate(WebhookAuthenticationContext context, HmacSha256WebhookAuthenticatorSettings settings)
    {
        if (string.IsNullOrEmpty(settings.SigningKey))
        {
            return false;
        }

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

        var keyBytes = Encoding.UTF8.GetBytes(settings.SigningKey);
        var payloadBytes = Encoding.UTF8.GetBytes(context.Body ?? string.Empty);
        var expectedBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
