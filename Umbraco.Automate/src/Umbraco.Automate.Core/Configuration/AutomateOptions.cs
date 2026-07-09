using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Core.Configuration;

/// <summary>
/// Top-level configuration options for Umbraco.Automate.
/// Bound to <c>Umbraco:Automate</c> in appsettings.json.
/// </summary>
/// <remarks>
/// Not <c>sealed</c> so the JSON schema definition can inherit from it and expose both
/// these root-level properties and the sub-section options under a single type.
/// </remarks>
public class AutomateOptions
{
    /// <summary>
    /// Gets or sets whether the automation engine is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the connection string Automate resolves from
    /// <c>ConnectionStrings</c>. Defaults to <c>umbracoAutomateDbDSN</c> so a standard
    /// dedicated configuration just works. Set to <c>umbracoDbDSN</c> to share the
    /// Umbraco CMS database, or to any other named entry so multiple products can share
    /// a single DSN.
    /// </summary>
    /// <remarks>
    /// Pointing at a shared connection is opt-in (i.e. you must explicitly change this
    /// value) because reuse can have performance implications: Automate writes outbox
    /// messages, run history and WorkflowCore engine tables that add traffic to the
    /// named database. The setting exists primarily for hosting environments (e.g.
    /// Umbraco Cloud) where the CMS connection string is not user-editable so cannot
    /// be copied into <c>umbracoAutomateDbDSN</c>.
    /// </remarks>
    public string UseNamedConnectionString { get; set; } = "umbracoAutomateDbDSN";

    /// <summary>
    /// Gets or sets the <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> key
    /// prefixes that automation settings may dereference via the <c>$Key:Path</c> syntax.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settings fields support a <c>$Key:Path</c> syntax that resolves to a configuration
    /// value at run time (e.g. <c>$Umbraco:Automate:Secrets:SlackToken</c>), letting admins
    /// keep credentials and per-environment values in app settings / environment variables
    /// rather than the database.
    /// </para>
    /// <para>
    /// Automations execute under an elevated service-account identity, so configuration
    /// resolution follows least-privilege: it is <strong>default-deny</strong>, and a key is
    /// only resolvable when it falls under one of these prefixes. This keeps automation
    /// settings scoped to configuration explicitly intended for them rather than the whole
    /// configuration tree. The allow-list lives here (app settings) by design, so only someone
    /// who already has access to the configuration decides which subset automations may read.
    /// </para>
    /// <para>
    /// Defaults to two dedicated sections, mirroring the GitHub Actions split:
    /// <c>Umbraco:Automate:Secrets</c> for sensitive values and
    /// <c>Umbraco:Automate:Variables</c> for non-sensitive per-environment values. Admins
    /// place automation-facing values under these. Add further prefixes to expose existing
    /// configuration sections without copying them, accepting that everything under an added
    /// prefix becomes readable by automation authors. Matching is segment-aware and
    /// case-insensitive: the prefix <c>Umbraco:Automate:Secrets</c> permits
    /// <c>Umbraco:Automate:Secrets:SlackToken</c> but not <c>Umbraco:Automate:SecretsBackup:X</c>.
    /// </para>
    /// <para>
    /// Keys under a <see cref="SecretConfigurationKeyPrefixes"/> prefix carry the extra
    /// restriction that they may only be referenced from sensitive fields — see that property.
    /// </para>
    /// </remarks>
    public IList<string> AllowedConfigurationKeyPrefixes { get; set; } = new List<string>
    {
        "Umbraco:Automate:Secrets",
        "Umbraco:Automate:Variables",
    };

    /// <summary>
    /// Gets or sets the subset of <see cref="AllowedConfigurationKeyPrefixes"/> whose values
    /// are treated as secret, and which may therefore only be referenced from settings fields
    /// marked <c>[Field(IsSensitive = true)]</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The allow-list scopes which sections are reachable; this adds a further constraint so a
    /// resolved secret only flows into fields the system already treats as credential-bearing
    /// (encrypted at rest, stripped on export), the same places a directly-entered credential
    /// belongs — rather than into fields whose values may later be surfaced in clear.
    /// </para>
    /// <para>
    /// Mirrors the GitHub Actions split: <c>Umbraco:Automate:Secrets</c> (the default here)
    /// is sensitive and field-restricted; <c>Umbraco:Automate:Variables</c> is left off this
    /// list so non-secret per-environment values (base URLs, IDs, flags) can be referenced
    /// from any field. Entries should be a subset of <see cref="AllowedConfigurationKeyPrefixes"/>;
    /// a secret prefix that is not also allowed is simply never resolvable. Matching is
    /// segment-aware and case-insensitive, as for the allow-list.
    /// </para>
    /// </remarks>
    public IList<string> SecretConfigurationKeyPrefixes { get; set; } = new List<string>
    {
        "Umbraco:Automate:Secrets",
    };
}

/// <summary>
/// Configuration options for the webhook endpoint.
/// Bound to <c>Umbraco:Automate:Webhook</c> in appsettings.json.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed webhook payload size in bytes.
    /// Requests exceeding this limit are rejected with <c>413 Payload Too Large</c>.
    /// Default: 1 MB.
    /// </summary>
    public long MaxPayloadBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the maximum number of webhook requests per automation per minute.
    /// Requests exceeding this limit are rejected with <c>429 Too Many Requests</c>.
    /// Default: 100.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 100;
}

/// <summary>
/// Configuration options for automation execution.
/// Bound to <c>Umbraco:Automate:Execution</c> in appsettings.json.
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// Gets or sets the default timeout per step.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default maximum number of retries for a failed step when the
    /// step's own <see cref="StepConfiguration.MaxRetries"/> is not set.
    /// Prevents a transient failure from retrying indefinitely.
    /// </summary>
    public int DefaultMaxRetries { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default retry interval for a failed step when the step's own
    /// <see cref="StepConfiguration.RetryInterval"/> is not set. Without this, WorkflowCore
    /// falls back to its built-in 60-second default.
    /// </summary>
    public TimeSpan DefaultRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of concurrent runs.
    /// </summary>
    public int MaxConcurrentRuns { get; set; } = 10;

    /// <summary>
    /// Gets or sets the WorkflowCore poll interval.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum automation chain depth. When an event carries a chain
    /// depth at or above this limit, the dispatcher drops it instead of starting another
    /// run. Hard backstop against runaway cascades; per-trigger
    /// <c>SkipAutomationOriginatedEvents</c> is the first line of defence.
    /// </summary>
    public int MaxChainDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum size, in UTF-8 encoded bytes, of a step's serialized output
    /// that is inlined into the WorkflowCore workflow data (which is re-serialized on every
    /// execution pass). Larger outputs are stored only on the step run record — written once —
    /// and referenced from the workflow data, hydrated on demand when a binding reads them.
    /// Default: 16 KB.
    /// </summary>
    public int MaxInlineOutputBytes { get; set; } = 16_384;

    /// <summary>
    /// Gets or sets the maximum HTTP response body size, in bytes, accepted by the built-in
    /// HTTP Request action. Responses exceeding the limit fail the step with an actionable
    /// error instead of flooding run storage with megabytes of payload. Default: 10 MB.
    /// </summary>
    public long MaxHttpResponseBodyBytes { get; set; } = 10_485_760;

    /// <summary>
    /// Gets or sets the execution mode for workflow processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ExecutionMode.SchedulerOnly"/> (default): Only the <c>SchedulingPublisher</c>
    /// (or <c>Single</c>) node consumes trigger events and executes workflows. Other nodes
    /// capture notifications and dispatch them to the message bus, but do not process them.
    /// This is safe with in-memory WorkflowCore persistence.
    /// </para>
    /// <para>
    /// <see cref="ExecutionMode.Distributed"/>: All nodes participate in consuming trigger
    /// events and executing workflow steps. Requires a shared WorkflowCore persistence provider
    /// (e.g. EF Core backed by SQL Server) and a real message transport (e.g. RabbitMQ).
    /// </para>
    /// </remarks>
    public ExecutionMode Mode { get; set; } = ExecutionMode.SchedulerOnly;
}

/// <summary>
/// Controls which nodes participate in workflow execution.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Only the SchedulingPublisher (or Single) node processes workflows.
    /// Safe with in-memory persistence.
    /// </summary>
    SchedulerOnly,

    /// <summary>
    /// All nodes participate in workflow execution via competing consumers.
    /// Requires shared persistence and a real message transport.
    /// </summary>
    Distributed,
}

/// <summary>
/// Configuration options for governance and audit features.
/// Bound to <c>Umbraco:Automate:Governance</c> in appsettings.json.
/// </summary>
public sealed class GovernanceOptions
{
    /// <summary>
    /// Gets or sets whether the audit log is enabled.
    /// </summary>
    public bool AuditLogEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of days to retain audit log data.
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets whether sensitive data is masked in run logs.
    /// </summary>
    public bool SensitiveDataMasking { get; set; } = true;

    /// <summary>
    /// Gets or sets the default notification policy for new automations.
    /// </summary>
    public NotifyOn DefaultNotifyOn { get; set; } = NotifyOn.Failed;
}

/// <summary>
/// Configuration options for the scheduled trigger background job.
/// Bound to <c>Umbraco:Automate:ScheduledTrigger</c> in appsettings.json.
/// </summary>
public sealed class ScheduledTriggerOptions
{
    /// <summary>
    /// Gets or sets the poll interval for checking scheduled triggers.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the startup delay before the first poll.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the maximum jitter applied to scheduled triggers configured as
    /// <see cref="ScheduleTiming.Flexible"/>. Each automation gets a deterministic
    /// per-id offset within <c>[0, MaxJitter]</c>, spreading load when many automations
    /// share a CRON tick. Set to <see cref="TimeSpan.Zero"/> to disable jitter entirely.
    /// </summary>
    public TimeSpan MaxJitter { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Configuration options for the circuit breaker that auto-disables a "sick" automation
/// (one failing on every trigger due to misconfiguration).
/// Bound to <c>Umbraco:Automate:CircuitBreaker</c> in appsettings.json.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets whether the circuit breaker is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of consecutive failures that issues a degradation warning
    /// (transitions to <see cref="AutomationHealth.Degraded"/>). Set lower than
    /// <see cref="ConsecutiveFailureThreshold"/> so the warning is reachable in the common
    /// "keeps failing" path before the disable threshold trips.
    /// </summary>
    public int ConsecutiveWarningThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the number of consecutive failures before an automation is disabled.
    /// </summary>
    public int ConsecutiveFailureThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the error rate (0.0–1.0) over the evaluation window that triggers a warning.
    /// </summary>
    public double WarningErrorRate { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the error rate (0.0–1.0) over the evaluation window that triggers auto-disable.
    /// </summary>
    public double DisableErrorRate { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the number of recent terminal runs evaluated for the error-rate calculation.
    /// Error-rate thresholds only apply once at least this many terminal runs exist.
    /// </summary>
    public int EvaluationWindowSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the grace period after a warning before auto-disable can trigger,
    /// giving the owner time to investigate and fix.
    /// </summary>
    public TimeSpan GracePeriodAfterWarning { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets whether a manual "run now" / replay is allowed while the circuit is open,
    /// so the owner can test a fix. The run is a pure test — success does NOT auto re-enable;
    /// the owner must re-enable explicitly.
    /// </summary>
    public bool AllowManualRunWhileDisabled { get; set; } = true;
}
