using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Group.Models;

/// <summary>
/// Request model for updating an automation group.
/// </summary>
public sealed class UpdateAutomationGroupRequestModel
{
    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The parent group ID, or null for root level.</summary>
    public Guid? ParentId { get; init; }
}
