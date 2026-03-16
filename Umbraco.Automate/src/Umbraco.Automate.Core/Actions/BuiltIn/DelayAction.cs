using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that pauses execution for a configured duration.
/// Useful for rate limiting, waiting for external processes, or spacing out steps.
/// </summary>
[Action("umbracoAutomate.delay", "Delay",
    Description = "Pauses execution for a specified duration.",
    Group = "Core",
    Icon = "icon-time")]
public sealed class DelayAction : ActionBase<DelaySettings, DelayOutput>
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
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<DelaySettings>();

        if (!TimeSpan.TryParse(settings.Duration, out var duration) || duration < TimeSpan.Zero)
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException($"Invalid delay duration: '{settings.Duration}'. Expected a TimeSpan string (e.g. 00:05:00)."),
                StepRunErrorCategory.Validation));
        }

        if (duration == TimeSpan.Zero)
        {
            return Task.FromResult(Success(new DelayOutput { DelayedFor = "00:00:00" }));
        }

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Sleeping for {Duration} (durable)",
            context.AutomationId, context.RunId, duration);

        return Task.FromResult(Sleep(duration, new DelayOutput { DelayedFor = duration.ToString() }));
    }
}
