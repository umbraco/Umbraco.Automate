using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Notifications;

/// <summary>
/// Notification fired before a workspace is saved. Can be cancelled.
/// </summary>
public sealed class WorkspaceSavingNotification(Workspace target, EventMessages messages)
    : CancelableObjectNotification<Workspace>(target, messages);

/// <summary>
/// Notification fired after a workspace has been saved.
/// </summary>
public sealed class WorkspaceSavedNotification(Workspace target, EventMessages messages)
    : ObjectNotification<Workspace>(target, messages)
{
    /// <summary>
    /// Gets the saved workspace.
    /// </summary>
    public Workspace Workspace { get; } = target;
}

/// <summary>
/// Notification fired before a workspace is deleted. Can be cancelled.
/// </summary>
public sealed class WorkspaceDeletingNotification(Workspace target, EventMessages messages)
    : CancelableObjectNotification<Workspace>(target, messages);

/// <summary>
/// Notification fired after a workspace has been deleted.
/// </summary>
public sealed class WorkspaceDeletedNotification(Workspace target, EventMessages messages)
    : ObjectNotification<Workspace>(target, messages)
{
    /// <summary>
    /// Gets the deleted workspace.
    /// </summary>
    public Workspace Workspace { get; } = target;
}
