using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

internal sealed class ConnectionDeletedDeployRefresherNotificationAsyncHandler(
    IDiskEntityService diskEntityService,
    ISignatureService signatureService)
    : AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase<Connection, ConnectionDeletedNotification>(
        diskEntityService,
        signatureService,
        UmbracoAutomateDeployConstants.UdiEntityType.Connection)
{
    /// <inheritdoc />
    protected override Guid GetEntityId(ConnectionDeletedNotification notification) => notification.Connection.Id;
}
