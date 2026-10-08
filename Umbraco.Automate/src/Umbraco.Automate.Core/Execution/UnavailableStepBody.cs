using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Stands in for a step whose action or control flow alias can't be resolved when the automation is
/// compiled. This happens when the package that provided it was removed, or it was excluded at compose
/// time (for example behind a feature flag). Running the step records a failed step run with a clear
/// error and stops the run.
/// </summary>
/// <remarks>
/// Leaving the step out of the compiled workflow instead would let the run carry on without it. Later
/// steps would bind to its missing output and could take the wrong branch, and the run would still end
/// Completed. The step's own error behaviour still applies, so under Suspend an operator can restore the
/// package and resume the run. Under Retry the failure is not retried, because a missing action won't
/// come back between attempts.
/// </remarks>
internal sealed class UnavailableStepBody : StepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly IAutomationRunRepository _runRepository;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger _logger;

    public UnavailableStepBody(
        StepConfiguration stepConfig,
        IAutomationRunRepository runRepository,
        AutomateMetrics metrics,
        ILogger logger)
    {
        _stepConfig = stepConfig;
        _runRepository = runRepository;
        _metrics = metrics;
        _logger = logger;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var exception = new InvalidOperationException(
            $"Step '{_stepConfig.Name}' uses '{_stepConfig.ActionAlias}', which is not available. The package that provides it may have been removed or disabled.");

        _logger.LogError(
            "Step {StepId} in run {RunId} uses '{ActionAlias}', which is not registered; failing the step",
            _stepConfig.Id, data.RunId, _stepConfig.ActionAlias);

        var now = DateTime.UtcNow;
        _runRepository.AddStepRunAsync(
            new StepRun
            {
                Id = Guid.NewGuid(),
                RunId = data.RunId,
                StepId = _stepConfig.Id,
                ActionAlias = _stepConfig.ActionAlias,
                Status = StepRunStatus.Failed,
                StartedUtc = now,
                CompletedUtc = now,
                Duration = TimeSpan.Zero,
                Error = exception.Message,
                ErrorCategory = StepRunErrorCategory.ConfigurationError,
            },
            context.CancellationToken).GetAwaiter().GetResult();
        _metrics.StepFailed(_stepConfig.ActionAlias);

        throw _stepConfig.ErrorBehavior == StepErrorBehavior.Retry
            ? new NonRetryableStepFailureException(exception)
            : exception;
    }
}
