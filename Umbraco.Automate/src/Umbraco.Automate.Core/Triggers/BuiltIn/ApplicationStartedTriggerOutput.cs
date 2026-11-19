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
    /// Gets a value indicating whether Umbraco restarted its runtime in-process, for example
    /// straight after an install. App pool recycles and deployments start a new process and
    /// report <c>false</c>, just like a cold start.
    /// </summary>
    public bool IsRestarting { get; set; }

    /// <summary>
    /// Gets the role of the server that started (e.g. <c>Single</c>, <c>SchedulingPublisher</c>,
    /// <c>Subscriber</c> or <c>Unknown</c> if election had not completed yet). Captured when the
    /// event is raised because the automation may be dispatched on a different server.
    /// </summary>
    public string ServerRole { get; set; } = string.Empty;
}
