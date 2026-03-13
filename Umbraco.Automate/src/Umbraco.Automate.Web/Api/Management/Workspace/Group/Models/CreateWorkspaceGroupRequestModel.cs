using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Group.Models;

/// <summary>
/// Request model for creating a new workspace group.
/// </summary>
public sealed class CreateWorkspaceGroupRequestModel
{
    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The parent group ID, or null for root level.</summary>
    public Guid? ParentId { get; init; }
}
