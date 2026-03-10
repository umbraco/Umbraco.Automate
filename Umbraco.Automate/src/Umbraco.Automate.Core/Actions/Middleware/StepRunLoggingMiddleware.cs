using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that logs the start and completion of each action execution
/// along with the elapsed duration.
/// </summary>
internal sealed class StepRunLoggingMiddleware : IActionMiddleware
{
    private readonly ILogger<StepRunLoggingMiddleware> _logger;

    public StepRunLoggingMiddleware(ILogger<StepRunLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Executing action '{ActionAlias}' for step {StepId} in run {RunId}.",
            context.ActionAlias,
            context.StepId,
            context.RunId);

        var stopwatch = Stopwatch.StartNew();
        var result = await next(context, cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Action '{ActionAlias}' for step {StepId} in run {RunId} completed with status {Status} in {ElapsedMs}ms.",
            context.ActionAlias,
            context.StepId,
            context.RunId,
            result.Status,
            stopwatch.ElapsedMilliseconds);

        return result;
    }
}
