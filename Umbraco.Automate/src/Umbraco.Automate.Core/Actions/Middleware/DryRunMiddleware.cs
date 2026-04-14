namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that intercepts action execution during dry-run mode.
/// Returns a synthetic result describing what the action would do, without executing it.
/// Must be registered as the outermost middleware to prevent any side effects.
/// </summary>
internal sealed class DryRunMiddleware : IActionMiddleware
{
    /// <inheritdoc />
    public Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        if (context.ExecutionContext?.IsDryRun != true)
        {
            return next(context, cancellationToken);
        }

        // Build a description of what the action would do.
        var dryRunOutput = new Dictionary<string, object?>
        {
            ["_dryRun"] = true,
            ["actionAlias"] = context.ActionAlias,
            ["settingsType"] = context.Settings?.GetType().Name,
            ["hasConnection"] = context.Connection is not null,
            ["connectionType"] = context.Connection?.Type,
            ["inputDataKeys"] = context.InputData.Keys.ToArray(),
        };

        // Include settings for inspection (they've already been resolved/validated by this point
        // since dry-run middleware sits outermost, but settings are deserialized in ActionStepBody
        // before the pipeline runs).
        if (context.Settings is not null)
        {
            dryRunOutput["settings"] = context.Settings;
        }

        return Task.FromResult(ActionResult.Success(dryRunOutput));
    }
}
