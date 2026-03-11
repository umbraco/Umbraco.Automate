using Umbraco.Automate.Web.Api.Management.Workspace.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Mapping;

/// <summary>
/// Map definitions for Workspace models.
/// </summary>
public class WorkspaceMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        // Response mappings (domain -> response)
        mapper.Define<Core.Workspaces.Workspace, WorkspaceResponseModel>((_, _) => new WorkspaceResponseModel(), MapToResponse);
        mapper.Define<Core.Workspaces.Workspace, WorkspaceItemResponseModel>((_, _) => new WorkspaceItemResponseModel(), MapToItemResponse);

        // Request mappings (request -> domain)
        mapper.Define<CreateWorkspaceRequestModel, Core.Workspaces.Workspace>(CreateWorkspaceFactory, MapFromCreateRequest);
        mapper.Define<UpdateWorkspaceRequestModel, Core.Workspaces.Workspace>((_, _) => new Core.Workspaces.Workspace
        {
            Alias = string.Empty,
            Name = string.Empty,
        }, MapFromUpdateRequest);
    }

    private static Core.Workspaces.Workspace CreateWorkspaceFactory(CreateWorkspaceRequestModel source, MapperContext context)
    {
        return new Core.Workspaces.Workspace
        {
            Alias = source.Alias,
            Name = source.Name,
        };
    }

    // Umbraco.Code.MapAll -Alias -Name -Id -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromCreateRequest(CreateWorkspaceRequestModel source, Core.Workspaces.Workspace target, MapperContext context)
    {
        target.ServiceAccountKey = source.ServiceAccountKey;
        target.UserGroups = source.UserGroups;
        target.Connections = source.Connections;
    }

    // Umbraco.Code.MapAll -Id -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromUpdateRequest(UpdateWorkspaceRequestModel source, Core.Workspaces.Workspace target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.ServiceAccountKey = source.ServiceAccountKey;
        target.UserGroups = source.UserGroups;
        target.Connections = source.Connections;
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(Core.Workspaces.Workspace source, WorkspaceResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.ServiceAccountKey = source.ServiceAccountKey;
        target.UserGroups = source.UserGroups;
        target.Connections = source.Connections;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
    }

    // Umbraco.Code.MapAll
    private static void MapToItemResponse(Core.Workspaces.Workspace source, WorkspaceItemResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.ServiceAccountKey = source.ServiceAccountKey;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
    }
}
