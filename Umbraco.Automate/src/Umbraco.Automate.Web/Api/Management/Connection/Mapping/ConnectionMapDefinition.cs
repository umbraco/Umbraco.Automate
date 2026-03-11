using Umbraco.Automate.Web.Api.Management.Connection.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Connection.Mapping;

/// <summary>
/// Map definitions for Connection models.
/// </summary>
public class ConnectionMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        // Response mappings (domain -> response)
        mapper.Define<Core.Connections.Connection, ConnectionResponseModel>((_, _) => new ConnectionResponseModel(), MapToResponse);
        mapper.Define<Core.Connections.Connection, ConnectionItemResponseModel>((_, _) => new ConnectionItemResponseModel(), MapToItemResponse);

        // Request mappings (request -> domain)
        mapper.Define<CreateConnectionRequestModel, Core.Connections.Connection>(CreateConnectionFactory, MapFromCreateRequest);
        mapper.Define<UpdateConnectionRequestModel, Core.Connections.Connection>((_, _) => new Core.Connections.Connection
        {
            Alias = string.Empty,
            Name = string.Empty,
            Type = string.Empty,
        }, MapFromUpdateRequest);
    }

    private static Core.Connections.Connection CreateConnectionFactory(CreateConnectionRequestModel source, MapperContext context)
    {
        return new Core.Connections.Connection
        {
            Alias = source.Alias,
            Name = source.Name,
            Type = source.Type,
        };
    }

    // Umbraco.Code.MapAll -Alias -Name -Type -Id -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromCreateRequest(CreateConnectionRequestModel source, Core.Connections.Connection target, MapperContext context)
    {
        target.Settings = source.Settings;
    }

    // Umbraco.Code.MapAll -Id -Version -DateCreated -DateModified -CreatedByUserId -ModifiedByUserId
    private static void MapFromUpdateRequest(UpdateConnectionRequestModel source, Core.Connections.Connection target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Type = source.Type;
        target.Settings = source.Settings;
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(Core.Connections.Connection source, ConnectionResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Type = source.Type;
        target.Settings = source.Settings;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
    }

    // Umbraco.Code.MapAll
    private static void MapToItemResponse(Core.Connections.Connection source, ConnectionItemResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Type = source.Type;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.DateModified = source.DateModified;
    }
}
