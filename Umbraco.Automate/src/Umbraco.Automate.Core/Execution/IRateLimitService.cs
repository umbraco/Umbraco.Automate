namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Service for checking per-automation rate limits before execution.
/// </summary>
internal interface IRateLimitService
{
    /// <summary>
    /// Checks whether the automation is within rate limits.
    /// Throws <see cref="RateLimitExceededException"/> if the limit is exceeded.
    /// </summary>
    Task CheckRateLimitAsync(Guid automationId, CancellationToken cancellationToken = default);
}
