using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Models;

/// <summary>
/// Request model for creating a new workspace.
/// </summary>
public sealed class CreateWorkspaceRequestModel
{
    /// <summary>The unique alias.</summary>
    [Required]
    public required string Alias { get; init; }

    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The service account key (UserKind.Api user).</summary>
    [Required]
    public Guid ServiceAccountKey { get; init; }

    /// <summary>User group keys with access to this workspace.</summary>
    public IList<Guid> UserGroups { get; init; } = [];

    /// <summary>Connection IDs that automations in this workspace are allowed to use.</summary>
    public IList<Guid> AllowedConnections { get; init; } = [];
}
