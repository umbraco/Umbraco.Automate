namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Constants for the well-known trigger initiator type values.
/// </summary>
public static class TriggerInitiatorType
{
    /// <summary>Triggered by internal system logic (e.g. content events, Engage analytics).</summary>
    public const string System = "system";

    /// <summary>Triggered manually by an authenticated user via the management API.</summary>
    public const string User = "user";

    /// <summary>Triggered by an inbound HTTP webhook request.</summary>
    public const string Webhook = "webhook";

    /// <summary>Triggered by a CRON schedule.</summary>
    public const string Scheduled = "scheduled";
}
