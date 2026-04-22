using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Automations.Transfer;

/// <inheritdoc />
internal sealed class SensitiveSettingsStripper : ISensitiveSettingsStripper
{
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly ControlFlowCollection _controlFlows;

    public SensitiveSettingsStripper(
        ActionCollection actions,
        TriggerCollection triggers,
        ControlFlowCollection controlFlows)
    {
        _actions = actions;
        _triggers = triggers;
        _controlFlows = controlFlows;
    }

    /// <inheritdoc />
    public TriggerConfiguration? StripTrigger(TriggerConfiguration? trigger)
    {
        if (trigger is null || trigger.Settings.Count == 0)
        {
            return trigger;
        }

        var schema = _triggers.GetByAlias(trigger.TriggerAlias)?.GetSettingsSchema();
        var stripped = StripSensitiveSettings(trigger.Settings, schema);

        if (ReferenceEquals(stripped, trigger.Settings))
        {
            return trigger;
        }

        return new TriggerConfiguration
        {
            TriggerAlias = trigger.TriggerAlias,
            Settings = stripped,
        };
    }

    /// <inheritdoc />
    public IList<StepConfiguration> StripSteps(IEnumerable<StepConfiguration> steps)
        => steps.Select(s => new StepConfiguration
        {
            Id = s.Id,
            ActionAlias = s.ActionAlias,
            Name = s.Name,
            Alias = s.Alias,
            ConnectionId = s.ConnectionId,
            Settings = StripStepSettings(s.ActionAlias, s.Settings),
            InputMappings = s.InputMappings,
            Position = s.Position,
            ErrorBehavior = s.ErrorBehavior,
            RetryInterval = s.RetryInterval,
            MaxRetries = s.MaxRetries,
        }).ToList();

    /// <inheritdoc />
    public Dictionary<string, object?> StripStepSettings(string actionAlias, Dictionary<string, object?> settings)
    {
        var stepType = (IStepType?)_actions.GetByAlias(actionAlias) ?? _controlFlows.GetByAlias(actionAlias);
        return StripSensitiveSettings(settings, stepType?.GetSettingsSchema());
    }

    private static Dictionary<string, object?> StripSensitiveSettings(
        Dictionary<string, object?> settings,
        EditableModelSchema? schema)
    {
        if (schema is null)
        {
            return settings;
        }

        var sensitiveFields = schema.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sensitiveFields.Count == 0)
        {
            return settings;
        }

        return settings
            .Where(kvp => !sensitiveFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
