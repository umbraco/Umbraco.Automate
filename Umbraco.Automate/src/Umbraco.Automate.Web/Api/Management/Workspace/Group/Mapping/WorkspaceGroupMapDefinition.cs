using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Mapping;

/// <summary>
/// Map definitions for WorkspaceGroup models.
/// </summary>
public class WorkspaceGroupMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<WorkspaceGroup, WorkspaceGroupResponseModel>((_, _) => new WorkspaceGroupResponseModel(), MapToResponse);
        mapper.Define<CreateWorkspaceGroupRequestModel, WorkspaceGroup>(CreateGroupFactory, MapFromCreateRequest);
        mapper.Define<UpdateWorkspaceGroupRequestModel, WorkspaceGroup>((_, _) => new WorkspaceGroup { Name = string.Empty }, MapFromUpdateRequest);
    }

    private static WorkspaceGroup CreateGroupFactory(CreateWorkspaceGroupRequestModel source, MapperContext context)
    {
        return new WorkspaceGroup { Name = source.Name };
    }

    // Umbraco.Code.MapAll -Name -Id -WorkspaceId -DateCreated
    private static void MapFromCreateRequest(CreateWorkspaceGroupRequestModel source, WorkspaceGroup target, MapperContext context)
    {
        target.ParentId = source.ParentId;
    }

    // Umbraco.Code.MapAll -Id -WorkspaceId -DateCreated
    private static void MapFromUpdateRequest(UpdateWorkspaceGroupRequestModel source, WorkspaceGroup target, MapperContext context)
    {
        target.Name = source.Name;
        target.ParentId = source.ParentId;
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(WorkspaceGroup source, WorkspaceGroupResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Name = source.Name;
        target.ParentId = source.ParentId;
        target.DateCreated = source.DateCreated;
    }
}
