using System.Text.Json;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// Maps between <see cref="Automation"/> domain model and <see cref="AutomationEntity"/> EF entity.
/// Encrypts sensitive settings through <see cref="IAutomationSettingsProtector"/> and decrypts
/// the stored definition through <see cref="IEditableModelSerializer"/>.
/// </summary>
internal sealed class AutomationFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IEditableModelSerializer _serializer;
    private readonly IAutomationSettingsProtector _settingsProtector;

    public AutomationFactory(
        IEditableModelSerializer serializer,
        IAutomationSettingsProtector settingsProtector)
    {
        _serializer = serializer;
        _settingsProtector = settingsProtector;
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
            Status = (AutomationStatus)entity.Status,
            PublishedVersion = entity.PublishedVersion,
            WorkspaceId = entity.WorkspaceId,
            GroupId = entity.GroupId,
            Trigger = definition?.Trigger,
            Steps = definition?.Steps ?? [],
            Connections = definition?.Connections ?? [],
            CanvasState = definition?.CanvasState,
            // Channels sit in an array, which the definition-level decrypt above does not walk.
            NotificationSettings = _settingsProtector.UnprotectNotificationSettings(definition?.NotificationSettings),
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
        entity.PublishedVersion = automation.PublishedVersion;
        entity.GroupId = automation.GroupId;
        entity.Version = automation.Version;
        entity.DateModified = automation.DateModified;
        entity.ModifiedByUserId = automation.ModifiedByUserId;
    }

    /// <summary>
    /// Encrypts any sensitive value the stored definition still holds in plaintext, leaving all
    /// other columns untouched. Used to repair definitions written before a sensitive field was protected.
    /// </summary>
    /// <remarks>
    /// Works on the stored JSON as-is and never decrypts it. Going through <see cref="BuildDomain"/>
    /// would decrypt everything first, and a value whose owner is no longer registered (an uninstalled
    /// trigger, action or channel) has no schema to re-encrypt it with, so it would be written back in
    /// plaintext. Encrypting is idempotent, so values that are already encrypted are left as they are.
    /// </remarks>
    /// <returns><c>true</c> if the definition changed.</returns>
    public bool ReprotectDefinition(AutomationEntity entity)
    {
        if (string.IsNullOrEmpty(entity.Definition))
        {
            return false;
        }

        var definition = JsonSerializer.Deserialize<AutomationDefinitionDto>(entity.Definition, JsonOptions);
        if (definition is null)
        {
            return false;
        }

        definition.Trigger = _settingsProtector.ProtectTrigger(definition.Trigger);
        definition.Steps = definition.Steps.Select(_settingsProtector.ProtectStep).ToList();
        definition.NotificationSettings = _settingsProtector.ProtectNotificationSettings(definition.NotificationSettings);

        var reprotected = JsonSerializer.Serialize(definition, JsonOptions);
        if (reprotected == entity.Definition)
        {
            return false;
        }

        entity.Definition = reprotected;
        return true;
    }

    private string? SerializeDefinition(Automation automation)
    {
        if (automation.Trigger is null && automation.Steps.Count == 0 && automation.Connections.Count == 0)
        {
            return null;
        }

        // Encrypt sensitive settings per trigger, step and notification channel before serializing the definition.
        var dto = new AutomationDefinitionDto
        {
            Trigger = _settingsProtector.ProtectTrigger(automation.Trigger),
            Steps = automation.Steps.Select(_settingsProtector.ProtectStep).ToList(),
            Connections = automation.Connections,
            CanvasState = automation.CanvasState,
            NotificationSettings = _settingsProtector.ProtectNotificationSettings(automation.NotificationSettings),
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }
}
