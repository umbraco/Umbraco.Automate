using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;

/// <summary>
/// Response model for a workspace group.
/// </summary>
public sealed class WorkspaceGroupResponseModel
{
    /// <summary>The group ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The parent group ID, or null if at root.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>The workspace this group belongs to.</summary>
    [Required]
    public Guid WorkspaceId { get; set; }

    /// <summary>When the group was created.</summary>
    public DateTime DateCreated { get; set; }
}
