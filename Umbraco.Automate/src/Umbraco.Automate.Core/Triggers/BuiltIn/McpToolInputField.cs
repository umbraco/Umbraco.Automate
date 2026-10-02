namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// One argument the <see cref="McpTrigger"/>'s tool accepts. Becomes a property in the tool's
/// JSON input schema shown to the AI agent, and a key in <see cref="McpTriggerOutput.Arguments"/>.
/// </summary>
public sealed class McpToolInputField
{
    public string Name { get; set; } = string.Empty;

    public McpToolInputFieldType Type { get; set; } = McpToolInputFieldType.Text;

    public string? Description { get; set; }

    public bool Required { get; set; }
}
