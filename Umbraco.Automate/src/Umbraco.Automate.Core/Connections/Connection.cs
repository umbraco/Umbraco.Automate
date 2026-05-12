using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// A named credential set that actions can use to authenticate with external systems.
/// Access is controlled at the workspace level — workspaces declare which connections are available.
/// </summary>
public sealed class Connection : IVersionableEntity
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique, URL-safe alias (used for Deploy transfer).
    /// </summary>
    public required string Alias { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the connection type alias (e.g. "httpBasicAuth", "oauth2").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the connection settings. Sensitive values are encrypted at rest.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];

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
