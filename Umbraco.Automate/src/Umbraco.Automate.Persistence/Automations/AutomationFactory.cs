using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// Maps between <see cref="Automation"/> domain model and <see cref="AutomationEntity"/> EF entity.
/// Delegates encryption/decryption of sensitive settings to <see cref="IEditableModelSerializer"/>.
/// </summary>
internal sealed class AutomationFactory
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

    public AutomationFactory(
        IEditableModelSerializer serializer,
        ActionCollection actions,
        TriggerCollection triggers,
        WebhookAuthenticatorCollection webhookAuthenticators)
    {
        _serializer = serializer;
        _actions = actions;
        _triggers = triggers;
        _webhookAuthenticators = webhookAuthenticators;
    }

    public Automation BuildDomain(AutomationEntity entity)
    {
        AutomationDefinitionDto? definition = null;
        if (!string.IsNullOrEmpty(entity.Definition))
        {
            definition = _serializer.Deserialize<AutomationDefinitionDto>(entity.Definition);
        }

        return new Automation
        {
            Id = entity.Id,
            Alias = entity.Alias,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            Status = (AutomationStatus)entity.Status,
            PublishedVersion = entity.PublishedVersion,
            WorkspaceId = entity.WorkspaceId,
            GroupId = entity.GroupId,
            Trigger = definition?.Trigger,
            Steps = definition?.Steps ?? [],
            Connections = definition?.Connections ?? [],
            CanvasState = definition?.CanvasState,
            NotificationSettings = definition?.NotificationSettings,
            Version = entity.Version,
            DateCreated = entity.DateCreated,
            DateModified = entity.DateModified,
            CreatedByUserId = entity.CreatedByUserId,
            ModifiedByUserId = entity.ModifiedByUserId,
        };
    }

    public AutomationEntity BuildEntity(Automation automation)
    {
        return new AutomationEntity
        {
            Id = automation.Id,
            Alias = automation.Alias,
            Name = automation.Name,
            Description = automation.Description,
            IsEnabled = automation.IsEnabled,
            Status = (int)automation.Status,
            PublishedVersion = automation.PublishedVersion,
            WorkspaceId = automation.WorkspaceId,
            GroupId = automation.GroupId,
            Definition = SerializeDefinition(automation),
            Version = automation.Version,
            DateCreated = automation.DateCreated,
            DateModified = automation.DateModified,
            CreatedByUserId = automation.CreatedByUserId,
            ModifiedByUserId = automation.ModifiedByUserId,
        };
    }

    public void UpdateEntity(AutomationEntity entity, Automation automation)
    {
        entity.Alias = automation.Alias;
        entity.Name = automation.Name;
        entity.Description = automation.Description;
        entity.IsEnabled = automation.IsEnabled;
        entity.Status = (int)automation.Status;
        entity.PublishedVersion = automation.PublishedVersion;
        entity.WorkspaceId = automation.WorkspaceId;
        entity.GroupId = automation.GroupId;
        entity.Definition = SerializeDefinition(automation);
        entity.Version = automation.Version;
        entity.DateModified = automation.DateModified;
        entity.ModifiedByUserId = automation.ModifiedByUserId;
        // DateCreated and CreatedByUserId intentionally not updated
    }

    public void UpdateMetadata(AutomationEntity entity, Automation automation)
    {
        entity.Status = (int)automation.Status;
        entity.IsEnabled = automation.IsEnabled;
        entity.PublishedVersion = automation.PublishedVersion;
        entity.GroupId = automation.GroupId;
        entity.Version = automation.Version;
        entity.DateModified = automation.DateModified;
        entity.ModifiedByUserId = automation.ModifiedByUserId;
    }

    private string? SerializeDefinition(Automation automation)
    {
        if (automation.Trigger is null && automation.Steps.Count == 0 && automation.Connections.Count == 0)
        {
            return null;
        }

        // Encrypt sensitive settings per step/trigger before serializing the definition.
        var trigger = EncryptTriggerSettings(automation.Trigger);
        var steps = automation.Steps.Select(EncryptStepSettings).ToList();

        var dto = new AutomationDefinitionDto
        {
            Trigger = trigger,
            Steps = steps,
            Connections = automation.Connections,
            CanvasState = automation.CanvasState,
            NotificationSettings = automation.NotificationSettings,
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private TriggerConfiguration? EncryptTriggerSettings(TriggerConfiguration? trigger)
    {
        if (trigger is null || trigger.Settings.Count == 0)
        {
            return trigger;
        }

        var schema = GetTriggerSchema(trigger.TriggerAlias);
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

    private Dictionary<string, object?> EncryptWebhookAuthenticatorSettings(Dictionary<string, object?> triggerSettings)
    {
        if (!triggerSettings.TryGetValue("authenticator", out var authValue) || authValue is null)
        {
            return triggerSettings;
        }

        // Normalize to Dictionary regardless of whether it came in as a dict, JsonElement, or POCO.
        var authDict = CoerceToDictionary(authValue);
        if (authDict is null)
        {
            return triggerSettings;
        }

        var alias = authDict.TryGetValue("alias", out var aliasValue) ? aliasValue as string : null;
        if (string.IsNullOrEmpty(alias))
        {
            return triggerSettings;
        }

        var authenticator = _webhookAuthenticators.FirstOrDefault(a =>
            string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase));
        var authSchema = authenticator?.GetSettingsSchema();
        if (authSchema is null || !authSchema.Fields.Any(f => f.IsSensitive))
        {
            return triggerSettings;
        }

        if (!authDict.TryGetValue("settings", out var settingsValue) || settingsValue is null)
        {
            return triggerSettings;
        }

        var settingsDict = CoerceToDictionary(settingsValue);
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

    private static Dictionary<string, object?>? CoerceToDictionary(object value)
    {
        return value switch
        {
            Dictionary<string, object?> dict => new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase),
            JsonElement { ValueKind: JsonValueKind.Object } element
                => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions),
            _ => JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    JsonSerializer.Serialize(value, JsonOptions), JsonOptions),
        };
    }

    private StepConfiguration EncryptStepSettings(StepConfiguration step)
    {
        if (step.Settings.Count == 0)
        {
            return step;
        }

        var schema = GetActionSchema(step.ActionAlias);
        var encryptedSettings = EncryptSettings(step.Settings, schema);

        if (encryptedSettings == step.Settings)
        {
            return step;
        }

        return new StepConfiguration
        {
            Id = step.Id,
            ActionAlias = step.ActionAlias,
            Name = step.Name,
            Alias = step.Alias,
            ConnectionId = step.ConnectionId,
            Settings = encryptedSettings,
            InputMappings = step.InputMappings,
            Position = step.Position,
            ErrorBehavior = step.ErrorBehavior,
            RetryInterval = step.RetryInterval,
            MaxRetries = step.MaxRetries,
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

    private EditableModelSchema? GetActionSchema(string actionAlias)
    {
        var action = _actions.FirstOrDefault(a => a.Alias == actionAlias);
        return action?.GetSettingsSchema();
    }

    private EditableModelSchema? GetTriggerSchema(string triggerAlias)
    {
        var trigger = _triggers.FirstOrDefault(t => t.Alias == triggerAlias);
        return trigger?.GetSettingsSchema();
    }
}
