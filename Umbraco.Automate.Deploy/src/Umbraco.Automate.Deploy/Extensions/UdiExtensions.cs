using Umbraco.Automate.Core.Models;
using Umbraco.Cms.Core;

namespace Umbraco.Automate.Deploy.Extensions;

/// <summary>
/// Extension methods for creating UDIs from Automate entities.
/// </summary>
internal static class UdiExtensions
{
    /// <summary>
    /// Creates a GuidUdi for an Automate entity with the specified entity type.
    /// </summary>
    public static GuidUdi GetUdi(this IAutomateEntity entity, string udiEntityType)
        => new(udiEntityType, entity.Id);
}
