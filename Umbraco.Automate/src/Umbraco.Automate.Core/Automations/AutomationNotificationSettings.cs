namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Per-automation notification settings stored as part of the automation definition.
/// </summary>
public sealed class AutomationNotificationSettings
{
    /// <summary>
    /// Gets or sets the configured notification channels.
    /// </summary>
    public List<ChannelConfiguration> Channels { get; set; } = [];
}

/// <summary>
/// Configuration for a specific channel on an automation.
/// </summary>
public sealed class ChannelConfiguration
{
    /// <summary>
    /// Gets the channel alias.
    /// </summary>
    public required string ChannelAlias { get; init; }

    /// <summary>
    /// Gets or sets the raw channel settings (resolved via the channel's SettingsType).
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this channel configuration is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when this channel should send notifications.
    /// </summary>
    public NotifyOn NotifyOn { get; set; } = NotifyOn.Failed;
}

/// <summary>
/// When to fire notification channels.
/// </summary>
[Flags]
public enum NotifyOn
{
    /// <summary>Never notify.</summary>
    Never = 0,

    /// <summary>Notify on failure.</summary>
    Failed = 1,

    /// <summary>Notify on suspension (waiting for input).</summary>
    Suspended = 2,

    /// <summary>Notify on successful completion.</summary>
    Completed = 4,

    /// <summary>Notify when a run succeeds after the previous terminal run was a failure.</summary>
    Recovered = 8,

    /// <summary>Notify when the automation is auto-disabled by the circuit breaker.</summary>
    Disabled = 16,

    /// <summary>Notify when the circuit breaker issues a degradation warning.</summary>
    Warning = 32,

    /// <summary>Notify on both failure and suspension.</summary>
    FailedOrSuspended = Failed | Suspended,
}
