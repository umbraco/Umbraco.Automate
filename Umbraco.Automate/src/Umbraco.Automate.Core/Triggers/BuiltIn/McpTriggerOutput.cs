namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="McpTrigger"/> containing the AI agent's tool-call arguments.
/// </summary>
public sealed class McpTriggerOutput
{
    public Dictionary<string, object?> Arguments { get; init; } = [];
}
