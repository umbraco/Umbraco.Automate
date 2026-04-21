namespace Umbraco.Automate.Slack.Configuration;

/// <summary>
/// Provides Slack-specific scope configuration, separating bot scopes (registered with OpenIddict)
/// from user scopes (sent via the non-standard <c>user_scope</c> parameter).
/// </summary>
internal interface ISlackScopeConfiguration
{
    /// <summary>
    /// Gets the user scopes to request via the <c>user_scope</c> parameter in the Slack V2 authorize URL.
    /// Configured at <c>Umbraco:Automate:Providers:Slack:UserScopes</c>.
    /// </summary>
    string[] UserScopes { get; }
}
