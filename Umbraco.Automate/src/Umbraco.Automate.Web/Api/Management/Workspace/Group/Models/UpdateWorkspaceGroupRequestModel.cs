using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;

/// <summary>
/// Request model for updating a workspace group.
/// </summary>
public sealed class UpdateWorkspaceGroupRequestModel
{
    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The parent group ID, or null for root level.</summary>
    public Guid? ParentId { get; init; }
}
