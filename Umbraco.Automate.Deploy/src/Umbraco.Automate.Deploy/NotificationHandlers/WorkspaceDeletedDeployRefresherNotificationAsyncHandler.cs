using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

internal sealed class WorkspaceDeletedDeployRefresherNotificationAsyncHandler(
    IDiskEntityService diskEntityService,
    ISignatureService signatureService)
    : AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase<Workspace, WorkspaceDeletedNotification>(
        diskEntityService,
        signatureService,
        UmbracoAutomateDeployConstants.UdiEntityType.Workspace)
{
    /// <inheritdoc />
    protected override Guid GetEntityId(WorkspaceDeletedNotification notification) => notification.Workspace.Id;
}
