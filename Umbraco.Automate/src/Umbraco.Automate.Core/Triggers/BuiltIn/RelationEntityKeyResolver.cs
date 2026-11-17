using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Resolves the GUID key of a relation's parent or child entity from its integer ID
/// and object type, shared by the relation triggers.
/// </summary>
internal static class RelationEntityKeyResolver
{
    /// <summary>
    /// Resolves the entity key for the given integer ID and object type.
    /// </summary>
    /// <param name="idKeyMap">The ID/key map used for the lookup.</param>
    /// <param name="id">The entity's integer ID.</param>
    /// <param name="objectType">The entity's object type key.</param>
    /// <returns>
    /// The entity key, or <c>null</c> when the object type is unknown or the entity
    /// no longer exists (for example when it was deleted together with the relation).
    /// </returns>
    public static Guid? ResolveEntityKey(IIdKeyMap idKeyMap, int id, Guid objectType)
    {
        var umbracoObjectType = ObjectTypes.GetUmbracoObjectType(objectType);
        if (umbracoObjectType == UmbracoObjectTypes.Unknown)
        {
            return null;
        }

        var attempt = idKeyMap.GetKeyForId(id, umbracoObjectType);
        return attempt.Success ? attempt.Result : null;
    }
}
