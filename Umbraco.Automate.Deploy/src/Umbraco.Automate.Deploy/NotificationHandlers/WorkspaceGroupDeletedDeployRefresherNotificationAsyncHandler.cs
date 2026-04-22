using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

internal sealed class WorkspaceGroupDeletedDeployRefresherNotificationAsyncHandler(
    IDiskEntityService diskEntityService,
    ISignatureService signatureService)
    : AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase<WorkspaceGroup, WorkspaceGroupDeletedNotification>(
        diskEntityService,
        signatureService,
        UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup)
{
    /// <inheritdoc />
    protected override Guid GetEntityId(WorkspaceGroupDeletedNotification notification) => notification.WorkspaceGroup.Id;
}
