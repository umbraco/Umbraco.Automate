using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Group.Mapping;

/// <summary>
/// Map definitions for AutomationGroup models.
/// </summary>
public class AutomationGroupMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<AutomationGroup, AutomationGroupResponseModel>((_, _) => new AutomationGroupResponseModel(), MapToResponse);
        mapper.Define<CreateAutomationGroupRequestModel, AutomationGroup>(CreateGroupFactory, MapFromCreateRequest);
        mapper.Define<UpdateAutomationGroupRequestModel, AutomationGroup>((_, _) => new AutomationGroup { Name = string.Empty }, MapFromUpdateRequest);
    }

    private static AutomationGroup CreateGroupFactory(CreateAutomationGroupRequestModel source, MapperContext context)
    {
        return new AutomationGroup { Name = source.Name };
    }

    // Umbraco.Code.MapAll -Name -Id -WorkspaceId -DateCreated
    private static void MapFromCreateRequest(CreateAutomationGroupRequestModel source, AutomationGroup target, MapperContext context)
    {
        target.ParentId = source.ParentId;
    }

    // Umbraco.Code.MapAll -Id -WorkspaceId -DateCreated
    private static void MapFromUpdateRequest(UpdateAutomationGroupRequestModel source, AutomationGroup target, MapperContext context)
    {
        target.Name = source.Name;
        target.ParentId = source.ParentId;
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(AutomationGroup source, AutomationGroupResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Name = source.Name;
        target.ParentId = source.ParentId;
        target.DateCreated = source.DateCreated;
    }
}
