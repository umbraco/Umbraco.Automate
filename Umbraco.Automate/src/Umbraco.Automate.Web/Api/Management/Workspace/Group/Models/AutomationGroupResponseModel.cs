using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;

/// <summary>
/// Response model for an automation group.
/// </summary>
public sealed class AutomationGroupResponseModel
{
    /// <summary>The group ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The parent group ID, or null if at root.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>When the group was created.</summary>
    public DateTime DateCreated { get; set; }
}
