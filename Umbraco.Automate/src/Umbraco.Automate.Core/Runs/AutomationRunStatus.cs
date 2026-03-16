namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The execution status of an automation run.
/// </summary>
public enum AutomationRunStatus
{
    /// <summary>Run is queued but not yet started.</summary>
    Pending = 0,

    /// <summary>Run is currently executing.</summary>
    Running = 1,

    /// <summary>Run completed successfully.</summary>
    Completed = 2,

    /// <summary>Run failed due to an error.</summary>
    Failed = 3,

    /// <summary>Run is suspended waiting for input (e.g. approval).</summary>
    Suspended = 4,

    /// <summary>Run was cancelled.</summary>
    Cancelled = 5,
}

/// <summary>
/// The execution status of a single step within a run.
/// </summary>
public enum StepRunStatus
{
    /// <summary>Step is queued but not yet started.</summary>
    Pending = 0,

    /// <summary>Step is currently executing.</summary>
    Running = 1,

    /// <summary>Step completed successfully.</summary>
    Completed = 2,

    /// <summary>Step failed due to an error.</summary>
    Failed = 3,

    /// <summary>Step was skipped.</summary>
    Skipped = 4,

    /// <summary>Step is waiting for external input (e.g. approval).</summary>
    WaitingForInput = 5,

    /// <summary>Step is durably sleeping for a configured duration.</summary>
    Sleeping = 6,
}
