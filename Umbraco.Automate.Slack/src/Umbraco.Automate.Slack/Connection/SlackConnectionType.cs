using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;

namespace Umbraco.Automate.Slack.Connection;

/// <summary>
/// Connection type for Slack workspaces using OAuth via OpenIddict WebIntegration.
/// </summary>
[ConnectionType("slack", "Slack", Group = "Messaging", Icon = "icon-message", Description = "Connect to a Slack workspace")]
public sealed class SlackConnectionType : OAuthConnectionTypeBase<SlackConnectionSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SlackConnectionType"/> class.
    /// </summary>
    public SlackConnectionType(ConnectionTypeInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override string ProviderName => "Slack";
}
