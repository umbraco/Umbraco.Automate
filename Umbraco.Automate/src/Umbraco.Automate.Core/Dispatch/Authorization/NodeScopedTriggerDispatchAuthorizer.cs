using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Actions;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Built-in authoriser for triggers whose output is bound to a CMS content or media node.
/// Triggers opt in by implementing <see cref="INodeScopedTrigger"/>; the authoriser checks
/// the workspace service account's start-node / Browse permission via
/// <see cref="IAutomationActionAuthorizer"/>, matching the per-action node guard.
/// </summary>
/// <remarks>
/// Returns <see cref="AutomationAuthorizationResult.Success"/> when the trigger isn't node
/// scoped, when typed output isn't available, or when the trigger doesn't extract a target
/// node from this event (a config-level event the trigger considers unrestricted).
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
        if (context.Trigger is not INodeScopedTrigger nodeScoped || context.TypedOutput is null)
        {
            return AutomationAuthorizationResult.Success;
        }

        if (nodeScoped.GetTargetNode(context.TypedOutput) is not { } target)
        {
            return AutomationAuthorizationResult.Success;
        }

        // Trigger payload only carries a node identifier — Browse is the natural gate for
        // "may this account learn that this node was modified?". Verb-specific checks
        // (publish/update) are still enforced inside each action.
        return target.Kind switch
        {
            NodeScopedTriggerTargetKind.Content => await _nodeAuthorizer.AuthorizeContentAsync(
                context.ServiceAccount, target.Key, BrowsePermissions, cancellationToken),
            NodeScopedTriggerTargetKind.Media => await _nodeAuthorizer.AuthorizeMediaAsync(
                context.ServiceAccount, target.Key, cancellationToken),
            _ => AutomationAuthorizationResult.Success,
        };
    }
}
