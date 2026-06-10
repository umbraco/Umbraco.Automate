namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Thrown when an automation exceeds its configured rate limit.
/// </summary>
public sealed class RateLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class.
    /// </summary>
    public RateLimitExceededException(Guid automationId, string message)
        : base(message)
    {
        AutomationId = automationId;
    }

    /// <summary>
    /// Gets the automation ID that exceeded the rate limit.
    /// </summary>
    public Guid AutomationId { get; }
}
