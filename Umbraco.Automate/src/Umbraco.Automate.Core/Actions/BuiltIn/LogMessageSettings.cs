using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="LogMessageAction"/>.
/// </summary>
public sealed class LogMessageSettings
{
    /// <summary>
    /// Gets or sets the message to log.
    /// </summary>
    [Field(
        Label = "Message",
        Description = "The message to write to the log.",
        SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "plaintext" },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log level. Defaults to "Information".
    /// Rendered as a dropdown; the available values mirror those recognised by
    /// <see cref="LogMessageAction.ParseLogLevel"/> (anything else falls back to Information).
    /// </summary>
    [Field(
        Label = "Log Level",
        Description = "The severity level used when writing the message to the log.",
        SortOrder = 1,
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """[{ "alias": "items", "value": ["Debug", "Information", "Warning", "Error"] }]""")]
    public string LogLevel { get; set; } = "Information";
}
