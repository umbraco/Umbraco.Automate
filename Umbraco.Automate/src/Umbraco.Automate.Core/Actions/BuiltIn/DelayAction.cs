using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that pauses execution for a configured duration.
/// Useful for rate limiting, waiting for external processes, or spacing out steps.
/// </summary>
[Action("umbracoAutomate.delay", "Delay")]
public sealed class DelayAction : ActionBase<DelayActionSettings>
{
    private readonly ILogger<DelayAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayAction"/> class.
    /// </summary>
    public DelayAction(ActionInfrastructure infrastructure, ILogger<DelayAction> logger)
        : base(infrastructure)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override string? Description => "Pauses execution for a specified duration.";

    /// <inheritdoc />
    public override string? Group => "Core";

    /// <inheritdoc />
    public override string? Icon => "icon-time";

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<DelayActionSettings>();

        if (!TimeSpan.TryParse(settings.Duration, out var duration) || duration < TimeSpan.Zero)
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid delay duration: '{settings.Duration}'. Expected a TimeSpan string (e.g. 00:05:00)."),
                StepRunErrorCategory.Validation);
        }

        if (duration == TimeSpan.Zero)
        {
            return ActionResult.Success(new { DelayedFor = "00:00:00" });
        }

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Delaying for {Duration}",
            context.AutomationId, context.RunId, duration);

        await Task.Delay(duration, cancellationToken);

        return ActionResult.Success(new { DelayedFor = duration.ToString() });
    }
}
