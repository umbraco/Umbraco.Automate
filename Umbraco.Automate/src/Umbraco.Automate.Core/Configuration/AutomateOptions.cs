using Umbraco.Automate.Core.Automations;

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
}
