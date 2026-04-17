using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Authenticates webhooks by comparing a plain secret token sent in the
/// <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter.
/// </summary>
public sealed class PlainSecretWebhookAuthenticator : IWebhookAuthenticator
{
    /// <summary>
    /// Well-known alias for this authenticator, also used as the default for webhook triggers.
    /// </summary>
    public const string WellKnownAlias = "plain-secret";

    internal const string SecretHeaderName = "X-Webhook-Secret";
    internal const string SecretQueryParam = "secret";

    /// <inheritdoc />
    public string Alias => WellKnownAlias;

    /// <inheritdoc />
    public string Name => "Plain Secret";

    /// <inheritdoc />
    public string? Description => "Caller sends the secret as a token in the X-Webhook-Secret header or ?secret= query parameter.";

    /// <inheritdoc />
    public bool RequiresBody => false;

    /// <inheritdoc />
    public bool Validate(WebhookAuthenticationContext context)
    {
        var providedSecret = context.Request.Headers[SecretHeaderName].FirstOrDefault()
                             ?? context.Request.Query[SecretQueryParam].FirstOrDefault();

        if (string.IsNullOrEmpty(providedSecret))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(context.Secret),
            Encoding.UTF8.GetBytes(providedSecret));
    }
}
