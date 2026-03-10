namespace Umbraco.Automate.Core.Configuration;

/// <summary>
/// Top-level configuration options for Umbraco.Automate.
/// Bound to <c>Umbraco:Automate</c> in appsettings.json.
/// </summary>
public sealed class AutomateOptions
{
    /// <summary>
    /// Gets or sets whether the automation engine is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
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
    /// Gets or sets the default retry count for failed steps.
    /// </summary>
    public int DefaultRetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of concurrent runs.
    /// </summary>
    public int MaxConcurrentRuns { get; set; } = 10;

    /// <summary>
    /// Gets or sets the WorkflowCore poll interval.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
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
}
