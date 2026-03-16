using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Response model for a single ancestor in an automation's tree path.
/// </summary>
public sealed class AutomationAncestorResponseModel
{
    /// <summary>The ancestor's unique ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The entity type (e.g. "ua:workspace" or "ua:automation-group").</summary>
    [Required]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The parent's unique ID, or null for root-level items.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>The parent's entity type, or null for root-level items.</summary>
    public string? ParentEntityType { get; set; }

    /// <summary>Whether this ancestor has children.</summary>
    public bool HasChildren { get; set; }

    /// <summary>Whether this ancestor is a folder (group).</summary>
    public bool IsFolder { get; set; }
}
