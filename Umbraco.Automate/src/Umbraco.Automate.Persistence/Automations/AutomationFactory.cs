using System.Text.Json;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// Maps between <see cref="Automation"/> domain model and <see cref="AutomationEntity"/> EF entity.
/// </summary>
internal static class AutomationFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static Automation BuildDomain(AutomationEntity entity)
    {
        AutomationDefinitionDto? definition = null;
        if (!string.IsNullOrEmpty(entity.Definition))
        {
            definition = JsonSerializer.Deserialize<AutomationDefinitionDto>(entity.Definition, JsonOptions);
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
            DraftVersion = entity.DraftVersion,
            GroupId = entity.GroupId,
            Trigger = definition?.Trigger,
            Steps = definition?.Steps ?? [],
            Connections = definition?.Connections ?? [],
            CanvasState = definition?.CanvasState,
            Version = entity.Version,
            DateCreated = entity.DateCreated,
            DateModified = entity.DateModified,
            CreatedByUserId = entity.CreatedByUserId,
            ModifiedByUserId = entity.ModifiedByUserId,
        };
    }

    public static AutomationEntity BuildEntity(Automation automation)
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
            DraftVersion = automation.DraftVersion,
            GroupId = automation.GroupId,
            Definition = SerializeDefinition(automation),
            Version = automation.Version,
            DateCreated = automation.DateCreated,
            DateModified = automation.DateModified,
            CreatedByUserId = automation.CreatedByUserId,
            ModifiedByUserId = automation.ModifiedByUserId,
        };
    }

    public static void UpdateEntity(AutomationEntity entity, Automation automation)
    {
        entity.Alias = automation.Alias;
        entity.Name = automation.Name;
        entity.Description = automation.Description;
        entity.IsEnabled = automation.IsEnabled;
        entity.Status = (int)automation.Status;
        entity.PublishedVersion = automation.PublishedVersion;
        entity.DraftVersion = automation.DraftVersion;
        entity.GroupId = automation.GroupId;
        entity.Definition = SerializeDefinition(automation);
        entity.Version = automation.Version;
        entity.DateModified = automation.DateModified;
        entity.ModifiedByUserId = automation.ModifiedByUserId;
        // DateCreated and CreatedByUserId intentionally not updated
    }

    private static string? SerializeDefinition(Automation automation)
    {
        if (automation.Trigger is null && automation.Steps.Count == 0 && automation.Connections.Count == 0)
        {
            return null;
        }

        var dto = new AutomationDefinitionDto
        {
            Trigger = automation.Trigger,
            Steps = automation.Steps,
            Connections = automation.Connections,
            CanvasState = automation.CanvasState,
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }
}
