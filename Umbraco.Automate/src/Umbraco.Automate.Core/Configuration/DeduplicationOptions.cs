namespace Umbraco.Automate.Core.Configuration;

/// <summary>
/// Configuration options for trigger event deduplication.
/// Bound to <c>Umbraco:Automate:Deduplication</c> in appsettings.json.
/// </summary>
public sealed class DeduplicationOptions
{
    /// <summary>
    /// Gets or sets the deduplication time window in minutes.
    /// Events with the same idempotency key within this window are treated as duplicates.
    /// </summary>
    public int WindowMinutes { get; set; } = 5;
}
