using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="ApplicationStartedTrigger"/> trigger.
/// </summary>
public sealed class ApplicationStartedTriggerSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the trigger should only fire on the main server.
    /// Every server in a load-balanced setup raises its own startup notification, so without
    /// this the automation runs once per server. When enabled, startups on
    /// <c>Subscriber</c> servers are ignored.
    /// </summary>
    [Field(
        Label = "Only run on the main server",
        Description = "In a load-balanced setup every server starts independently, so this automation would run once per server. Enable to only run when the main (scheduling publisher) server starts. Most reliable when server roles are configured explicitly — with automatic election a server's role may not yet be known at startup, in which case it still runs.",
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool OnlyRunOnMainServer { get; set; }
}
