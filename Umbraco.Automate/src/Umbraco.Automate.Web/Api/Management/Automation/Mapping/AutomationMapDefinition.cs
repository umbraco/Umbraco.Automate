using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Automation.Mapping;

/// <summary>
/// Map definitions for Automation models.
/// </summary>
public class AutomationMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        // Response mappings (domain -> response)
        mapper.Define<Core.Automations.Automation, AutomationResponseModel>((_, _) => new AutomationResponseModel(), MapToResponse);
        mapper.Define<Core.Automations.Automation, AutomationItemResponseModel>((_, _) => new AutomationItemResponseModel(), MapToItemResponse);

        // Request mappings (request -> domain)
        mapper.Define<CreateAutomationRequestModel, Core.Automations.Automation>(CreateAutomationFactory, MapFromCreateRequest);
        mapper.Define<UpdateAutomationRequestModel, Core.Automations.Automation>((_, _) => new Core.Automations.Automation
        {
            Alias = string.Empty,
            Name = string.Empty,
        }, MapFromUpdateRequest);
    }

    private static Core.Automations.Automation CreateAutomationFactory(CreateAutomationRequestModel source, MapperContext context)
    {
        return new Core.Automations.Automation
        {
            Alias = source.Alias,
            Name = source.Name,
        };
    }

    // Umbraco.Code.MapAll -Alias -Name -Id -WorkspaceId -Status -PublishedVersion -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromCreateRequest(CreateAutomationRequestModel source, Core.Automations.Automation target, MapperContext context)
    {
        target.Description = source.Description;
        target.IsEnabled = source.IsEnabled;
        target.GroupId = source.GroupId;
        target.Trigger = source.Trigger;
        target.Steps = source.Steps;
        target.Connections = source.Connections;
        target.CanvasState = source.CanvasState;
        target.NotificationSettings = source.NotificationSettings;
    }

    // Umbraco.Code.MapAll -Id -WorkspaceId -Status -PublishedVersion -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromUpdateRequest(UpdateAutomationRequestModel source, Core.Automations.Automation target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.IsEnabled = source.IsEnabled;
        target.GroupId = source.GroupId;
        target.Trigger = source.Trigger;
        target.Steps = source.Steps;
        target.Connections = source.Connections;
        target.CanvasState = source.CanvasState;
        target.NotificationSettings = source.NotificationSettings;
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(Core.Automations.Automation source, AutomationResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.IsEnabled = source.IsEnabled;
        target.Status = source.Status;
        target.PublishedVersion = source.PublishedVersion;
        target.WorkspaceId = source.WorkspaceId;
        target.GroupId = source.GroupId;
        target.Trigger = source.Trigger;
        target.Steps = source.Steps;
        target.Connections = source.Connections;
        target.CanvasState = source.CanvasState;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
        target.NotificationSettings = source.NotificationSettings;
    }

    // Umbraco.Code.MapAll -NotificationSettings
    private static void MapToItemResponse(Core.Automations.Automation source, AutomationItemResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.IsEnabled = source.IsEnabled;
        target.GroupId = source.GroupId;
        target.Status = source.Status;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
    }
}
