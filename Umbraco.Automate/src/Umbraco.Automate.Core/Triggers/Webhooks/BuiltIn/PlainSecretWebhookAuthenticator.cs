using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Authenticates webhooks by comparing a plain secret token sent in the
/// <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter.
/// </summary>
public sealed class PlainSecretWebhookAuthenticator : IWebhookAuthenticator
{
    internal const string SecretHeaderName = "X-Webhook-Secret";
    internal const string SecretQueryParam = "secret";

    /// <inheritdoc />
    public string Alias => "plain-secret";

    /// <inheritdoc />
    public string Name => "Plain Secret";

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
