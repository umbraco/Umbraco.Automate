using System.Text.Json;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that makes an automation callable as an MCP tool over its own URL.
/// </summary>
[Trigger(WellKnownAlias, "MCP Tool",
    Description = "Fires when an AI agent calls this automation's MCP tool.",
    Group = "Core",
    Icon = "icon-wall-plug")]
public sealed class McpTrigger : TriggerBase<McpTriggerSettings, McpTriggerOutput>, ISupportsManualRun, IPublishValidatableStepType
{
    public const string WellKnownAlias = "umbracoAutomate.mcp";

    public McpTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Binds the saved <see cref="McpTriggerSettings.TestArguments"/> with the same
    /// <see cref="McpToolArgumentBinder"/> an incoming MCP tool call goes through, so a step sees
    /// identical data whether it was exercised on demand or by a real agent, and test arguments a
    /// real call would be refused for are refused here too. The bearer-secret check has no bearing
    /// here — the point is exercising the steps, not the endpoint's authentication.
    /// </remarks>
    public ManualRunOutput CreateManualRunOutput(object? settings)
    {
        var typedSettings = settings as McpTriggerSettings;

        var rawArguments = ParseTestArguments(typedSettings?.TestArguments);
        if (rawArguments is null)
        {
            return ManualRunOutput.Invalid(
                "The MCP trigger's test arguments are not a JSON object. Fix them in the trigger's settings.");
        }

        if (!McpToolArgumentBinder.TryBind(typedSettings?.InputFields ?? [], rawArguments, out var arguments, out var bindError))
        {
            return ManualRunOutput.Invalid(
                $"The MCP trigger's test arguments don't match its input fields: {bindError} Fix them in the trigger's settings.");
        }

        var output = new McpTriggerOutput { Arguments = arguments };

        return ManualRunOutput.From(
            JsonOptions.DeserializeToUnwrappedDictionary(JsonSerializer.Serialize(output, JsonOptions.Default)));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The settings type's own rules already require a tool name in the MCP format. This adds the
    /// checks across input fields that a single field's rules can't express: every field needs a
    /// name, and no two fields can share one, because the tool's argument schema is keyed by name.
    /// </remarks>
    public Task<IReadOnlyList<string>> ValidateSettingsForPublishAsync(
        object? settings,
        Automation automation,
        CancellationToken cancellationToken = default)
    {
        if (settings is not McpTriggerSettings typedSettings)
        {
            return Task.FromResult<IReadOnlyList<string>>(["A tool name is required."]);
        }

        var errors = new List<string>();

        if (typedSettings.InputFields.Any(field => string.IsNullOrWhiteSpace(field.Name)))
        {
            errors.Add("Every input field needs a name.");
        }

        var duplicateNames = typedSettings.InputFields
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            errors.Add($"Input field names must be unique. Used more than once: {string.Join(", ", duplicateNames)}.");
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    /// <summary>
    /// Parses the saved test arguments as a JSON object. Returns <c>null</c> when the text is
    /// present but isn't a JSON object, so the run is refused rather than started with arguments
    /// the author didn't mean. Blank text is treated as "no arguments".
    /// </summary>
    private static Dictionary<string, JsonElement>? ParseTestArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Last one wins on a repeated property name, as System.Text.Json does when it binds a
            // real call's arguments.
            var arguments = new Dictionary<string, JsonElement>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                arguments[property.Name] = property.Value.Clone();
            }

            return arguments;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
