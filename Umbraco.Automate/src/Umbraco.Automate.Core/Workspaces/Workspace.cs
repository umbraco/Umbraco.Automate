using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// A workspace — an admin-configured container that defines who can work on automations
/// within it and what resources (connections, service accounts) are available.
/// </summary>
public sealed class Workspace : IVersionableEntity
{
    /// <inheritdoc />
    public Guid Id { get; internal set; }

    /// <summary>
    /// Gets or sets the unique, URL-safe alias (used for Deploy transfer).
    /// </summary>
    public required string Alias { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the service account key (<c>UserKind.Api</c> user) —
    /// the identity all automations in this workspace execute as.
    /// </summary>
    public Guid ServiceAccountKey { get; set; }

    /// <summary>
    /// Gets or sets the user group keys that have access to this workspace.
    /// </summary>
    public IList<Guid> UserGroups { get; set; } = [];

    /// <summary>
    /// Gets or sets the connection IDs available within this workspace.
    /// </summary>
    public IList<Guid> Connections { get; set; } = [];

    /// <inheritdoc />
    public int Version { get; internal set; } = 1;

    /// <inheritdoc />
    public DateTime DateCreated { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public Guid? CreatedByUserId { get; set; }

    /// <inheritdoc />
    public Guid? ModifiedByUserId { get; set; }
}
