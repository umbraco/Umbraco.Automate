using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Triggers.Webhooks;

/// <summary>
/// Builder for the webhook authenticator collection.
/// </summary>
public class WebhookAuthenticatorCollectionBuilder
    : LazyCollectionBuilderBase<WebhookAuthenticatorCollectionBuilder, WebhookAuthenticatorCollection, IWebhookAuthenticator>
{
    /// <inheritdoc />
    protected override WebhookAuthenticatorCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered webhook authenticators.
/// </summary>
public sealed class WebhookAuthenticatorCollection : BuilderCollectionBase<IWebhookAuthenticator>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookAuthenticatorCollection"/> class.
    /// </summary>
    public WebhookAuthenticatorCollection(Func<IEnumerable<IWebhookAuthenticator>> items) : base(items) { }

    /// <summary>
    /// Gets an authenticator by its alias.
    /// </summary>
    public IWebhookAuthenticator? GetByAlias(string alias)
        => this.FirstOrDefault(a => string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
