namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Builds and invokes the middleware pipeline around action execution.
/// </summary>
internal sealed class ActionMiddlewarePipeline
{
    private readonly ActionMiddlewareCollection _middleware;

    public ActionMiddlewarePipeline(ActionMiddlewareCollection middleware)
    {
        _middleware = middleware;
    }

    /// <summary>
    /// Executes the action through the middleware pipeline.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of execution.</returns>
    public Task<ActionResult> ExecuteAsync(IAction action, ActionContext context, CancellationToken cancellationToken)
    {
        // Build the pipeline from the inside out:
        // The innermost delegate is the action itself.
        ActionMiddlewareDelegate pipeline = (ctx, ct) => action.ExecuteAsync(ctx, ct);

        // Wrap in middleware in reverse order so the first middleware is outermost.
        foreach (var middleware in _middleware.Reverse())
        {
            var next = pipeline;
            pipeline = (ctx, ct) => middleware.ApplyAsync(ctx, next, ct);
        }

        return pipeline(context, cancellationToken);
    }
}
