using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions from action execution
/// and wraps them in a failed <see cref="ActionResult"/>.
/// </summary>
internal sealed class ErrorHandlingMiddleware : IActionMiddleware
{
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Action '{ActionAlias}' was cancelled for step {StepId} in run {RunId}.",
                context.ActionAlias,
                context.StepId,
                context.RunId);

            return ActionResult.Failed(
                new OperationCanceledException($"Action '{context.ActionAlias}' was cancelled."),
                StepRunErrorCategory.Cancelled);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "Action '{ActionAlias}' timed out for step {StepId} in run {RunId}.",
                context.ActionAlias,
                context.StepId,
                context.RunId);

            return ActionResult.Failed(ex, StepRunErrorCategory.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Action '{ActionAlias}' failed with an unhandled exception for step {StepId} in run {RunId}.",
                context.ActionAlias,
                context.StepId,
                context.RunId);

            return ActionResult.Failed(ex);
        }
    }
}
