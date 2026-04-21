using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Automate.Web.Realtime;

/// <summary>
/// SignalR hub used to push realtime notifications to any authenticated backoffice user.
/// Connection is gated by the backoffice access policy — the client filters messages by
/// the content it is currently editing.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class EditorNotificationHub : Hub<IEditorNotificationHubClient>
{
    /// <summary>
    /// The relative route the hub is served on.
    /// </summary>
    public const string Route = "/umbraco/automate/hubs/editor-notification";
}
