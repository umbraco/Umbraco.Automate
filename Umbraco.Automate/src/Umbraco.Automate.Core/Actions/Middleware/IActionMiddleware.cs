using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Delegate representing the next middleware in the action execution pipeline.
/// </summary>
public delegate Task<ActionResult> ActionMiddlewareDelegate(ActionContext context, CancellationToken cancellationToken);

/// <summary>
/// A middleware that wraps action execution with cross-cutting concerns.
/// Middleware is executed in registration order, forming a nested pipeline around the action.
/// </summary>
public interface IActionMiddleware : IDiscoverable
{
    /// <summary>
    /// Processes the action context and optionally delegates to the next middleware.
    /// </summary>
    /// <param name="context">The action execution context.</param>
    /// <param name="next">The next middleware or action in the pipeline.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The action result.</returns>
    Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken);
}
