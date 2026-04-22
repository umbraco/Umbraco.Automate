using Umbraco.Automate.OpenIddict.Providers;

// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time.
// Describes the appsettings.json shape below Umbraco:Automate:Providers:Slack so
// tooling can give editors IntelliSense against appsettings-schema.Umbraco.Automate.Slack.json.
internal sealed class UmbracoAutomateSlackSchema
{
    /// <summary>
    /// Configuration container for all Umbraco products.
    /// </summary>
    public required UmbracoDefinition Umbraco { get; set; }

    public sealed class UmbracoDefinition
    {
        /// <summary>
        /// Configuration of Umbraco Automate.
        /// </summary>
        public required UmbracoAutomateDefinition Automate { get; set; }
    }

    public sealed class UmbracoAutomateDefinition
    {
        /// <summary>
        /// OAuth provider credentials, keyed by provider name.
        /// </summary>
        public required ProvidersDefinition Providers { get; set; }
    }

    public sealed class ProvidersDefinition
    {
        /// <summary>
        /// Slack OAuth app credentials and scope configuration.
        /// </summary>
        public required SlackProviderDefinition Slack { get; set; }
    }

    /// <summary>
    /// Slack provider section (<c>Umbraco:Automate:Providers:Slack</c>). Inherits
    /// the shared OAuth fields (<c>ClientId</c>, <c>ClientSecret</c>, <c>Scopes</c>)
    /// from <see cref="OAuthProviderConfiguration"/> and adds the Slack-specific
    /// <c>UserScopes</c> array, which is sent via Slack V2's non-standard
    /// <c>user_scope</c> parameter.
    /// </summary>
    public sealed class SlackProviderDefinition : OAuthProviderConfiguration
    {
        /// <summary>
        /// Gets or sets Slack user scopes — requested via the <c>user_scope</c>
        /// parameter in the Slack V2 authorize URL. Leave empty to request a
        /// bot-only token.
        /// </summary>
        public string[] UserScopes { get; set; } = [];
    }
}
