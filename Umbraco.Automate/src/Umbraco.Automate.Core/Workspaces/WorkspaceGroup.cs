namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// A group (folder) that organizes automations within a workspace.
/// </summary>
public sealed class WorkspaceGroup
{
    /// <summary>
    /// Gets or sets the unique ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the parent group ID, or null if at the root.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Gets or sets the workspace this group belongs to.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets when the group was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
