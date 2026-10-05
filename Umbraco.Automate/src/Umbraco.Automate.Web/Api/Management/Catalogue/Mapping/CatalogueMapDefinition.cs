using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.StepTypes;
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
    private readonly ILogger<CatalogueMapDefinition> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueMapDefinition"/> class without logging.
    /// </summary>
    [Obsolete("Use the constructor taking a logger. This constructor will be removed in a future major version.")]
    public CatalogueMapDefinition()
        : this(NullLogger<CatalogueMapDefinition>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogueMapDefinition"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [ActivatorUtilitiesConstructor]
    public CatalogueMapDefinition(ILogger<CatalogueMapDefinition> logger) => _logger = logger;

    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<StepOutcome, StepOutcomeResponseModel>((_, _) => new StepOutcomeResponseModel(), MapToStepOutcome);
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
    private void MapToActionItem(IAction source, ActionItemResponseModel target, MapperContext context)
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
        target.Outcomes = MapOutcomes(source, context);
        target.HasDynamicOutcomes = source.HasDynamicOutcomes;
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
        // Triggers start an automation rather than finish with a result, so they never expose outcomes.
        target.Outcomes = [];
        target.HasDynamicOutcomes = false;
        target.Type = "trigger";
    }

    // Umbraco.Code.MapAll
    private void MapToControlFlowItem(IControlFlow source, ControlFlowItemResponseModel target, MapperContext context)
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
        target.Outcomes = MapOutcomes(source, context);
        target.HasDynamicOutcomes = source.HasDynamicOutcomes;
        target.Type = "controlFlow";
    }

    // Umbraco.Code.MapAll
    private static void MapToStepOutcome(StepOutcome source, StepOutcomeResponseModel target, MapperContext context)
    {
        target.Key = source.Key;
        target.Label = source.Label;
        target.IsDefault = source.IsDefault;
    }

    // Static outcomes only: dynamic ones depend on a step's settings and are resolved per step.
    // A third-party step type that throws or returns junk must not take the whole catalogue down.
    private List<StepOutcomeResponseModel> MapOutcomes(IStepType source, MapperContext context)
    {
        try
        {
            return StepOutcomeResponseMapping.MapOutcomes(outcome => context.Map<StepOutcomeResponseModel>(outcome)!, _logger, source.Alias, source.GetOutcomes());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Step type '{Alias}' threw while listing its outcomes; exposing none.", source.Alias);
            return [];
        }
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
