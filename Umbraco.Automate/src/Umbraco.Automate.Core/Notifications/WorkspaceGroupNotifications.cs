using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Notifications;

/// <summary>
/// Notification fired before a workspace group is saved. Can be cancelled.
/// </summary>
public sealed class WorkspaceGroupSavingNotification(WorkspaceGroup target, EventMessages messages)
    : CancelableObjectNotification<WorkspaceGroup>(target, messages);

/// <summary>
/// Notification fired after a workspace group has been saved.
/// </summary>
public sealed class WorkspaceGroupSavedNotification(WorkspaceGroup target, EventMessages messages)
    : ObjectNotification<WorkspaceGroup>(target, messages)
{
    /// <summary>
    /// Gets the saved workspace group.
    /// </summary>
    public WorkspaceGroup WorkspaceGroup { get; } = target;
}

/// <summary>
/// Notification fired before a workspace group is deleted. Can be cancelled.
/// </summary>
public sealed class WorkspaceGroupDeletingNotification(WorkspaceGroup target, EventMessages messages)
    : CancelableObjectNotification<WorkspaceGroup>(target, messages);

/// <summary>
/// Notification fired after a workspace group has been deleted.
/// </summary>
public sealed class WorkspaceGroupDeletedNotification(WorkspaceGroup target, EventMessages messages)
    : ObjectNotification<WorkspaceGroup>(target, messages)
{
    /// <summary>
    /// Gets the deleted workspace group.
    /// </summary>
    public WorkspaceGroup WorkspaceGroup { get; } = target;
}
