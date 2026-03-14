using System.Text.Json;
using Json.Schema;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;

/// <summary>
/// Map definitions for Catalogue models (actions, triggers, control flows, notification channels).
/// </summary>
public class CatalogueMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<IAction, ActionItemResponseModel>((_, _) => new ActionItemResponseModel(), MapToActionItem);
        mapper.Define<ITrigger, TriggerItemResponseModel>((_, _) => new TriggerItemResponseModel(), MapToTriggerItem);
        mapper.Define<IControlFlow, ControlFlowItemResponseModel>((_, _) => new ControlFlowItemResponseModel(), MapToControlFlowItem);
        mapper.Define<INotificationChannel, NotificationChannelItemResponseModel>(
            (_, _) => new NotificationChannelItemResponseModel(), MapToNotificationChannelItem);
    }

    // Umbraco.Code.MapAll
    private static void MapToActionItem(IAction source, ActionItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = SerializeOutputSchema(source.GetOutputSchema());
        target.Type = "action";
    }

    // Umbraco.Code.MapAll
    private static void MapToTriggerItem(ITrigger source, TriggerItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = SerializeOutputSchema(source.GetOutputSchema());
        target.Type = "trigger";
    }

    // Umbraco.Code.MapAll
    private static void MapToControlFlowItem(IControlFlow source, ControlFlowItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = SerializeOutputSchema(source.GetOutputSchema());
        target.Type = "controlFlow";
    }

    // Umbraco.Code.MapAll
    private static void MapToNotificationChannelItem(INotificationChannel source, NotificationChannelItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
    }

    private static readonly JsonSerializerOptions JsonSchemaSerializerOptions = new()
    {
        Converters = { new Json.Schema.SchemaJsonConverter() },
    };

    private static Dictionary<string, object?>? SerializeOutputSchema(JsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        // Serialize via JsonSchema's own converter to get a proper JSON Schema document,
        // then deserialize into a dictionary for clean API serialization and Swagger rendering.
        var json = JsonSerializer.Serialize(schema, JsonSchemaSerializerOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }
}
