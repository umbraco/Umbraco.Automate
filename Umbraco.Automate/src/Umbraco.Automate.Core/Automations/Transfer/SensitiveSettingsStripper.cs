using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;

namespace Umbraco.Automate.Core.Automations.Transfer;

/// <inheritdoc />
internal sealed class SensitiveSettingsStripper : ISensitiveSettingsStripper
{
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly ControlFlowCollection _controlFlows;
    private readonly ConnectionTypeCollection _connectionTypes;
    private readonly WebhookAuthenticatorCollection _webhookAuthenticators;
    private readonly NotificationChannelCollection _notificationChannels;

    public SensitiveSettingsStripper(
        ActionCollection actions,
        TriggerCollection triggers,
        ControlFlowCollection controlFlows,
        ConnectionTypeCollection connectionTypes,
        WebhookAuthenticatorCollection webhookAuthenticators,
        NotificationChannelCollection notificationChannels)
    {
        _actions = actions;
        _triggers = triggers;
        _controlFlows = controlFlows;
        _connectionTypes = connectionTypes;
        _webhookAuthenticators = webhookAuthenticators;
        _notificationChannels = notificationChannels;
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

        // Webhook triggers nest the selected authenticator's settings, whose sensitive fields come
        // from that strategy's schema rather than the trigger's.
        if (trigger.TriggerAlias == WebhookTrigger.WellKnownAlias)
        {
            stripped = StripWebhookAuthenticatorSettings(stripped);
        }

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

    /// <inheritdoc />
    public Dictionary<string, object?> StripConnectionSettings(string connectionTypeAlias, Dictionary<string, object?> settings)
    {
        var connectionType = _connectionTypes.GetByAlias(connectionTypeAlias);
        return StripSensitiveSettings(settings, connectionType?.GetSettingsSchema());
    }

    /// <inheritdoc />
    public AutomationNotificationSettings? StripNotificationSettings(AutomationNotificationSettings? notificationSettings)
    {
        if (notificationSettings is null || notificationSettings.Channels.Count == 0)
        {
            return notificationSettings;
        }

        return new AutomationNotificationSettings
        {
            Channels = notificationSettings.Channels.Select(StripChannel).ToList(),
        };
    }

    private ChannelConfiguration StripChannel(ChannelConfiguration channel)
    {
        var schema = _notificationChannels.GetByAlias(channel.ChannelAlias)?.GetSettingsSchema();
        var stripped = StripSensitiveSettings(channel.Settings, schema);

        if (ReferenceEquals(stripped, channel.Settings))
        {
            return channel;
        }

        return new ChannelConfiguration
        {
            ChannelAlias = channel.ChannelAlias,
            Settings = stripped,
            IsEnabled = channel.IsEnabled,
            NotifyOn = channel.NotifyOn,
        };
    }

    private Dictionary<string, object?> StripWebhookAuthenticatorSettings(Dictionary<string, object?> triggerSettings)
    {
        var authKey = triggerSettings.Keys.FirstOrDefault(k => string.Equals(k, "authenticator", StringComparison.OrdinalIgnoreCase));
        if (authKey is null)
        {
            return triggerSettings;
        }

        var authDict = SettingsDictionary.From(triggerSettings[authKey]);
        if (authDict is null)
        {
            return triggerSettings;
        }

        var alias = authDict.TryGetValue("alias", out var aliasValue) ? AsString(aliasValue) : null;
        if (string.IsNullOrEmpty(alias))
        {
            return triggerSettings;
        }

        var settingsDict = authDict.TryGetValue("settings", out var settingsValue)
            ? SettingsDictionary.From(settingsValue)
            : null;
        if (settingsDict is null)
        {
            return triggerSettings;
        }

        var authSchema = _webhookAuthenticators.GetByAlias(alias)?.GetSettingsSchema();
        var strippedStrategySettings = StripSensitiveSettings(settingsDict, authSchema);
        if (ReferenceEquals(strippedStrategySettings, settingsDict))
        {
            return triggerSettings;
        }

        authDict["settings"] = strippedStrategySettings;
        return new Dictionary<string, object?>(triggerSettings, StringComparer.OrdinalIgnoreCase)
        {
            [authKey] = authDict,
        };
    }

    // Settings bound from an API request arrive as JsonElement, not string.
    private static string? AsString(object? value) => value switch
    {
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

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
