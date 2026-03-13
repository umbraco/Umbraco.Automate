using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Web.Api.Management.Versioning.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Mapping;

/// <summary>
/// Map definitions for versioning models.
/// </summary>
public class VersioningMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<EntityVersion, EntityVersionResponseModel>((_, _) => new EntityVersionResponseModel(), Map);
    }

    // Umbraco.Code.MapAll
    private static void Map(EntityVersion source, EntityVersionResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.EntityId = source.EntityId;
        target.Version = source.Version;
        target.DateCreated = source.DateCreated;
        target.CreatedByUserId = source.CreatedByUserId;
        target.ChangeDescription = source.ChangeDescription;
    }
}
