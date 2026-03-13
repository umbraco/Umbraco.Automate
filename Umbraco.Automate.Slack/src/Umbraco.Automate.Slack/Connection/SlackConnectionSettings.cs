using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Slack.Connection;

/// <summary>
/// Settings for the Slack connection type.
/// </summary>
public class SlackConnectionSettings
{
    /// <summary>
    /// Gets or sets the OAuth credential ID linking to the stored Slack workspace tokens.
    /// </summary>
    [Field(Label = "Slack Workspace",
        Description = "Authenticate with your Slack workspace",
        EditorUiAlias = "Umb.Automate.OAuth",
        EditorConfig = """[{ "alias": "provider", "value": "Slack" }]""")]
    public Guid? OAuthCredentialsId { get; set; }
}
