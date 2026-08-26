using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
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
        mapper.Define<IConnectionType, ConnectionTypeItemResponseModel>(
            (_, _) => new ConnectionTypeItemResponseModel(), MapToConnectionTypeItem);
        mapper.Define<IWebhookAuthenticator, WebhookAuthenticatorItemResponseModel>(
            (_, _) => new WebhookAuthenticatorItemResponseModel(), MapToWebhookAuthenticatorItem);
    }

    // Umbraco.Code.MapAll
    private static void MapToActionItem(IAction source, ActionItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.ConnectionTypeAlias = source.ConnectionTypeAlias;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = OutputSchemaSerializer.Serialize(source.GetOutputSchema());
        target.HasDynamicOutputSchema = source.HasDynamicOutputSchema;
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
        target.ConnectionTypeAlias = source.ConnectionTypeAlias;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = OutputSchemaSerializer.Serialize(source.GetOutputSchema());
        target.HasDynamicOutputSchema = source.HasDynamicOutputSchema;
        // Opting into the capability is what makes "Run now" available for the trigger, so the
        // backoffice can ask the catalogue rather than keep its own list of runnable aliases.
        target.SupportsManualRun = source is ISupportsManualRun;
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
        target.ConnectionTypeAlias = source.ConnectionTypeAlias;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputSchema = OutputSchemaSerializer.Serialize(source.GetOutputSchema());
        target.HasDynamicOutputSchema = source.HasDynamicOutputSchema;
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

    // Umbraco.Code.MapAll
    private static void MapToConnectionTypeItem(IConnectionType source, ConnectionTypeItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
    }

    // Umbraco.Code.MapAll
    private static void MapToWebhookAuthenticatorItem(IWebhookAuthenticator source, WebhookAuthenticatorItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.SettingsSchema = source.GetSettingsSchema();
    }
}
