using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Notifications;

/// <summary>
/// Notification fired before a connection is saved. Can be cancelled.
/// </summary>
public sealed class ConnectionSavingNotification(Connection target, EventMessages messages)
    : CancelableObjectNotification<Connection>(target, messages);

/// <summary>
/// Notification fired after a connection has been saved.
/// </summary>
public sealed class ConnectionSavedNotification(Connection target, EventMessages messages)
    : ObjectNotification<Connection>(target, messages)
{
    /// <summary>
    /// Gets the saved connection.
    /// </summary>
    public Connection Connection { get; } = target;
}

/// <summary>
/// Notification fired before a connection is deleted. Can be cancelled.
/// </summary>
public sealed class ConnectionDeletingNotification(Connection target, EventMessages messages)
    : CancelableObjectNotification<Connection>(target, messages);

/// <summary>
/// Notification fired after a connection has been deleted.
/// </summary>
public sealed class ConnectionDeletedNotification(Connection target, EventMessages messages)
    : ObjectNotification<Connection>(target, messages)
{
    /// <summary>
    /// Gets the deleted connection.
    /// </summary>
    public Connection Connection { get; } = target;
}
