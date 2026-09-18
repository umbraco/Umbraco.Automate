using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that starts another automation, enabling automation chaining.
/// </summary>
/// <remarks>
/// The child run goes through the same execution pipeline as trigger dispatch — run history,
/// rate limiting and the circuit breaker all apply, and the published version snapshot is
/// executed rather than the current draft. Because this imperative path bypasses
/// <c>TriggerEventHandler</c>, the origin-chain cycle and depth guards are enforced here and
/// the extended chain is passed on so grandchildren inherit it.
/// </remarks>
[Action("umbracoAutomate.startAutomation", "Start Automation",
    Description = "Starts another automation from the same workspace, optionally passing along trigger data.",
    Group = "Core",
    Icon = "icon-directions-alt")]
public sealed class StartAutomationAction : ActionBase<StartAutomationSettings, StartAutomationOutput>, IValidatableStepType
{
    private readonly IAutomationService _automationService;
    private readonly IEntityVersionService _versionService;
    private readonly IAutomationExecutor _executor;
    private readonly IOptionsMonitor<ExecutionOptions> _executionOptions;
    private readonly ILogger<StartAutomationAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartAutomationAction"/> class.
    /// </summary>
    public StartAutomationAction(
        ActionInfrastructure infrastructure,
        IAutomationService automationService,
        IEntityVersionService versionService,
        IAutomationExecutor executor,
        IOptionsMonitor<ExecutionOptions> executionOptions,
        ILogger<StartAutomationAction> logger)
        : base(infrastructure)
    {
        _automationService = automationService;
        _versionService = versionService;
        _executor = executor;
        _executionOptions = executionOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<StartAutomationSettings>();

        if (string.IsNullOrWhiteSpace(settings.AutomationKey) || !Guid.TryParse(settings.AutomationKey, out var automationKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing automation key: '{settings.AutomationKey}'."),
                StepRunErrorCategory.Validation);
        }

        // Parse the trigger data up front so a bad payload fails before anything is started.
        Dictionary<string, object?>? triggerOutputData;
        try
        {
            triggerOutputData = ParseTriggerData(settings.TriggerData);
        }
        catch (JsonException ex)
        {
            return ActionResult.Failed(
                new ArgumentException($"Trigger Data is not a valid JSON object: {ex.Message}", ex),
                StepRunErrorCategory.Validation);
        }

        var automation = await _automationService.GetAutomationAsync(automationKey, cancellationToken);
        if (automation is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automationKey}' was not found."),
                StepRunErrorCategory.ConfigurationError);
        }

        // Workspace boundary: membership, connections and the service account are all scoped
        // per workspace, so a step may only start automations from its own workspace.
        if (context.ExecutionContext is { } executionContext && automation.WorkspaceId != executionContext.WorkspaceId)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automation.Name}' belongs to another workspace."),
                StepRunErrorCategory.ConfigurationError);
        }

        if (automation.Status != AutomationStatus.Published)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Automation '{automation.Name}' is not published."),
                StepRunErrorCategory.ConfigurationError);
        }

        // Cycle and depth guards, mirroring TriggerEventHandler: this imperative path bypasses
        // trigger dispatch, so the origin-chain invariants must be enforced here. The chain
        // handed to the child is OriginChain ∪ { this automation } — the same shape
        // AutomationOriginMiddleware stamps on events raised from within a run.
        IReadOnlyList<Guid> inheritedChain = context.ExecutionContext?.OriginChain ?? [];
        var chain = new List<Guid>(inheritedChain.Count + 1);
        chain.AddRange(inheritedChain);
        chain.Add(context.AutomationId);

        if (chain.Contains(automationKey))
        {
            return ActionResult.Failed(
                new InvalidOperationException(
                    $"Starting automation '{automation.Name}' would create a cycle ({string.Join(" -> ", chain)} -> {automationKey})."),
                StepRunErrorCategory.ConfigurationError);
        }

        var maxChainDepth = _executionOptions.CurrentValue.MaxChainDepth;
        if (chain.Count > maxChainDepth)
        {
            return ActionResult.Failed(
                new InvalidOperationException(
                    $"Starting automation '{automation.Name}' would exceed the maximum chain depth of {maxChainDepth}."),
                StepRunErrorCategory.ConfigurationError);
        }

        // Run the frozen published snapshot, matching trigger dispatch (not the current draft).
        var executionAutomation = automation;
        if (automation.PublishedVersion.HasValue)
        {
            var snapshot = await _versionService.GetVersionSnapshotAsync<Automation>(
                automation.Id, automation.PublishedVersion.Value, cancellationToken);

            if (snapshot is not null)
            {
                executionAutomation = snapshot;
            }
            else
            {
                _logger.LogWarning(
                    "Published version {Version} snapshot not found for automation {AutomationId}, using current state",
                    automation.PublishedVersion.Value, automation.Id);
            }
        }

        var runId = await _executor.ExecuteAsync(
            executionAutomation,
            TriggerInitiatorType.System,
            initiatorId: context.RunId.ToString(),
            triggerOutputData,
            cancellationToken,
            originChain: chain);

        if (runId == Guid.Empty)
        {
            // Circuit-breaker quiet skip: the target automation is auto-disabled. Not a failure
            // of this step — surface it in the output so the run log shows why no child run exists.
            _logger.LogInformation(
                "Automation {AutomationId} / Run {RunId}: Start of automation {TargetAutomationId} skipped — circuit breaker is open",
                context.AutomationId, context.RunId, automationKey);

            return Success(new StartAutomationOutput
            {
                AutomationKey = automationKey,
                Started = false,
                SkippedReason = "The automation is currently disabled by its circuit breaker.",
            });
        }

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Started automation {TargetAutomationId} (run {TargetRunId})",
            context.AutomationId, context.RunId, automationKey, runId);

        return Success(new StartAutomationOutput
        {
            AutomationKey = automationKey,
            RunId = runId,
            Started = true,
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ValidateSettingsAsync(object? settings, CancellationToken cancellationToken = default)
    {
        if (settings is not StartAutomationSettings typed)
        {
            return [];
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(typed.AutomationKey) || !Guid.TryParse(typed.AutomationKey, out var automationKey))
        {
            errors.Add($"'{typed.AutomationKey}' is not a valid automation key.");
        }
        else if (await _automationService.GetAutomationAsync(automationKey, cancellationToken) is null)
        {
            errors.Add($"Automation '{automationKey}' does not exist.");
        }

        // Trigger data can only be checked when it is a literal — bindings resolve at run time.
        if (!string.IsNullOrWhiteSpace(typed.TriggerData)
            && !typed.TriggerData.Contains("${", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(typed.TriggerData);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Trigger Data must be a JSON object.");
                }
            }
            catch (JsonException)
            {
                errors.Add("Trigger Data is not valid JSON.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Parses the configured trigger data into the unwrapped dictionary shape the executor
    /// expects — plain .NET values that survive the WorkflowCore persistence round-trip and
    /// stay traversable by <c>${ trigger.* }</c> binding paths.
    /// </summary>
    private static Dictionary<string, object?>? ParseTriggerData(string? triggerData)
        => string.IsNullOrWhiteSpace(triggerData)
            ? null
            : JsonOptions.DeserializeToUnwrappedDictionary(triggerData);
}
