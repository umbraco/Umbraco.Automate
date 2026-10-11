namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ApplicationStartedTrigger"/> when the application starts.
/// </summary>
public sealed class ApplicationStartedTriggerOutput
{
    /// <summary>
    /// Gets the UTC timestamp when the application started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the application is restarting (true) or starting for the first time (false).
    /// </summary>
    public bool IsRestarting { get; set; }

    /// <summary>
    /// Gets the role of the server that started (e.g. <c>Single</c>, <c>SchedulingPublisher</c>,
    /// <c>Subscriber</c> or <c>Unknown</c> if election had not completed yet). Captured when the
    /// event is raised because the automation may be dispatched on a different server.
    /// </summary>
    public string ServerRole { get; set; } = string.Empty;
}
