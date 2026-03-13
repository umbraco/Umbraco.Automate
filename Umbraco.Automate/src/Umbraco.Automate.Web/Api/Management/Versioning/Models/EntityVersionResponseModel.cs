using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Models;

/// <summary>
/// Response model for a single version history entry.
/// </summary>
public class EntityVersionResponseModel
{
    /// <summary>
    /// The unique identifier of this version record.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the entity this version belongs to.
    /// </summary>
    [Required]
    public Guid EntityId { get; set; }

    /// <summary>
    /// The version number (1, 2, 3, etc.).
    /// </summary>
    [Required]
    public int Version { get; set; }

    /// <summary>
    /// The date and time when this version was created.
    /// </summary>
    [Required]
    public DateTime DateCreated { get; set; }

    /// <summary>
    /// The user key (GUID) of the user who created this version.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// Optional description of what changed in this version.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// Whether this version is the currently published version.
    /// Always false for non-publishable entity types.
    /// </summary>
    [Required]
    public bool IsPublished { get; set; }
}
