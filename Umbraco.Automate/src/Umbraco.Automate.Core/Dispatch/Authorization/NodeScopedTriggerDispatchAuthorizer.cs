using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Built-in authoriser for triggers whose output is bound to CMS content or media nodes.
/// Outputs opt in by implementing <see cref="IContentScopedTriggerOutput"/> or
/// <see cref="IMediaScopedTriggerOutput"/>; the authoriser then checks the workspace
/// service account's start-node / Browse permission via
/// <see cref="IAutomationActionAuthorizer"/>, matching the per-action node guard.
/// </summary>
/// <remarks>
/// <para>
/// Single-item outputs are allowed or denied whole. Batch outputs
/// (<see cref="IBatchTriggerOutput"/>) are checked item by item using the same rule: items
/// the account may not Browse are removed, and dispatch is denied only when none remain.
/// Keeping both shapes here means one place decides node access for trigger payloads.
/// </para>
/// <para>
/// Returns <see cref="AutomationAuthorizationResult.Success"/> when the typed output is
/// absent, when neither marker is present, or when the marker reports no target key (a
/// config-level event the output considers unrestricted). Batch items without a marker are
/// kept for the same reason.
/// </para>
/// </remarks>
internal sealed class NodeScopedTriggerDispatchAuthorizer : IOutputNarrowingTriggerDispatchAuthorizer
{
    private static readonly IReadOnlySet<string> BrowsePermissions = new HashSet<string> { ActionBrowse.ActionLetter };

    private readonly IAutomationActionAuthorizer _nodeAuthorizer;

    public NodeScopedTriggerDispatchAuthorizer(IAutomationActionAuthorizer nodeAuthorizer)
    {
        _nodeAuthorizer = nodeAuthorizer;
    }

    /// <summary>
    /// Plain-contract view of <see cref="AuthorizeAndNarrowAsync"/>.
    /// </summary>
    /// <remarks>
    /// For a batch output, <see cref="AutomationAuthorizationResult.Success"/> means "at least
    /// one item is visible to the service account", <em>not</em> "the whole output may be
    /// dispatched". Callers that dispatch must use <see cref="AuthorizeAndNarrowAsync"/> and
    /// run with its narrowed output; <see cref="TriggerEventHandler"/> does this.
    /// </remarks>
    public async Task<AutomationAuthorizationResult> AuthorizeAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken)
        => (await AuthorizeAndNarrowAsync(context, cancellationToken)).Result;

    /// <inheritdoc />
    public Task<TriggerDispatchNarrowingResult> AuthorizeAndNarrowAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken)
        => context.TypedOutput is IBatchTriggerOutput batch
            ? AuthorizeBatchAsync(context.ServiceAccount, batch, cancellationToken)
            : AuthorizeSingleAsync(context.ServiceAccount, context.TypedOutput, cancellationToken);

    private async Task<TriggerDispatchNarrowingResult> AuthorizeSingleAsync(
        IUser serviceAccount,
        object? output,
        CancellationToken cancellationToken)
    {
        var result = await AuthorizeNodeAsync(serviceAccount, output, cancellationToken);
        return result.Authorized
            ? TriggerDispatchNarrowingResult.Unchanged
            : TriggerDispatchNarrowingResult.Denied(result);
    }

    private async Task<TriggerDispatchNarrowingResult> AuthorizeBatchAsync(
        IUser serviceAccount,
        IBatchTriggerOutput batch,
        CancellationToken cancellationToken)
    {
        var items = batch.GetItems();
        if (items.Count == 0)
        {
            return TriggerDispatchNarrowingResult.Unchanged;
        }

        // CMS's permission services authorise a set of keys atomically (one denied key fails
        // the whole call) and silently ignore keys they can't find, so they can't tell us
        // which items to keep. Check each item instead: one node path lookup per item.
        var allowed = new List<object>(items.Count);
        string? firstFailureReason = null;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await AuthorizeNodeAsync(serviceAccount, item, cancellationToken);
            if (result.Authorized)
            {
                allowed.Add(item);
            }
            else
            {
                firstFailureReason ??= result.FailureReason;
            }
        }

        if (allowed.Count == items.Count)
        {
            return TriggerDispatchNarrowingResult.Unchanged;
        }

        if (allowed.Count == 0)
        {
            return TriggerDispatchNarrowingResult.Denied(AutomationAuthorizationResult.Fail(
                $"Service account may not access any of the {items.Count} batch items. First denial: {firstFailureReason}"));
        }

        return TriggerDispatchNarrowingResult.Narrowed(batch.WithItems(allowed));
    }

    private async Task<AutomationAuthorizationResult> AuthorizeNodeAsync(
        IUser serviceAccount,
        object? output,
        CancellationToken cancellationToken)
    {
        // Trigger payload only carries a node identifier — Browse is the natural gate for
        // "may this account learn that this node was modified?". Verb-specific checks
        // (publish/update) are still enforced inside each action.
        switch (output)
        {
            case IContentScopedTriggerOutput content when content.GetContentKey() is { } contentKey:
                return await _nodeAuthorizer.AuthorizeContentAsync(
                    serviceAccount, contentKey, BrowsePermissions, cancellationToken);

            case IMediaScopedTriggerOutput media when media.GetMediaKey() is { } mediaKey:
                return await _nodeAuthorizer.AuthorizeMediaAsync(
                    serviceAccount, mediaKey, cancellationToken);

            default:
                return AutomationAuthorizationResult.Success;
        }
    }
}
