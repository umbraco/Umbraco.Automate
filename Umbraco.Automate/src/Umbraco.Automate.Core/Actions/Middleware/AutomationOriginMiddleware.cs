using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Pushes the running automation's <see cref="AutomationOrigin"/> onto the
/// <see cref="IAutomationOriginAccessor"/> for the duration of action execution. Lets the
/// trigger dispatch path detect notifications that fire as a side effect of an action and
/// stamp them with the originating run, enabling per-trigger loop-prevention and a global
/// chain-depth backstop.
/// </summary>
internal sealed class AutomationOriginMiddleware : IActionMiddleware
{
    private readonly IAutomationOriginAccessor _originAccessor;

    public AutomationOriginMiddleware(IAutomationOriginAccessor originAccessor)
    {
        _originAccessor = originAccessor;
    }

    /// <inheritdoc />
    public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        if (context.ExecutionContext is null)
        {
            return await next(context, cancellationToken);
        }

        // Build the lineage that any side effect of this action will carry: upstream chain
        // received from the trigger, plus this run's own automation. Receivers detect cycles
        // by looking for their own ID in this chain.
        var inheritedChain = context.ExecutionContext.OriginChain;
        var chain = new Guid[inheritedChain.Count + 1];
        for (var i = 0; i < inheritedChain.Count; i++)
        {
            chain[i] = inheritedChain[i];
        }

        chain[inheritedChain.Count] = context.AutomationId;

        var origin = new AutomationOrigin(
            RunId: context.ExecutionContext.RunId,
            WorkspaceId: context.ExecutionContext.WorkspaceId,
            AutomationChain: chain);

        using var _ = _originAccessor.Push(origin);

        return await next(context, cancellationToken);
    }
}
