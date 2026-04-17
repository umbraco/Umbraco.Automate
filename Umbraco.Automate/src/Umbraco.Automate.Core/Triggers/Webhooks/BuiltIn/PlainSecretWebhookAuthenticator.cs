using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Authenticates webhooks by comparing a plain secret token sent in the
/// <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter.
/// </summary>
public sealed class PlainSecretWebhookAuthenticator : WebhookAuthenticatorBase<PlainSecretWebhookAuthenticatorSettings>
{
    /// <summary>
    /// Well-known alias for this authenticator, also used as the default for webhook triggers.
    /// </summary>
    public const string WellKnownAlias = "plain-secret";

    internal const string SecretHeaderName = "X-Webhook-Secret";
    internal const string SecretQueryParam = "secret";

    /// <inheritdoc />
    public override string Alias => WellKnownAlias;

    /// <inheritdoc />
    public override string Name => "Plain Secret";

    /// <inheritdoc />
    public override string? Description => "Caller sends the secret as a token in the X-Webhook-Secret header or ?secret= query parameter.";

    /// <inheritdoc />
    public override bool RequiresBody => false;

    /// <inheritdoc />
    protected override bool Validate(WebhookAuthenticationContext context, PlainSecretWebhookAuthenticatorSettings settings)
    {
        if (string.IsNullOrEmpty(settings.Secret))
        {
            return false;
        }

        var providedSecret = context.Request.Headers[SecretHeaderName].FirstOrDefault()
                             ?? context.Request.Query[SecretQueryParam].FirstOrDefault();

        if (string.IsNullOrEmpty(providedSecret))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(settings.Secret),
            Encoding.UTF8.GetBytes(providedSecret));
    }
}
