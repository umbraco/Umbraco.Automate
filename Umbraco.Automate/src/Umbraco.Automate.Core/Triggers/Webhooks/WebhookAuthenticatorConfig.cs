using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

namespace Umbraco.Automate.Core.Triggers.Webhooks;

/// <summary>
/// Configured authentication for a webhook trigger: the selected strategy alias and its
/// strategy-specific settings.
/// </summary>
public sealed class WebhookAuthenticatorConfig
{
    /// <summary>
    /// Gets or sets the alias of the selected <see cref="IWebhookAuthenticator"/>.
    /// Defaults to the built-in plain-secret strategy.
    /// </summary>
    public string Alias { get; set; } = PlainSecretWebhookAuthenticator.WellKnownAlias;

    /// <summary>
    /// Gets or sets the strategy-specific settings. The shape is defined by the authenticator
    /// identified by <see cref="Alias"/>; see its <see cref="IWebhookAuthenticator.SettingsType"/>.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}
