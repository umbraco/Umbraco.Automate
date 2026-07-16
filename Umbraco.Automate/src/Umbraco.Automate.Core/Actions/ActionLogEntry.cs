namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Severity of a log entry recorded by an action during execution.
/// </summary>
public enum ActionLogLevel
{
    /// <summary>Diagnostic detail, useful when troubleshooting a specific automation.</summary>
    Debug = 0,

    /// <summary>General progress information.</summary>
    Info = 1,

    /// <summary>Something unexpected happened but the step continued.</summary>
    Warning = 2,

    /// <summary>An error occurred.</summary>
    Error = 3,
}

/// <summary>
/// A single leveled log message recorded by an action during execution, persisted with
/// the step run and shown in the run detail UI.
/// </summary>
/// <param name="TimestampUtc">The UTC time the entry was recorded.</param>
/// <param name="Level">The severity of the entry.</param>
/// <param name="Message">The log message text.</param>
public sealed record ActionLogEntry(DateTime TimestampUtc, ActionLogLevel Level, string Message);
