namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Thrown by <see cref="ActionStepBody"/> when a step whose error behaviour is Retry has failed in a
/// way that retrying cannot fix — a terminal error category, or an exhausted retry budget.
/// <see cref="AutomateRetryHandler"/> recognises it and terminates the workflow instead of
/// scheduling another attempt.
/// </summary>
/// <remarks>
/// Carries the step's own error as <see cref="Exception.InnerException"/> and reuses its message, so
/// what WorkflowCore logs and records as the execution error is the step's real error.
/// </remarks>
internal sealed class NonRetryableStepFailureException : Exception
{
    public NonRetryableStepFailureException(Exception stepError)
        : base(stepError.Message, stepError)
    {
    }
}
