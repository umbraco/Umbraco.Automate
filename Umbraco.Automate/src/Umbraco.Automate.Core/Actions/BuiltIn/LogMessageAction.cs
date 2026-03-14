using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that writes a message to the application log.
/// Useful for debugging automations and as a minimal smoke-test action.
/// </summary>
[Action("umbracoAutomate.logMessage", "Log Message",
    Description = "Writes a message to the application log.",
    Group = "Core",
    Icon = "icon-notepad")]
public sealed class LogMessageAction : ActionBase<LogMessageSettings>
{
    private readonly ILogger<LogMessageAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogMessageAction"/> class.
    /// </summary>
    public LogMessageAction(ActionInfrastructure infrastructure, ILogger<LogMessageAction> logger)
        : base(infrastructure)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<LogMessageSettings>();
        var level = ParseLogLevel(settings.LogLevel);

        _logger.Log(level, "Automation {AutomationId} / Run {RunId}: {Message}",
            context.AutomationId, context.RunId, settings.Message);

        return Task.FromResult(ActionResult.Success(new { Message = settings.Message }));
    }

    private static LogLevel ParseLogLevel(string? level)
        => level?.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Information,
        };
}
