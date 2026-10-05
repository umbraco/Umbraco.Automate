using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore step body that executes an Umbraco Automate action within the middleware pipeline.
/// One instance is created per step in the compiled workflow.
/// </summary>
internal sealed class ActionStepBody : StepBodyAsync
{
    private readonly StepConfiguration _stepConfig;
    private readonly IAction _action;
    private readonly ActionMiddlewarePipeline _pipeline;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly ForEachCollectionCache _collectionCache;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly SettingsBindingResolver _settingsBindingResolver;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IConnectionService _connectionService;
    private readonly IStepErrorClassifier _errorClassifier;
    private readonly IOptions<ExecutionOptions> _executionOptions;
    private readonly AutomateMetrics _metrics;
    private readonly IEventAggregator _eventAggregator;
    private readonly ILogger<ActionStepBody> _logger;

    public ActionStepBody(
        StepConfiguration stepConfig,
        IAction action,
        ActionMiddlewarePipeline pipeline,
        BindingEvaluator bindingEvaluator,
        ForEachCollectionCache collectionCache,
        StepOutputHydrationCache hydrationCache,
        SettingsBindingResolver settingsBindingResolver,
        IAutomationRunRepository runRepository,
        IConnectionService connectionService,
        IStepErrorClassifier errorClassifier,
        IOptions<ExecutionOptions> executionOptions,
        AutomateMetrics metrics,
        IEventAggregator eventAggregator,
        ILogger<ActionStepBody> logger)
    {
        _stepConfig = stepConfig;
        _action = action;
        _pipeline = pipeline;
        _bindingEvaluator = bindingEvaluator;
        _collectionCache = collectionCache;
        _hydrationCache = hydrationCache;
        _settingsBindingResolver = settingsBindingResolver;
        _runRepository = runRepository;
        _connectionService = connectionService;
        _errorClassifier = errorClassifier;
        _executionOptions = executionOptions;
        _metrics = metrics;
        _eventAggregator = eventAggregator;
        _logger = logger;
    }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var cancellationToken = context.CancellationToken;

        // Resume path: workflow is resuming after Sleep with persisted data.
        if (context.PersistenceData is SleepPersistenceData sleepData)
        {
            return await HandleSleepResumeAsync(sleepData, data, cancellationToken);
        }

        // Resume path: workflow is resuming after WaitForEvent with the event data.
        // WorkflowCore's SeedSubscription sets EventData and EventPublished on the pointer.
        // context.Item (pointer.ContextItem) is NOT populated — event data lives on pointer.EventData.
        if (context.ExecutionPointer.EventPublished && context.ExecutionPointer.EventData is not null)
        {
            return await HandleResumeAsync(context, data, cancellationToken);
        }

        // Normal execution path.
        return await HandleExecuteAsync(context, data, cancellationToken);
    }

    private async Task<ExecutionResult> HandleExecuteAsync(
        IStepExecutionContext context,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        // Build binding data context: trigger output + all prior step outputs + loop iteration.
        var iterationContext = context.Item as ForEachIterationContext;
        var bindingData = BindingDataBuilder.Build(data, iterationContext, _collectionCache, _hydrationCache, cancellationToken);

        // Setup phase — resolve inputs, settings, bindings, and connections before we
        // invoke the middleware pipeline. These operations can throw on misconfiguration
        // (bad bindings, missing required settings, invalid connection reference). If we
        // let them escape, WorkflowCore will retry the step body indefinitely. Catch them
        // here, classify, and turn them into a properly-recorded step failure.
        Dictionary<string, object?> resolvedInputs;
        object? settings;
        ConfiguredConnection? connection;

        try
        {
            resolvedInputs = ResolveInputMappings(_stepConfig.InputMappings, bindingData);

            settings = null;
            if (_action.SettingsType is not null && _stepConfig.Settings.Count > 0)
            {
                settings = _action.ResolveSettings(_stepConfig.Settings);
            }

            if (settings is not null)
            {
                _settingsBindingResolver.ResolveBindings(settings, bindingData);
            }

            // Resolve connection for this step.
            // Priority: explicit step connectionId > auto-resolve by action's connection type alias.
            connection = null;
            if (_stepConfig.ConnectionId is { } connectionId)
            {
                connection = await _connectionService.GetConfiguredConnectionAsync(connectionId, cancellationToken);
            }
            else if (_action.ConnectionTypeAlias is { } typeAlias)
            {
                connection = await ResolveConnectionByTypeAsync(typeAlias, data.ExecutionContext, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await HandleSetupFailureAsync(ex, data, context, cancellationToken);
        }

        // Create a linked CancellationTokenSource that enforces the step timeout.
        // If the workflow-level token is cancelled, this will also cancel.
        var stepTimeout = _executionOptions.Value.DefaultTimeout;
        using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stepCts.CancelAfter(stepTimeout);
        var stepCancellationToken = stepCts.Token;

        // Create action context.
        var actionContext = new ActionContext
        {
            AutomationId = data.AutomationId,
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _action.Alias,
            Settings = settings,
            InputData = resolvedInputs,
            CancellationToken = stepCancellationToken,
            ExecutionContext = data.ExecutionContext,
            Connection = connection,
            Action = _action,
            BindingData = bindingData,
            MinimumLogLevel = _executionOptions.Value.MinimumLogLevel,
            MaxLogEntries = _executionOptions.Value.MaxLogEntriesPerStep,
        };

        var (inputJson, sensitiveValues) = RecordStepInput(settings, resolvedInputs);

        // Create and persist step run.
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _action.Alias,
            Status = StepRunStatus.Running,
            StartedUtc = DateTime.UtcNow,
            InputData = inputJson,
        };

        await _runRepository.AddStepRunAsync(stepRun, cancellationToken);

        // Execute through the middleware pipeline with the timeout-linked token.
        var result = await _pipeline.ExecuteAsync(_action, actionContext, stepCancellationToken);

        // Map what the action returned onto the exit the run follows. Done before the log entries
        // are captured so a warning about an undeclared outcome lands in the step's run log. A
        // broken declaration or a missing outcome turns the result into a Validation failure, which
        // the Failed handling below records and (being a terminal category) never retries.
        var routedOutcome = result.Outcome;
        if (result.Status == ActionResultStatus.Success && result.Suspension is null)
        {
            var routing = await ResolveOutcomeRoutingAsync(result.Outcome, cancellationToken);
            if (routing.FailureMessage is not null)
            {
                result = ActionResult.Failed(new ValidationException(routing.FailureMessage), StepRunErrorCategory.Validation);
            }
            else
            {
                routedOutcome = routing.Outcome;
                if (routing.UndeclaredOutcomeWarning is not null)
                {
                    actionContext.LogWarning(routing.UndeclaredOutcomeWarning);
                    _logger.LogWarning(
                        "Action '{ActionAlias}' returned outcome '{Outcome}', which it does not declare, for step {StepId}",
                        _action.Alias, routedOutcome, _stepConfig.Id);
                }
            }
        }

        // Capture any log entries the action recorded, regardless of outcome. Reads from
        // the same ActionContext instance the pipeline just executed — ErrorHandlingMiddleware
        // catches exceptions on this same context, so entries recorded before a throw survive
        // even for a Failed result. Every branch below ends in an UpdateStepRunAsync call, so
        // this rides along with no extra DB round trip. Sensitive setting values an action wrote
        // into a message are redacted before storing, as they are in the recorded input.
        stepRun.LogEntries = ActionLogSanitizer.Sanitize(actionContext.LogEntries, sensitiveValues);

        // Handle suspension: the action needs the workflow to pause.
        switch (result.Suspension)
        {
            case ActionSuspension.WaitForEvent wait:
                // Take the subscription's start time before the step is shown as waiting.
                // WorkflowCore only delivers an event published at or after this time, and
                // callers (an approver, a test) publish as soon as they see WaitingForInput;
                // a timestamp taken after the write could post-date that event and drop it.
                var subscribeAsOf = DateTime.UtcNow;

                stepRun.Status = StepRunStatus.WaitingForInput;
                StoreOutputData(result.OutputData, stepRun, data, iterationContext);
                await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);
                await SuspendRunForInputAsync(data, cancellationToken);

                _logger.LogInformation(
                    "Step {StepId} is waiting for input (event: {EventName}/{EventKey})",
                    _stepConfig.Id, wait.EventName, wait.EventKey);

                return ExecutionResult.WaitForEvent(wait.EventName, wait.EventKey, subscribeAsOf);

            case ActionSuspension.Sleep sleep:
                stepRun.Status = StepRunStatus.Sleeping;
                StoreOutputData(result.OutputData, stepRun, data, iterationContext);
                await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);

                _logger.LogInformation(
                    "Step {StepId} is sleeping for {Duration}",
                    _stepConfig.Id, sleep.Duration);

                var persistenceData = new SleepPersistenceData
                {
                    StepRunId = stepRun.Id,
                    RunId = data.RunId,
                    StepId = _stepConfig.Id,
                };

                return ExecutionResult.Sleep(sleep.Duration, persistenceData);
        }

        // Update step run with result.
        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        switch (result.Status)
        {
            case ActionResultStatus.Success:
                stepRun.Status = StepRunStatus.Completed;
                stepRun.BranchOutcome = routedOutcome;
                StoreOutputData(result.OutputData, stepRun, data, iterationContext);
                _metrics.StepExecuted(_action.Alias);
                break;

            case ActionResultStatus.Failed:
                stepRun.Status = StepRunStatus.Failed;
                stepRun.Error = result.Exception?.Message;
                stepRun.ErrorCategory = result.ErrorCategory;
                _metrics.StepFailed(_action.Alias);
                break;

            case ActionResultStatus.Skipped:
                stepRun.Status = StepRunStatus.Skipped;
                break;
        }

        if (stepRun.Duration.HasValue)
        {
            _metrics.RecordStepDuration(stepRun.Duration.Value.TotalMilliseconds, _action.Alias);
        }

        await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);

        // Pipeline-caught failures: decide retry/terminate/skip based on the configured
        // ErrorBehavior (applied via WorkflowCore on the WorkflowStep) and the classifier.
        if (result.Status == ActionResultStatus.Failed)
        {
            var exception = result.Exception ?? new InvalidOperationException($"Step '{_stepConfig.Name}' failed.");
            return DecideFailureOutcome(exception, result.ErrorCategory ?? StepRunErrorCategory.Unknown, context);
        }

        // Route via WorkflowCore's outcome matching: the action's own outcome, or the default
        // exit it declared when it returned none.
        if (result.Status == ActionResultStatus.Success && routedOutcome is not null)
        {
            return ExecutionResult.Outcome(routedOutcome);
        }

        return ExecutionResult.Next();
    }

    /// <summary>
    /// Works out which exit a successful action result follows, from the step's saved, unbound
    /// settings (never the binding-resolved settings the action ran with) so run time agrees with
    /// what the author published. The rule itself lives in <see cref="StepOutcomeRouter"/>.
    /// </summary>
    private async Task<StepOutcomeRouting> ResolveOutcomeRoutingAsync(string? returnedOutcome, CancellationToken cancellationToken)
    {
        var routing = await StepOutcomeRouter.ResolveAsync(_action, _stepConfig.Settings, returnedOutcome, cancellationToken);
        if (routing.Exception is not null)
        {
            _logger.LogError(routing.Exception, "Action '{ActionAlias}' could not list its outcomes for step {StepId}", _action.Alias, _stepConfig.Id);
        }

        return routing;
    }

    /// <summary>
    /// Decides how to surface a step failure to WorkflowCore. The step's
    /// <see cref="StepErrorBehavior"/> is wired onto the <c>WorkflowStep</c> at compile
    /// time, so WorkflowCore honors retry/suspend/terminate/compensate when we throw.
    /// Terminate and Suspend always throw — the whole point of those modes is to halt
    /// the run regardless of retry budget or error category. Only the Retry path applies
    /// the terminal-category and retry-budget guards, short-circuiting with
    /// <see cref="ExecutionResult.Next"/> when retrying cannot help.
    /// </summary>
    private ExecutionResult DecideFailureOutcome(
        Exception exception,
        StepRunErrorCategory category,
        IStepExecutionContext context)
    {
        // Terminate always aborts the workflow — WorkflowCore's Terminate handler stops
        // execution and does not retry, because we set the step's ErrorBehavior.
        if (_stepConfig.ErrorBehavior == StepErrorBehavior.Terminate)
        {
            _logger.LogError("Step {StepId} failed with Terminate behavior, aborting workflow", _stepConfig.Id);
            throw exception;
        }

        // Suspend pauses the workflow for manual intervention — throw unconditionally so
        // WorkflowCore applies the Suspend handler. The retry-budget and terminal-category
        // guards below intentionally do not apply: the whole point of Suspend is that a
        // human fixes the underlying issue (e.g. bad config, missing credentials) before
        // resuming, which is exactly the case where retry cannot help.
        if (_stepConfig.ErrorBehavior == StepErrorBehavior.Suspend)
        {
            _logger.LogError(
                "Step {StepId} failed with Suspend behavior ({Category}) — suspending workflow",
                _stepConfig.Id, category);
            throw exception;
        }

        // Terminal category + Retry behavior: retrying cannot change the outcome
        // (bad settings, missing auth, etc.). Skip past the failed step without retry.
        if (_errorClassifier.IsTerminal(category))
        {
            _logger.LogError(
                "Step {StepId} failed with terminal category {Category} — skipping without retry",
                _stepConfig.Id, category);
            return ExecutionResult.Next();
        }

        // Transient failure. Cap retries at the step's configured MaxRetries, falling back
        // to ExecutionOptions.DefaultMaxRetries so we don't loop forever on the same step.
        var maxRetries = _stepConfig.MaxRetries ?? _executionOptions.Value.DefaultMaxRetries;
        if (context.ExecutionPointer.RetryCount >= maxRetries)
        {
            _logger.LogError(
                "Step {StepId} exhausted retry budget ({MaxRetries}) — skipping past failure",
                _stepConfig.Id, maxRetries);
            return ExecutionResult.Next();
        }

        // Throw so WorkflowCore applies the configured Retry behavior (with interval) via
        // the step-level settings set at compile time.
        throw exception;
    }

    /// <summary>
    /// Handles an exception raised during the pre-pipeline setup phase (input mapping,
    /// settings resolution, connection resolution). These exceptions don't flow through
    /// <see cref="ErrorHandlingMiddleware"/>, so we classify them here, record the step
    /// run, and route the failure through the same decision logic as pipeline failures.
    /// </summary>
    private async Task<ExecutionResult> HandleSetupFailureAsync(
        Exception exception,
        AutomationWorkflowData data,
        IStepExecutionContext context,
        CancellationToken cancellationToken)
    {
        var category = _errorClassifier.Classify(exception);

        _logger.LogError(
            exception,
            "Step {StepId} setup failed ({Category}) for action '{ActionAlias}' in run {RunId}",
            _stepConfig.Id, category, _action.Alias, data.RunId);

        var now = DateTime.UtcNow;
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _action.Alias,
            Status = StepRunStatus.Failed,
            StartedUtc = now,
            CompletedUtc = now,
            Duration = TimeSpan.Zero,
            Error = exception.Message,
            ErrorCategory = category,
        };

        await _runRepository.AddStepRunAsync(stepRun, cancellationToken);
        _metrics.StepFailed(_action.Alias);

        return DecideFailureOutcome(exception, category, context);
    }

    /// <summary>
    /// Marks the run Suspended while a step waits for input. WorkflowCore's WaitForEvent only parks
    /// the execution pointer — the workflow itself stays Runnable — so RunFinalizer's
    /// WorkflowStatus.Suspended sync never sees this pause and the run would otherwise read as
    /// Running for as long as the approval is outstanding.
    /// </summary>
    private async Task SuspendRunForInputAsync(AutomationWorkflowData data, CancellationToken cancellationToken)
    {
        var run = await _runRepository.GetAsync(data.RunId, cancellationToken);

        // Only Running → Suspended; a run already Suspended (another parallel branch is waiting too)
        // or finished is left alone, so the Suspended notification fires once per pause.
        if (run is null || run.Status is not AutomationRunStatus.Running)
        {
            return;
        }

        run.Status = AutomationRunStatus.Suspended;
        await _runRepository.SaveAsync(run, cancellationToken);

        // The dispatcher handles NotifyOn.Suspended via this notification, as it does for RunFinalizer.
        await _eventAggregator.PublishAsync(
            new AutomationRunCompletedNotification(run, new EventMessages()),
            cancellationToken);
    }

    private async Task<ExecutionResult> HandleResumeAsync(
        IStepExecutionContext context,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step {StepId} resumed with event data", _stepConfig.Id);

        // Deserialize the decision from the execution pointer's event data.
        // WorkflowCore's SeedSubscription sets EventData on the pointer, not ContextItem.
        var eventData = context.ExecutionPointer.EventData;
        ApprovalDecision? decision = null;
        if (eventData is JsonElement jsonElement)
        {
            decision = JsonSerializer.Deserialize<ApprovalDecision>(
                jsonElement.GetRawText(), Dispatch.JsonOptions.Default);
        }
        else if (eventData is ApprovalDecision directDecision)
        {
            decision = directDecision;
        }
        else if (eventData is Newtonsoft.Json.Linq.JObject jObject)
        {
            decision = jObject.ToObject<ApprovalDecision>();
        }

        // Find the existing step run for this step.
        var run = await _runRepository.GetAsync(data.RunId, cancellationToken);
        var stepRun = run?.StepRuns.FirstOrDefault(sr => sr.StepId == _stepConfig.Id && sr.Status == StepRunStatus.WaitingForInput);

        if (stepRun is null)
        {
            _logger.LogWarning("No WaitingForInput step run found for step {StepId} in run {RunId}", _stepConfig.Id, data.RunId);
            return ExecutionResult.Next();
        }

        // The run was marked Suspended when this step started waiting (SuspendRunForInputAsync);
        // bring it back to Running now that the event has fired.
        if (run is not null && run.Status == AutomationRunStatus.Suspended)
        {
            run.Status = AutomationRunStatus.Running;
            await _runRepository.SaveAsync(run, cancellationToken);

            await _eventAggregator.PublishAsync(
                new AutomationRunResumedNotification(run, new EventMessages()),
                cancellationToken);
        }

        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        // Store the decision as step output.
        if (decision is not null)
        {
            var outputData = new ApprovalDecisionOutput
            {
                Approved = decision.Outcome == ApprovalOutcome.Approved,

                // Name, not the enum value: JsonOptions.Default has no string-enum converter, so
                // an ApprovalOutcome here would bind as 0/1 and force conditions to compare
                // against a magic number instead of "Approved"/"Rejected".
                Outcome = decision.Outcome.ToString(),
                Comment = decision.Comment,
                ApprovedByUserKey = decision.ApprovedByUserKey,
                DecisionUtc = decision.DecisionUtc,
            };
            StoreOutputData(outputData, stepRun, data, context.Item as ForEachIterationContext);
        }

        if (stepRun.Duration.HasValue)
        {
            _metrics.RecordStepDuration(stepRun.Duration.Value.TotalMilliseconds, _action.Alias);
        }

        // A rejection is a designed path, not a failure: the step is terminal either way and the
        // decision picks the outcome, so an author can wire the approved and rejected handles to
        // different steps without an intervening If. Automations predating those handles have a
        // single unlabelled edge, which the compiler wires with a null outcome value that matches
        // whichever outcome is returned — see ApprovalOutcomeTests for the guard on that.
        //
        // A refusal is recorded as Rejected rather than Completed so RunFinalizer can mark the run
        // Rejected without re-reading step output. Both count as an executed step: the step did its
        // job, which was to obtain a decision.
        if (decision is not null)
        {
            var approved = decision.Outcome == ApprovalOutcome.Approved;

            stepRun.Status = approved ? StepRunStatus.Completed : StepRunStatus.Rejected;
            await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);
            _metrics.StepExecuted(_action.Alias);

            return ExecutionResult.Outcome(approved
                ? RequestApprovalAction.ApprovedOutcome
                : RequestApprovalAction.RejectedOutcome);
        }

        // No decision on the event — the step was resumed by something that is not an approval
        // submission. That is a genuine error rather than an outcome.
        //
        // Categorised as a configuration error, not as Cancelled: nothing was cancelled, and this is
        // the category DefaultStepErrorClassifier already gives the InvalidOperationException thrown
        // just below. Both are terminal, so retry behaviour is unchanged — a resume payload that was
        // not a decision will not become one on a second attempt.
        stepRun.Status = StepRunStatus.Failed;
        _metrics.StepFailed(_action.Alias);
        stepRun.Error = "Approval step resumed without a valid decision";
        stepRun.ErrorCategory = StepRunErrorCategory.ConfigurationError;
        await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);

        if (_stepConfig.ErrorBehavior == StepErrorBehavior.Terminate)
        {
            throw new InvalidOperationException(stepRun.Error);
        }

        return ExecutionResult.Next();
    }

    private async Task<ExecutionResult> HandleSleepResumeAsync(
        SleepPersistenceData sleepData,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step {StepId} resumed after sleep", sleepData.StepId);

        var run = await _runRepository.GetAsync(sleepData.RunId, cancellationToken);
        var stepRun = run?.StepRuns.FirstOrDefault(sr => sr.Id == sleepData.StepRunId && sr.Status == StepRunStatus.Sleeping);

        if (stepRun is null)
        {
            _logger.LogWarning("No Sleeping step run found for step {StepId} in run {RunId}", sleepData.StepId, sleepData.RunId);
            return ExecutionResult.Next();
        }

        stepRun.Status = StepRunStatus.Completed;
        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        if (stepRun.Duration.HasValue)
        {
            _metrics.RecordStepDuration(stepRun.Duration.Value.TotalMilliseconds, _action.Alias);
        }

        _metrics.StepExecuted(_action.Alias);
        await _runRepository.UpdateStepRunAsync(stepRun, cancellationToken);

        return ExecutionResult.Next();
    }

    private void StoreOutputData(
        object? outputData,
        StepRun stepRun,
        AutomationWorkflowData data,
        ForEachIterationContext? iterationContext)
    {
        if (outputData is null)
        {
            return;
        }

        var outputJson = JsonSerializer.Serialize(outputData, Dispatch.JsonOptions.Default);
        stepRun.OutputData = outputJson;

        // Small outputs are deserialized to a case-insensitive dictionary with plain .NET
        // types (not JsonElement) so values survive the WorkflowCore Newtonsoft.Json
        // persistence round-trip and are accessible to BindingEvaluator.ResolvePath.
        // Large outputs would be re-serialized into the workflow instance blob on every
        // execution pass, so only a marker referencing the step run (whose OutputData above
        // is written once) goes into the workflow data — binding evaluation hydrates it on
        // demand via StepOutputHydrationCache.
        var unwrapped = StepOutputReference.CreateInlineOrMarker(
            outputJson, stepRun.Id, _executionOptions.Value.MaxInlineOutputBytes);

        // Write to the run-global table so steps after the loop (and external observers)
        // can still read the most recent value. Inside an iteration the global entry is
        // last-write-wins, which is fine — body steps resolve through the iteration scope.
        data.StepOutputs[_stepConfig.Id] = unwrapped;
        data.LastCompletedStepId = _stepConfig.Id;

        // Inside a ForEach/While/Parallel iteration, also record the output under the
        // iteration's scope path so siblings further down the body chain can read this
        // iteration's output without being clobbered by later iterations re-running the
        // same step.
        if (iterationContext is not null)
        {
            var scopePath = iterationContext.ScopePath;
            if (!data.IterationStepOutputs.TryGetValue(scopePath, out var iterOutputs))
            {
                iterOutputs = [];
                data.IterationStepOutputs[scopePath] = iterOutputs;
            }
            iterOutputs[_stepConfig.Id] = unwrapped;
            data.IterationLastCompletedStepId[scopePath] = _stepConfig.Id;
        }
    }

    private async Task<ConfiguredConnection?> ResolveConnectionByTypeAsync(
        string connectionTypeAlias,
        AutomationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var allConfigured = await _connectionService.GetConfiguredConnectionsByIdsAsync(
            executionContext.AllowedConnections, cancellationToken);

        var matches = allConfigured
            .Where(c => string.Equals(c.Type, connectionTypeAlias, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            _logger.LogWarning(
                "No connection of type '{ConnectionTypeAlias}' found in workspace '{WorkspaceId}' for step {StepId}",
                connectionTypeAlias, executionContext.WorkspaceId, _stepConfig.Id);

            return null;
        }

        if (matches.Count > 1)
        {
            _logger.LogWarning(
                "Multiple connections of type '{ConnectionTypeAlias}' found in workspace '{WorkspaceId}' for step {StepId}. Using first match '{ConnectionId}'",
                connectionTypeAlias, executionContext.WorkspaceId, _stepConfig.Id, matches[0].Id);
        }

        return matches[0];
    }

    /// <summary>
    /// Serializes what the step received — its settings after bindings and configuration
    /// references were resolved (or, for a step without settings, its resolved input mappings) —
    /// for the run view. Sensitive values are masked <em>before</em> the payload is stored, so a
    /// resolved secret never lands in the step run record; the run data API masks again on read.
    /// Also returns the values that were masked, so the same values can be redacted from the
    /// step's log entries. Recording the input is diagnostic only: a failure here is logged and
    /// never fails the step.
    /// </summary>
    private (string? Json, IReadOnlyCollection<string> SensitiveValues) RecordStepInput(
        object? settings,
        Dictionary<string, object?> resolvedInputs)
    {
        object? input = settings ?? (resolvedInputs.Count > 0 ? resolvedInputs : null);
        if (input is null)
        {
            return (null, []);
        }

        try
        {
            var node = JsonSerializer.SerializeToNode(input, input.GetType(), Dispatch.JsonOptions.Settings);
            var sensitiveFieldKeys = SensitiveDataMasker.GetSensitiveFieldKeys(_action.GetSettingsSchema());
            var sensitiveValues = ActionLogSanitizer.CollectSensitiveValues(node, sensitiveFieldKeys);
            SensitiveDataMasker.Mask(node, sensitiveFieldKeys);
            return (node?.ToJsonString(Dispatch.JsonOptions.Default), sensitiveValues);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not record the input of step {StepId}; the step runs regardless", _stepConfig.Id);
            return (null, []);
        }
    }

    private Dictionary<string, object?> ResolveInputMappings(
        Dictionary<string, string> inputMappings,
        Dictionary<string, object?> bindingData)
    {
        var resolved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, binding) in inputMappings)
        {
            resolved[key] = _bindingEvaluator.Evaluate(binding, bindingData);
        }

        return resolved;
    }
}
