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

    /// <summary>Triggered by replaying a previous run via the management API.</summary>
    public const string Replay = "replay";

    /// <summary>
    /// Returns true when the initiator represents a deliberate human action (a manual "run now"
    /// or a replay) rather than an automatic trigger (<see cref="System"/> / <see cref="Scheduled"/>
    /// / <see cref="Webhook"/>). Used by the circuit breaker to allow interactive test runs while
    /// an automation is auto-disabled.
    /// </summary>
    public static bool IsInteractive(string initiatorType)
        => initiatorType is User or Replay;
}
