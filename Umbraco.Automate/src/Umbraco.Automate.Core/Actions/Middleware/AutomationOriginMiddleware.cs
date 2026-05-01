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

        var origin = new AutomationOrigin(
            RunId: context.ExecutionContext.RunId,
            AutomationId: context.AutomationId,
            WorkspaceId: context.ExecutionContext.WorkspaceId,
            ChainDepth: context.ExecutionContext.ChainDepth);

        using var _ = _originAccessor.Push(origin);

        return await next(context, cancellationToken);
    }
}
