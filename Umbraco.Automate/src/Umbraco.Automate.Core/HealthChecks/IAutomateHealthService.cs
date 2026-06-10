namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Provides health status information for the automation engine.
/// Used by Umbraco health checks to query internal infrastructure state.
/// </summary>
public interface IAutomateHealthService
{
    /// <summary>
    /// Gets the current outbox queue statistics.
    /// </summary>
    Task<AutomateQueueStats> GetQueueStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Queue statistics for the automation outbox.
/// </summary>
public sealed class AutomateQueueStats
{
    /// <summary>Number of messages waiting to be processed.</summary>
    public int Pending { get; init; }

    /// <summary>Number of messages currently being processed.</summary>
    public int Processing { get; init; }

    /// <summary>Number of messages that failed processing.</summary>
    public int Failed { get; init; }

    /// <summary>Number of messages that exceeded max retries.</summary>
    public int DeadLettered { get; init; }
}
