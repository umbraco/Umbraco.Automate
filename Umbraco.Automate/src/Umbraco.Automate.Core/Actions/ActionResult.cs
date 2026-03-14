namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// The result of executing an action.
/// </summary>
public sealed class ActionResult
{
    private ActionResult(
        ActionResultStatus status,
        object? outputData,
        Exception? exception,
        StepRunErrorCategory? errorCategory,
        string? reason,
        string? waitEventName = null,
        string? waitEventKey = null,
        string? outcome = null)
    {
        Status = status;
        OutputData = outputData;
        Exception = exception;
        ErrorCategory = errorCategory;
        Reason = reason;
        WaitEventName = waitEventName;
        WaitEventKey = waitEventKey;
        Outcome = outcome;
    }

    /// <summary>
    /// Gets the result status.
    /// </summary>
    public ActionResultStatus Status { get; }

    /// <summary>
    /// Gets the output data produced by the action, if any.
    /// </summary>
    public object? OutputData { get; }

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the error category for failed results.
    /// </summary>
    public StepRunErrorCategory? ErrorCategory { get; }

    /// <summary>
    /// Gets the reason for skipped results.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Gets the event name to wait for (only set when <see cref="Status"/> is <see cref="ActionResultStatus.WaitingForInput"/>).
    /// </summary>
    public string? WaitEventName { get; }

    /// <summary>
    /// Gets the event key to wait for (only set when <see cref="Status"/> is <see cref="ActionResultStatus.WaitingForInput"/>).
    /// </summary>
    public string? WaitEventKey { get; }

    /// <summary>
    /// Gets the named outcome for branching actions (e.g. "true"/"false" for If, case value for Switch).
    /// When null, the step follows the default (sequential) transition.
    /// </summary>
    public string? Outcome { get; }

    /// <summary>
    /// Creates a successful result with optional output data.
    /// </summary>
    public static ActionResult Success(object? outputData = null)
        => new(ActionResultStatus.Success, outputData, null, null, null);

    /// <summary>
    /// Creates a successful result with a named outcome for branching.
    /// </summary>
    /// <param name="outcome">The named outcome (e.g. "true", "false", or a switch case value).</param>
    /// <param name="outputData">Optional output data.</param>
    public static ActionResult SuccessWithOutcome(string outcome, object? outputData = null)
        => new(ActionResultStatus.Success, outputData, null, null, null, outcome: outcome);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ActionResult Failed(Exception exception, StepRunErrorCategory category = StepRunErrorCategory.Unknown)
        => new(ActionResultStatus.Failed, null, exception, category, null);

    /// <summary>
    /// Creates a skipped result with an optional reason.
    /// </summary>
    public static ActionResult Skipped(string? reason = null)
        => new(ActionResultStatus.Skipped, null, null, null, reason);

    /// <summary>
    /// Creates a result that suspends the workflow until the specified event is received.
    /// </summary>
    /// <param name="eventName">The WorkflowCore event name to wait for.</param>
    /// <param name="eventKey">The WorkflowCore event key to wait for.</param>
    /// <param name="outputData">Optional output data to store before waiting.</param>
    public static ActionResult WaitForInput(string eventName, string eventKey, object? outputData = null)
        => new(ActionResultStatus.WaitingForInput, outputData, null, null, null, eventName, eventKey);
}

/// <summary>
/// The status of an action execution result.
/// </summary>
public enum ActionResultStatus
{
    /// <summary>The action completed successfully.</summary>
    Success = 0,

    /// <summary>The action failed.</summary>
    Failed = 1,

    /// <summary>The action was skipped.</summary>
    Skipped = 2,

    /// <summary>The action is waiting for external input (e.g. approval).</summary>
    WaitingForInput = 3,
}

/// <summary>
/// Categorises the error that caused a step run to fail.
/// </summary>
public enum StepRunErrorCategory
{
    /// <summary>Unknown or unclassified error.</summary>
    Unknown = 0,

    /// <summary>Input validation failed.</summary>
    Validation = 1,

    /// <summary>Authentication or authorisation error.</summary>
    Authentication = 2,

    /// <summary>Rate limit exceeded.</summary>
    RateLimiting = 3,

    /// <summary>Operation timed out.</summary>
    Timeout = 4,

    /// <summary>External service is unavailable.</summary>
    ServiceUnavailable = 5,

    /// <summary>Received an invalid or unexpected response.</summary>
    InvalidResponse = 6,

    /// <summary>Operation was cancelled.</summary>
    Cancelled = 7,

    /// <summary>Configuration or settings error.</summary>
    ConfigurationError = 8,
}
