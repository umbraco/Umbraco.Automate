using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Services.ErrorHandlers;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore error handler for <see cref="WorkflowErrorHandling.Retry"/> that replaces the
/// engine's own <see cref="RetryHandler"/>. A step failure that retrying cannot fix
/// (<see cref="NonRetryableStepFailureException"/>) is handed to WorkflowCore's
/// <see cref="TerminateHandler"/>; every other exception goes to the stock
/// <see cref="RetryHandler"/>, unchanged.
/// </summary>
/// <remarks>
/// WorkflowCore picks the handler from the failing step's compile-time <c>ErrorBehavior</c> only
/// (<c>ExecutionResultProcessor.HandleStepException</c>), and the workflow definition is shared by
/// every run, so the behaviour cannot be switched to Terminate per failure. Deciding per exception
/// here keeps the termination inside the engine's own error pipeline: the pointer is marked Failed,
/// the execution error is recorded, the step error is reported, and the WorkflowError /
/// WorkflowTerminated lifecycle events are published — exactly as for a step configured as
/// Terminate. Registered by <c>ReplaceWorkflowRetryHandler</c>, which removes the stock handler so
/// only one Retry handler applies.
/// </remarks>
internal sealed class AutomateRetryHandler : IWorkflowErrorHandler
{
    private readonly RetryHandler _retryHandler;
    private readonly TerminateHandler _terminateHandler;

    public AutomateRetryHandler(RetryHandler retryHandler, TerminateHandler terminateHandler)
    {
        _retryHandler = retryHandler;
        _terminateHandler = terminateHandler;
    }

    public WorkflowErrorHandling Type => WorkflowErrorHandling.Retry;

    public void Handle(
        WorkflowInstance workflow,
        WorkflowDefinition def,
        ExecutionPointer pointer,
        WorkflowStep step,
        Exception exception,
        Queue<ExecutionPointer> bubbleUpQueue)
    {
        IWorkflowErrorHandler handler = exception is NonRetryableStepFailureException
            ? _terminateHandler
            : _retryHandler;

        handler.Handle(workflow, def, pointer, step, exception, bubbleUpQueue);
    }
}
