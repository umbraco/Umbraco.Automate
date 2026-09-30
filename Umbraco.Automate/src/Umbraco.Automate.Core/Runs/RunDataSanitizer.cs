using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Runs;

/// <inheritdoc />
internal sealed class RunDataSanitizer : IRunDataSanitizer
{
    // Relaxed escaping keeps quotes, '<', '&' and non-ASCII readable; the value is rendered as
    // text by the backoffice, never as HTML.
    private static readonly JsonSerializerOptions DisplayOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlows;
    private readonly ILogger<RunDataSanitizer> _logger;

    public RunDataSanitizer(
        ActionCollection actions,
        ControlFlowCollection controlFlows,
        ILogger<RunDataSanitizer> logger)
    {
        _actions = actions;
        _controlFlows = controlFlows;
        _logger = logger;
    }

    /// <inheritdoc />
    public SanitizedRunData SanitizeStepInput(string actionAlias, string? inputJson)
    {
        var stepType = (IStepType?)_actions.GetByAlias(actionAlias) ?? _controlFlows.GetByAlias(actionAlias);
        var sensitiveFieldKeys = SensitiveDataMasker.GetSensitiveFieldKeys(stepType?.GetSettingsSchema());
        return Sanitize(inputJson, sensitiveFieldKeys);
    }

    /// <inheritdoc />
    public SanitizedRunData SanitizeStepOutput(string actionAlias, string? outputJson)
        => Sanitize(outputJson, sensitiveFieldKeys: null);

    /// <inheritdoc />
    public SanitizedRunData SanitizeTriggerData(string? triggerJson)
        => Sanitize(triggerJson, sensitiveFieldKeys: null);

    /// <summary>
    /// Masks, pretty-prints and truncates a stored JSON payload.
    /// </summary>
    internal SanitizedRunData Sanitize(string? json, IReadOnlySet<string>? sensitiveFieldKeys)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SanitizedRunData.Empty;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            // Payloads are always written by our own serializer, so this should not happen. If it
            // does, withhold the value: without parsing it we cannot mask it.
            _logger.LogWarning(ex, "Stored run data could not be parsed as JSON and was withheld from display");
            return SanitizedRunData.Empty;
        }

        if (node is null)
        {
            return SanitizedRunData.Empty;
        }

        SensitiveDataMasker.Mask(node, sensitiveFieldKeys);
        return Truncate(node.ToJsonString(DisplayOptions));
    }

    private static SanitizedRunData Truncate(string value)
    {
        if (value.Length <= IRunDataSanitizer.MaxValueLength)
        {
            return new SanitizedRunData { Value = value };
        }

        var length = IRunDataSanitizer.MaxValueLength;

        // Don't split a surrogate pair.
        if (char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return new SanitizedRunData { Value = value[..length], Truncated = true };
    }
}
