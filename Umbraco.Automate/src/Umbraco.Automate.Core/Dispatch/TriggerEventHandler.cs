using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Handles trigger event messages from the outbox and starts matching automation runs.
/// </summary>
internal sealed class TriggerEventHandler : IMessageHandler
{
    private readonly IAutomationService _automationService;
    private readonly IEntityVersionService _versionService;
    private readonly IAutomationExecutor _executor;
    private readonly IExecutionNodeEligibility _nodeEligibility;
    private readonly TriggerCollection _triggers;
    private readonly IWorkspaceServiceAccountResolver _serviceAccountResolver;
    private readonly ISectionAccessChecker _sectionAccessChecker;
    private readonly TriggerDispatchAuthorizerCollection _dispatchAuthorizers;
    private readonly IOptionsMonitor<ExecutionOptions> _executionOptions;
    private readonly ILogger<TriggerEventHandler> _logger;

    public TriggerEventHandler(
        IAutomationService automationService,
        IEntityVersionService versionService,
        IAutomationExecutor executor,
        IExecutionNodeEligibility nodeEligibility,
        TriggerCollection triggers,
        IWorkspaceServiceAccountResolver serviceAccountResolver,
        ISectionAccessChecker sectionAccessChecker,
        TriggerDispatchAuthorizerCollection dispatchAuthorizers,
        IOptionsMonitor<ExecutionOptions> executionOptions,
        ILogger<TriggerEventHandler> logger)
    {
        _automationService = automationService;
        _versionService = versionService;
        _executor = executor;
        _nodeEligibility = nodeEligibility;
        _triggers = triggers;
        _serviceAccountResolver = serviceAccountResolver;
        _sectionAccessChecker = sectionAccessChecker;
        _dispatchAuthorizers = dispatchAuthorizers;
        _executionOptions = executionOptions;
        _logger = logger;
    }

    public string Topic => OutboxTriggerDispatcher.TopicName;

    public bool CanProcessNow() => _nodeEligibility.CanExecuteWorkflows();

    public async Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        // Defensive: if eligibility flipped between the dispatcher's pre-claim check and
        // now, throw so the dispatcher retries on backoff (rather than silently completing
        // the message and losing the trigger event).
        if (!_nodeEligibility.CanExecuteWorkflows())
        {
            throw new NodeNotEligibleException(Topic);
        }

        var message = JsonSerializer.Deserialize<TriggerEventMessage>(body, JsonOptions.Default)
                      ?? throw new InvalidOperationException("Failed to deserialize TriggerEventMessage");

        _logger.LogDebug("Received trigger event for {TriggerAlias}", message.TriggerAlias);

        // Global chain-length backstop: drop the event before doing any work if the cascade
        // chain has exceeded the configured limit. This catches loops the per-trigger
        // SkipAutomationOriginatedEvents toggle misses (e.g. operator deliberately turned
        // the toggle off, or a chain that doesn't repeat any automation but goes too deep).
        var maxChainDepth = _executionOptions.CurrentValue.MaxChainDepth;
        if (message.OriginAutomationChain.Count > maxChainDepth)
        {
            _logger.LogWarning(
                "Dropping trigger {TriggerAlias} — chain length {ChainLength} exceeded MaxChainDepth {MaxChainDepth} (origin run {OriginRunId}, chain {Chain})",
                message.TriggerAlias, message.OriginAutomationChain.Count, maxChainDepth, message.OriginRunId,
                string.Join(" -> ", message.OriginAutomationChain));
            return;
        }

        // Find all published automations that use this trigger. When the event targets a
        // specific automation (imperative entry points like the webhook endpoint, addressed by
        // automation ID), narrow to that one — otherwise this fans out to every published
        // automation subscribed to the alias, running them all.
        var automations = await _automationService.GetAllAutomationsAsync(cancellationToken);
        var matching = automations
            .Where(a => a.Status == AutomationStatus.Published
                        && a.Trigger?.TriggerAlias == message.TriggerAlias
                        && (message.TargetAutomationId is null || a.Id == message.TargetAutomationId))
            .ToList();

        if (matching.Count == 0)
        {
            _logger.LogInformation(
                "No published automations matched trigger {TriggerAlias} — event dropped",
                message.TriggerAlias);
            return;
        }

        // Deserialize trigger output data for the run context.
        // Unwrap JsonElement values to primitives so they survive the Newtonsoft.Json
        // round-trip used by the WorkflowCore persistence provider.
        Dictionary<string, object?>? triggerOutputData = null;
        if (!string.IsNullOrEmpty(message.OutputData))
        {
            triggerOutputData = JsonOptions.DeserializeToUnwrappedDictionary(message.OutputData);
        }

        // Look up the trigger so we can consult its per-automation settings filter
        // (ITrigger.CanHandle). Not finding the trigger is unusual but shouldn't drop
        // events — fall through and dispatch unconditionally.
        var trigger = _triggers.GetByAlias(message.TriggerAlias);

        // Typed output is only needed when at least one automation has configured
        // trigger settings, or when a dispatch authoriser needs to inspect the payload.
        // Deserialize lazily so the common no-filter / no-authoriser path stays cheap.
        object? typedOutput = null;
        var typedOutputAttempted = false;

        // Per-invocation cache of service-account resolution per workspace. Avoids repeating
        // the IUserService.GetAsync round-trip when several published automations subscribe
        // to the same trigger from the same workspace.
        var serviceAccountCache = new Dictionary<Guid, IUser?>();

        foreach (var automation in matching)
        {
            // Resolve the published version snapshot for execution.
            // This ensures we run the frozen, published state — not the current draft.
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

            // Filter: apply the trigger's per-automation settings predicate against the
            // published snapshot's settings. Skip when there are no configured settings,
            // no trigger, or the trigger declares no settings type — nothing to match on.
            var triggerSettings = executionAutomation.Trigger?.Settings;
            if (trigger is not null
                && trigger.SettingsType is not null
                && triggerSettings is { Count: > 0 })
            {
                if (!typedOutputAttempted)
                {
                    typedOutput = DeserializeTypedOutput(message.OutputData, trigger.OutputType);
                    typedOutputAttempted = true;
                }

                if (typedOutput is not null)
                {
                    var resolvedSettings = trigger.ResolveSettings(triggerSettings);

                    // Origin-aware filtering: only relevant when the event was caused by an
                    // automation (chain non-empty). Three postures, configured per trigger:
                    //   - Run        : always fire
                    //   - SkipOnCycle: skip when this automation is in the chain (direct or
                    //                  indirect cycle); default for built-in content triggers
                    //   - SkipAlways : skip whenever any automation caused the event
                    if (message.OriginAutomationChain.Count > 0
                        && resolvedSettings is IAutomationOriginatedEventBehavior behaviorConfig)
                    {
                        var behavior = behaviorConfig.OnAutomationOriginated;
                        var shouldSkip = behavior switch
                        {
                            AutomationOriginatedEventBehavior.SkipAlways => true,
                            AutomationOriginatedEventBehavior.SkipOnCycle =>
                                message.OriginAutomationChain.Contains(executionAutomation.Id),
                            _ => false,
                        };

                        if (shouldSkip)
                        {
                            _logger.LogDebug(
                                "Automation {AutomationId} skipped — trigger {TriggerAlias} behavior {Behavior} blocks event from chain {Chain}",
                                executionAutomation.Id, message.TriggerAlias, behavior,
                                string.Join(" -> ", message.OriginAutomationChain));
                            continue;
                        }
                    }

                    if (!trigger.CanHandle(typedOutput, resolvedSettings))
                    {
                        _logger.LogDebug(
                            "Automation {AutomationId} skipped by trigger {TriggerAlias} settings filter",
                            executionAutomation.Id, message.TriggerAlias);
                        continue;
                    }
                }
            }

            // Runtime section guard: even if the trigger was permitted at publish time,
            // the workspace's service account may have been downgraded since. Skip the run
            // (no run record, no payload exposure) when the account no longer has the
            // sections the trigger requires. After that, run any registered dispatch
            // authorisers — built-in NodeScopedTriggerDispatchAuthorizer plus any provider
            // packages have registered (e.g. Commerce store-scoped).
            var sectionGated = trigger is not null && trigger.RequiredSections.Count > 0;
            var hasDispatchAuthorizers = _dispatchAuthorizers.Count > 0;
            if (sectionGated || hasDispatchAuthorizers)
            {
                var serviceAccount = await ResolveServiceAccountAsync(executionAutomation.WorkspaceId, serviceAccountCache, cancellationToken);
                if (serviceAccount is null)
                {
                    _logger.LogWarning(
                        "Automation {AutomationId} skipped — workspace service account could not be resolved for trigger {TriggerAlias}.",
                        executionAutomation.Id, message.TriggerAlias);
                    continue;
                }

                if (sectionGated && !_sectionAccessChecker.CanAccess(serviceAccount, trigger!))
                {
                    _logger.LogWarning(
                        "Automation {AutomationId} skipped — workspace service account does not satisfy trigger {TriggerAlias} section requirements ({Sections}). Republish or update the service account.",
                        executionAutomation.Id, message.TriggerAlias, string.Join(", ", trigger!.RequiredSections));
                    continue;
                }

                if (hasDispatchAuthorizers && trigger is not null)
                {
                    if (!typedOutputAttempted)
                    {
                        typedOutput = DeserializeTypedOutput(message.OutputData, trigger.OutputType);
                        typedOutputAttempted = true;
                    }

                    var authContext = new TriggerDispatchAuthorizationContext
                    {
                        Trigger = trigger,
                        TypedOutput = typedOutput,
                        ServiceAccount = serviceAccount,
                        Automation = executionAutomation,
                    };

                    var deny = await EvaluateAuthorizersAsync(authContext, cancellationToken);
                    if (deny is not null)
                    {
                        _logger.LogInformation(
                            "Automation {AutomationId} skipped by dispatch authoriser {Authoriser} (trigger {TriggerAlias}): {Reason}",
                            executionAutomation.Id, deny.Value.AuthorizerName, message.TriggerAlias, deny.Value.Result.FailureReason);
                        continue;
                    }
                }
            }

            _logger.LogInformation(
                "Starting run for automation {AutomationAlias} ({AutomationId}) version {Version} from trigger {TriggerAlias}",
                executionAutomation.Alias, executionAutomation.Id, executionAutomation.Version, message.TriggerAlias);

            await _executor.ExecuteAsync(
                executionAutomation,
                message.InitiatorType,
                message.InitiatorId,
                triggerOutputData,
                cancellationToken,
                originChain: message.OriginAutomationChain);
        }
    }

    private async Task<(AutomationAuthorizationResult Result, string AuthorizerName)?> EvaluateAuthorizersAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken)
    {
        foreach (var authorizer in _dispatchAuthorizers)
        {
            var result = await authorizer.AuthorizeAsync(context, cancellationToken);
            if (!result.Authorized)
            {
                return (result, authorizer.GetType().Name);
            }
        }

        return null;
    }

    private async Task<IUser?> ResolveServiceAccountAsync(
        Guid workspaceId,
        Dictionary<Guid, IUser?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(workspaceId, out var cached))
        {
            return cached;
        }

        var user = await _serviceAccountResolver.GetServiceAccountAsync(workspaceId, cancellationToken);
        cache[workspaceId] = user;
        return user;
    }

    private object? DeserializeTypedOutput(string? outputData, Type? outputType)
    {
        if (string.IsNullOrEmpty(outputData) || outputType is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(outputData, outputType, JsonOptions.Default);
        }
        catch (JsonException ex)
        {
            // A deserialization failure shouldn't silently suppress automations. Log and
            // fall through — automations with settings filters will behave as if no filter
            // was configured rather than being dropped.
            _logger.LogWarning(ex,
                "Failed to deserialize trigger output as {OutputType} for filtering — automations with configured settings will fire unfiltered",
                outputType.FullName);
            return null;
        }
    }
}
