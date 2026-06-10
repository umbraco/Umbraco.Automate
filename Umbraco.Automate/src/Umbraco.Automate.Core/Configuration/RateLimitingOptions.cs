namespace Umbraco.Automate.Core.Configuration;

/// <summary>
/// Configuration options for per-automation rate limiting.
/// Bound to <c>Umbraco:Automate:RateLimiting</c> in appsettings.json.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>
    /// Gets or sets whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of runs per automation per minute.
    /// </summary>
    public int MaxRunsPerAutomationPerMinute { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of concurrent runs per automation.
    /// </summary>
    public int MaxConcurrentRunsPerAutomation { get; set; } = 10;
}
