using System.Text.Json;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that makes an automation callable as an MCP tool over its own URL.
/// </summary>
[Trigger(WellKnownAlias, "MCP Tool",
    Description = "Fires when an AI agent calls this automation's MCP tool.",
    Group = "Core",
    Icon = "icon-plug")]
public sealed class McpTrigger : TriggerBase<McpTriggerSettings, McpTriggerOutput>, ISupportsManualRun
{
    public const string WellKnownAlias = "umbracoAutomate.mcp";

    public McpTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Parses the saved <see cref="McpTriggerSettings.TestArguments"/> the same way an incoming
    /// MCP tool call's arguments would be bound, so a step sees identical data whether it was
    /// exercised on demand or by a real agent. The bearer-secret check has no bearing here — the
    /// point is exercising the steps, not the endpoint's authentication.
    /// </remarks>
    public ManualRunOutput CreateManualRunOutput(object? settings)
    {
        var typedSettings = settings as McpTriggerSettings;

        var arguments = ParseTestArguments(typedSettings?.TestArguments);
        if (arguments is null)
        {
            return ManualRunOutput.Invalid(
                "The MCP trigger's test arguments are not a JSON object. Fix them in the trigger's settings.");
        }

        var output = new McpTriggerOutput { Arguments = arguments };

        return ManualRunOutput.From(
            JsonOptions.DeserializeToUnwrappedDictionary(JsonSerializer.Serialize(output, JsonOptions.Default)));
    }

    /// <summary>
    /// Parses the saved test arguments as a JSON object. Returns <c>null</c> when the text is
    /// present but isn't a JSON object, so the run is refused rather than started with arguments
    /// the author didn't mean. Blank text is treated as "no arguments".
    /// </summary>
    private static Dictionary<string, object?>? ParseTestArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonOptions.DeserializeToUnwrappedDictionary(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
