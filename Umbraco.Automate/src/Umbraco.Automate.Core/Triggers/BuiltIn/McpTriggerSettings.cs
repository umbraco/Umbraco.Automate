using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="McpTrigger"/>.
/// </summary>
public sealed class McpTriggerSettings
{
    [Field(
        Label = "Tool Name",
        Description = "The name the AI agent sees for this tool. Letters, numbers, hyphens, and underscores only.")]
    [RegularExpression("^[a-zA-Z0-9_-]{1,64}$", ErrorMessage = "Tool name may only contain letters, numbers, hyphens, and underscores.")]
    public string ToolName { get; set; } = string.Empty;

    [Field(
        Label = "Tool Description",
        Description = "Tells the AI agent when to use this tool.",
        EditorUiAlias = "Umb.PropertyEditorUi.TextArea",
        SortOrder = 10)]
    public string ToolDescription { get; set; } = string.Empty;

    [Field(
        Label = "Input Fields",
        Description = "The arguments this tool accepts. Each becomes part of the tool's schema and is available to steps as Arguments[\"name\"].",
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.McpInputFieldsBuilder",
        SortOrder = 20)]
    public List<McpToolInputField> InputFields { get; set; } = [];

    [Field(
        Label = "Secret",
        Description = "Bearer token the caller must send in the Authorization header. Leave blank to allow calls with no authentication.",
        IsSensitive = true,
        EditorUiAlias = "Umb.Automate.WebhookSecretField",
        SortOrder = 30)]
    public string? Secret { get; set; }

    [Field(
        Label = "Timeout (seconds)",
        Description = "How long to wait for the run to finish before telling the agent it's still running.",
        SortOrder = 40)]
    public int TimeoutSeconds { get; set; } = 30;

    [Field(
        Label = "Test arguments",
        Description = "The arguments to use when running this automation on demand, as a JSON object matching the input fields above.",
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 160 },
                { "alias": "wordWrap", "value": true }
            ]
            """,
        SortOrder = 100)]
    public string? TestArguments { get; set; }
}
