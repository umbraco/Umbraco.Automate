using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Models;

/// <summary>
/// Response model for version history with pagination info.
/// </summary>
public class EntityVersionHistoryResponseModel
{
    /// <summary>
    /// The current version of the entity.
    /// </summary>
    [Required]
    public int CurrentVersion { get; set; }

    /// <summary>
    /// The total number of versions available.
    /// </summary>
    [Required]
    public int TotalVersions { get; set; }

    /// <summary>
    /// The currently published version number, or null if the entity type does not support publishing
    /// or the entity has never been published.
    /// </summary>
    public int? PublishedVersion { get; set; }

    /// <summary>
    /// The list of version entries.
    /// </summary>
    [Required]
    public IEnumerable<EntityVersionResponseModel> Versions { get; set; } = [];
}
