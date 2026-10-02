using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Default <see cref="IAutomationSettingsProtector"/>. Delegates the per-field work to
/// <see cref="IEditableModelSerializer"/> and supplies it with the right schema for each part
/// of the automation.
/// </summary>
internal sealed class AutomationSettingsProtector : IAutomationSettingsProtector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IEditableModelSerializer _serializer;
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly WebhookAuthenticatorCollection _webhookAuthenticators;
    private readonly NotificationChannelCollection _notificationChannels;

    public AutomationSettingsProtector(
        IEditableModelSerializer serializer,
        ActionCollection actions,
        TriggerCollection triggers,
        WebhookAuthenticatorCollection webhookAuthenticators,
        NotificationChannelCollection notificationChannels)
    {
        _serializer = serializer;
        _actions = actions;
        _triggers = triggers;
        _webhookAuthenticators = webhookAuthenticators;
        _notificationChannels = notificationChannels;
    }

    /// <inheritdoc />
    public TriggerConfiguration? ProtectTrigger(TriggerConfiguration? trigger)
    {
        if (trigger is null || trigger.Settings.Count == 0)
        {
            return trigger;
        }

        var schema = GetSchema(SensitiveSettingsOwner.Trigger, trigger.TriggerAlias);
        var encryptedSettings = EncryptSettings(trigger.Settings, schema);

        // Webhook triggers carry a dynamic per-strategy sub-schema under Authenticator.Settings.
        // The top-level schema doesn't know which authenticator's fields are sensitive,
        // so we look up the selected strategy and encrypt its settings inline.
        if (trigger.TriggerAlias == WebhookTrigger.WellKnownAlias)
        {
            encryptedSettings = EncryptWebhookAuthenticatorSettings(encryptedSettings);
        }

        if (ReferenceEquals(encryptedSettings, trigger.Settings))
        {
            return trigger;
        }

        return new TriggerConfiguration
        {
            TriggerAlias = trigger.TriggerAlias,
            Settings = encryptedSettings,
        };
    }

    /// <inheritdoc />
    public StepConfiguration ProtectStep(StepConfiguration step)
    {
        if (step.Settings.Count == 0)
        {
            return step;
        }

        var schema = GetSchema(SensitiveSettingsOwner.Action, step.ActionAlias);
        var encryptedSettings = EncryptSettings(step.Settings, schema);

        return ReferenceEquals(encryptedSettings, step.Settings)
            ? step
            : WithSettings(step, encryptedSettings);
    }

    /// <inheritdoc />
    public AutomationNotificationSettings? ProtectNotificationSettings(AutomationNotificationSettings? notificationSettings)
    {
        if (notificationSettings is null || notificationSettings.Channels.Count == 0)
        {
            return notificationSettings;
        }

        return new AutomationNotificationSettings
        {
            Channels = notificationSettings.Channels
                .Select(channel => WithSettings(
                    channel,
                    EncryptSettings(channel.Settings, GetSchema(SensitiveSettingsOwner.NotificationChannel, channel.ChannelAlias))))
                .ToList(),
        };
    }

    /// <inheritdoc />
    public TriggerConfiguration? UnprotectTrigger(TriggerConfiguration? trigger)
    {
        if (trigger is null || trigger.Settings.Count == 0)
        {
            return trigger;
        }

        // Decryption walks nested objects, so this also covers the webhook authenticator's settings.
        return new TriggerConfiguration
        {
            TriggerAlias = trigger.TriggerAlias,
            Settings = DecryptSettings(trigger.Settings),
        };
    }

    /// <inheritdoc />
    public StepConfiguration UnprotectStep(StepConfiguration step)
        => step.Settings.Count == 0
            ? step
            : WithSettings(step, DecryptSettings(step.Settings));

    /// <inheritdoc />
    public AutomationNotificationSettings? UnprotectNotificationSettings(AutomationNotificationSettings? notificationSettings)
    {
        if (notificationSettings is null || notificationSettings.Channels.Count == 0)
        {
            return notificationSettings;
        }

        return new AutomationNotificationSettings
        {
            Channels = notificationSettings.Channels
                .Select(channel => channel.Settings.Count == 0
                    ? channel
                    : WithSettings(channel, DecryptSettings(channel.Settings)))
                .ToList(),
        };
    }

    /// <inheritdoc />
    public void ProtectAutomationSettings(Automation automation)
    {
        automation.Trigger = ProtectTrigger(automation.Trigger);
        automation.Steps = automation.Steps.Select(ProtectStep).ToList();
        automation.NotificationSettings = ProtectNotificationSettings(automation.NotificationSettings);
    }

    /// <inheritdoc />
    public void UnprotectAutomationSettings(Automation automation)
    {
        automation.Trigger = UnprotectTrigger(automation.Trigger);
        automation.Steps = automation.Steps.Select(UnprotectStep).ToList();
        automation.NotificationSettings = UnprotectNotificationSettings(automation.NotificationSettings);
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetSensitiveKeys(SensitiveSettingsOwner owner, string alias)
        => GetSchema(owner, alias)?.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private EditableModelSchema? GetSchema(SensitiveSettingsOwner owner, string alias)
        => owner switch
        {
            SensitiveSettingsOwner.Trigger => _triggers.FirstOrDefault(t => t.Alias == alias)?.GetSettingsSchema(),
            SensitiveSettingsOwner.Action => _actions.FirstOrDefault(a => a.Alias == alias)?.GetSettingsSchema(),
            SensitiveSettingsOwner.WebhookAuthenticator => _webhookAuthenticators
                .FirstOrDefault(a => string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase))
                ?.GetSettingsSchema(),
            SensitiveSettingsOwner.NotificationChannel => _notificationChannels.GetByAlias(alias)?.GetSettingsSchema(),
            _ => null,
        };

    private Dictionary<string, object?> EncryptWebhookAuthenticatorSettings(Dictionary<string, object?> triggerSettings)
    {
        if (!triggerSettings.TryGetValue("authenticator", out var authValue) || authValue is null)
        {
            return triggerSettings;
        }

        // Normalize to Dictionary regardless of whether it came in as a dict, JsonElement, or POCO.
        var authDict = SettingsDictionary.From(authValue);
        if (authDict is null)
        {
            return triggerSettings;
        }

        var alias = authDict.TryGetValue("alias", out var aliasValue) ? AsString(aliasValue) : null;
        if (string.IsNullOrEmpty(alias))
        {
            return triggerSettings;
        }

        var authSchema = GetSchema(SensitiveSettingsOwner.WebhookAuthenticator, alias);
        if (authSchema is null || !authSchema.Fields.Any(f => f.IsSensitive))
        {
            return triggerSettings;
        }

        if (!authDict.TryGetValue("settings", out var settingsValue) || settingsValue is null)
        {
            return triggerSettings;
        }

        var settingsDict = SettingsDictionary.From(settingsValue);
        if (settingsDict is null)
        {
            return triggerSettings;
        }

        var encryptedStrategySettings = EncryptSettings(settingsDict, authSchema);
        if (ReferenceEquals(encryptedStrategySettings, settingsDict))
        {
            return triggerSettings;
        }

        var newAuth = new Dictionary<string, object?>(authDict, StringComparer.OrdinalIgnoreCase)
        {
            ["settings"] = encryptedStrategySettings,
        };
        return new Dictionary<string, object?>(triggerSettings, StringComparer.OrdinalIgnoreCase)
        {
            ["authenticator"] = newAuth,
        };
    }

    /// <summary>
    /// Encrypts sensitive values in a settings dictionary by serializing through
    /// <see cref="IEditableModelSerializer"/> and deserializing back to a dictionary.
    /// Returns the original dictionary if no encryption was needed.
    /// </summary>
    private Dictionary<string, object?> EncryptSettings(
        Dictionary<string, object?> settings,
        EditableModelSchema? schema)
    {
        if (schema is null || !schema.Fields.Any(f => f.IsSensitive))
        {
            return settings;
        }

        var encryptedJson = _serializer.Serialize(settings, schema);
        if (encryptedJson is null)
        {
            return settings;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(encryptedJson, JsonOptions) ?? settings;
    }

    private Dictionary<string, object?> DecryptSettings(Dictionary<string, object?> settings)
        => _serializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(settings, JsonOptions))
            ?? settings;

    // Settings bound from an API request arrive as JsonElement, not string. Reading only
    // `as string` here would skip the lookup and leave the authenticator secret unencrypted.
    private static string? AsString(object? value) => value switch
    {
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    private static StepConfiguration WithSettings(StepConfiguration step, Dictionary<string, object?> settings)
        => new()
        {
            Id = step.Id,
            ActionAlias = step.ActionAlias,
            Name = step.Name,
            Alias = step.Alias,
            ConnectionId = step.ConnectionId,
            Settings = settings,
            InputMappings = step.InputMappings,
            Position = step.Position,
            ErrorBehavior = step.ErrorBehavior,
            RetryInterval = step.RetryInterval,
            MaxRetries = step.MaxRetries,
        };

    private static ChannelConfiguration WithSettings(ChannelConfiguration channel, Dictionary<string, object?> settings)
        => ReferenceEquals(settings, channel.Settings)
            ? channel
            : new ChannelConfiguration
            {
                ChannelAlias = channel.ChannelAlias,
                Settings = settings,
                IsEnabled = channel.IsEnabled,
                NotifyOn = channel.NotifyOn,
            };
}
