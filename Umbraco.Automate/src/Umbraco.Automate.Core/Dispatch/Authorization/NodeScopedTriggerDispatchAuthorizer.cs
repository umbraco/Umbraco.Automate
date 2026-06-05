using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Actions;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Built-in authoriser for triggers whose output is bound to a CMS content or media node.
/// Outputs opt in by implementing <see cref="IContentScopedTriggerOutput"/> or
/// <see cref="IMediaScopedTriggerOutput"/>; the authoriser then checks the workspace
/// service account's start-node / Browse permission via
/// <see cref="IAutomationActionAuthorizer"/>, matching the per-action node guard.
/// </summary>
/// <remarks>
/// Returns <see cref="AutomationAuthorizationResult.Success"/> when the typed output is
/// absent, when neither marker is present, or when the marker reports no target key (a
/// config-level event the output considers unrestricted).
/// </remarks>
internal sealed class NodeScopedTriggerDispatchAuthorizer : ITriggerDispatchAuthorizer
{
    private static readonly IReadOnlySet<string> BrowsePermissions = new HashSet<string> { ActionBrowse.ActionLetter };

    private readonly IAutomationActionAuthorizer _nodeAuthorizer;

    public NodeScopedTriggerDispatchAuthorizer(IAutomationActionAuthorizer nodeAuthorizer)
    {
        _nodeAuthorizer = nodeAuthorizer;
    }

    public async Task<AutomationAuthorizationResult> AuthorizeAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        // Trigger payload only carries a node identifier — Browse is the natural gate for
        // "may this account learn that this node was modified?". Verb-specific checks
        // (publish/update) are still enforced inside each action.
        switch (context.TypedOutput)
        {
            case IContentScopedTriggerOutput content when content.GetContentKey() is { } contentKey:
                return await _nodeAuthorizer.AuthorizeContentAsync(
                    context.ServiceAccount, contentKey, BrowsePermissions, cancellationToken);

            case IMediaScopedTriggerOutput media when media.GetMediaKey() is { } mediaKey:
                return await _nodeAuthorizer.AuthorizeMediaAsync(
                    context.ServiceAccount, mediaKey, cancellationToken);

            default:
                return AutomationAuthorizationResult.Success;
        }
    }
}
