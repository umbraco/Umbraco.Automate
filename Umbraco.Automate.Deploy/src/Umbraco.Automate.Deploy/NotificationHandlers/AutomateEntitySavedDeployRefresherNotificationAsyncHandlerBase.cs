using Umbraco.Automate.Core.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Core.Connectors.ServiceConnectors;
using Umbraco.Deploy.Infrastructure.Disk;
using Umbraco.Extensions;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

/// <summary>
/// Base class for handling saved notifications for Umbraco.Automate entities.
/// Automatically writes artifacts to disk when entities are saved.
/// </summary>
public abstract class AutomateEntitySavedDeployRefresherNotificationAsyncHandlerBase<TEntity, TNotification>
    : INotificationAsyncHandler<TNotification>
    where TEntity : class, IAutomateEntity
    where TNotification : INotification
{
    private readonly IServiceConnectorFactory _serviceConnectorFactory;
    private readonly IDiskEntityService _diskEntityService;
    private readonly ISignatureService _signatureService;
    private readonly string _entityType;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    protected AutomateEntitySavedDeployRefresherNotificationAsyncHandlerBase(
        IServiceConnectorFactory serviceConnectorFactory,
        IDiskEntityService diskEntityService,
        ISignatureService signatureService,
        string entityType)
    {
        _serviceConnectorFactory = serviceConnectorFactory;
        _diskEntityService = diskEntityService;
        _signatureService = signatureService;
        _entityType = entityType;

        diskEntityService.RegisterDiskEntityType(entityType);
    }

    /// <summary>
    /// Extracts the entity from the notification.
    /// </summary>
    protected abstract TEntity GetEntity(TNotification notification);

    /// <inheritdoc />
    public async Task HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        var entity = GetEntity(notification);

        var artifacts = await _serviceConnectorFactory
            .GetArtifactsAsync(_entityType, [entity], new DictionaryCache(), cancellationToken)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await _diskEntityService.WriteArtifactsAsync(artifacts, cancellationToken).ConfigureAwait(false);

        _signatureService.SetSignatures(artifacts);
    }
}
