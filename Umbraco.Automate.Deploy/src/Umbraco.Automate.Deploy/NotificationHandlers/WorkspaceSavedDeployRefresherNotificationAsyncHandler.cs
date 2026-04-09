using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Core.Connectors.ServiceConnectors;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

internal sealed class WorkspaceSavedDeployRefresherNotificationAsyncHandler(
    IServiceConnectorFactory serviceConnectorFactory,
    IDiskEntityService diskEntityService,
    ISignatureService signatureService)
    : AutomateEntitySavedDeployRefresherNotificationAsyncHandlerBase<Workspace, WorkspaceSavedNotification>(
        serviceConnectorFactory,
        diskEntityService,
        signatureService,
        UmbracoAutomateDeployConstants.UdiEntityType.Workspace)
{
    /// <inheritdoc />
    protected override Workspace GetEntity(WorkspaceSavedNotification notification) => notification.Workspace;
}
