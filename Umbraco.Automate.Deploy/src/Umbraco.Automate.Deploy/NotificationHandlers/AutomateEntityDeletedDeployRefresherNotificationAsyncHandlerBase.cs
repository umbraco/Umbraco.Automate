using Umbraco.Automate.Core.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

/// <summary>
/// Base class for handling deleted notifications for Umbraco.Automate entities.
/// Automatically removes artifacts from disk when entities are deleted.
/// </summary>
public abstract class AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase<TEntity, TNotification>
    : INotificationAsyncHandler<TNotification>
    where TEntity : class, IAutomateEntity
    where TNotification : INotification
{
    private readonly IDiskEntityService _diskEntityService;
    private readonly ISignatureService _signatureService;
    private readonly string _entityType;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    protected AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase(
        IDiskEntityService diskEntityService,
        ISignatureService signatureService,
        string entityType)
    {
        _diskEntityService = diskEntityService;
        _signatureService = signatureService;
        _entityType = entityType;

        diskEntityService.RegisterDiskEntityType(entityType);
    }

    /// <summary>
    /// Extracts the entity ID from the notification.
    /// </summary>
    protected abstract Guid GetEntityId(TNotification notification);

    /// <inheritdoc />
    public Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        var entityId = GetEntityId(notification);
        var udi = Udi.Create(_entityType, entityId);

        _diskEntityService.DeleteArtifacts(
            [entityId],
            id => Udi.Create(_entityType, id),
            _ => $"Deleted {_entityType}");

        _signatureService.ClearSignatures([udi]);

        return Task.CompletedTask;
    }
}
