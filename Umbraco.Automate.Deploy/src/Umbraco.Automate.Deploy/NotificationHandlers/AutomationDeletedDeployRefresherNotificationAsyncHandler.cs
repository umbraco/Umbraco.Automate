using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Deploy.Core;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.NotificationHandlers;

internal sealed class AutomationDeletedDeployRefresherNotificationAsyncHandler(
    IDiskEntityService diskEntityService,
    ISignatureService signatureService)
    : AutomateEntityDeletedDeployRefresherNotificationAsyncHandlerBase<Automation, AutomationDeletedNotification>(
        diskEntityService,
        signatureService,
        UmbracoAutomateDeployConstants.UdiEntityType.Automation)
{
    /// <inheritdoc />
    protected override Guid GetEntityId(AutomationDeletedNotification notification) => notification.Automation.Id;
}
