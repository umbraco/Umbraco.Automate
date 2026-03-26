using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Models;

/// <summary>
/// Request model for updating an existing workspace.
/// </summary>
public sealed class UpdateWorkspaceRequestModel
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

    /// <summary>
    /// The version of the workspace that the client last read.
    /// Used for optimistic concurrency — the server returns 409 Conflict if this
    /// does not match the current version.
    /// </summary>
    [Required]
    public int Version { get; init; }
}
