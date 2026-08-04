using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Default <see cref="ICircuitBreakerService"/>. Derives the consecutive-failure count and error
/// rate from recent run history (no persisted counter) and persists only the transition state
/// (<see cref="AutomationHealthState"/>).
/// </summary>
internal sealed class CircuitBreakerService : ICircuitBreakerService
{
    private readonly IOptionsMonitor<CircuitBreakerOptions> _options;
    private readonly IAutomationHealthRepository _healthRepository;
    private readonly IAutomationRunService _runService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly ILogger<CircuitBreakerService> _logger;

    public CircuitBreakerService(
        IOptionsMonitor<CircuitBreakerOptions> options,
        IAutomationHealthRepository healthRepository,
        IAutomationRunService runService,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory,
        ILogger<CircuitBreakerService> logger)
    {
        _options = options;
        _healthRepository = healthRepository;
        _runService = runService;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
        _logger = logger;
    }

    public async Task EvaluateAsync(AutomationRun run, CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
        {
            return;
        }

        switch (run.Status)
        {
            // Rejected counts as a success: the automation executed exactly as designed and a human
            // declined. Ignoring it instead would starve a half-open breaker of the successes it
            // needs to close, so an automation that is usually rejected could never recover.
            case AutomationRunStatus.Completed:
            case AutomationRunStatus.Rejected:
                await HandleSuccessAsync(run.AutomationId, cancellationToken);
                break;

            case AutomationRunStatus.Failed:
                await HandleFailureAsync(run.AutomationId, _options.CurrentValue, cancellationToken);
                break;

            // Suspended is an intentional wait-for-intervention; Cancelled is a user terminate.
            // Neither is a failure, so both are ignored.
        }
    }

    public async Task<bool> IsRunAllowedAsync(Guid automationId, string initiatorType, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return true;
        }

        var state = await _healthRepository.GetAsync(automationId, cancellationToken);
        if (state is null || state.Health != AutomationHealth.Disabled)
        {
            return true;
        }

        // Disabled: only permitted interactive test runs may proceed.
        return options.AllowManualRunWhileDisabled && TriggerInitiatorType.IsInteractive(initiatorType);
    }

    public async Task ResetAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var state = await _healthRepository.GetAsync(automationId, cancellationToken);
        if (state is null || state.Health == AutomationHealth.Healthy)
        {
            return;
        }

        var previous = state.Health;
        state.Health = AutomationHealth.Healthy;
        state.WarningIssuedUtc = null;
        state.DisabledUtc = null;
        // Advance the window floor so future evaluations ignore the failures that tripped it.
        state.WindowResetUtc = DateTime.UtcNow;
        await _healthRepository.SaveAsync(state, cancellationToken);

        Publish(state, previous, "Re-enabled.");
    }

    public async Task<AutomationHealthState> GetHealthAsync(Guid automationId, CancellationToken cancellationToken = default)
        => await _healthRepository.GetAsync(automationId, cancellationToken)
           ?? new AutomationHealthState { AutomationId = automationId };

    public async Task<IReadOnlyDictionary<Guid, AutomationHealthState>> GetHealthAsync(
        IReadOnlyCollection<Guid> automationIds,
        CancellationToken cancellationToken = default)
    {
        var present = await _healthRepository.GetManyAsync(automationIds, cancellationToken);

        var result = new Dictionary<Guid, AutomationHealthState>(automationIds.Count);
        foreach (var id in automationIds)
        {
            result[id] = present.TryGetValue(id, out var state)
                ? state
                : new AutomationHealthState { AutomationId = id };
        }

        return result;
    }

    private async Task HandleSuccessAsync(Guid automationId, CancellationToken cancellationToken)
    {
        var state = await _healthRepository.GetAsync(automationId, cancellationToken);

        // Only Degraded auto-recovers. Disabled requires an explicit re-enable — a successful
        // interactive test run must not silently resume automatic execution.
        if (state is null || state.Health != AutomationHealth.Degraded)
        {
            return;
        }

        var previous = state.Health;
        state.Health = AutomationHealth.Healthy;
        state.WarningIssuedUtc = null;
        await _healthRepository.SaveAsync(state, cancellationToken);

        Publish(state, previous, "Recovered after a successful run.");
    }

    // Note: in-place Degraded → Healthy (above) does NOT advance the window floor — the recovery
    // is run-driven; the window naturally rolls forward as more runs accumulate.

    private async Task HandleFailureAsync(Guid automationId, CircuitBreakerOptions options, CancellationToken cancellationToken)
    {
        var state = await _healthRepository.GetAsync(automationId, cancellationToken)
            ?? new AutomationHealthState { AutomationId = automationId };

        // Already disabled — idempotent no-op. The gate blocks automatic runs, so a failure here
        // can only come from a failed interactive test run, which leaves it disabled.
        if (state.Health == AutomationHealth.Disabled)
        {
            return;
        }

        // Only consider runs that started after the last reset, so a re-enable / re-publish
        // doesn't re-trip on stale failures the operator just acknowledged.
        var since = state.WindowResetUtc ?? DateTime.MinValue;
        var window = await _runService.GetRecentTerminalStatusesAsync(
            automationId, options.EvaluationWindowSize, since, cancellationToken);

        var consecutiveFailures = 0;
        foreach (var status in window)
        {
            if (status != AutomationRunStatus.Failed)
            {
                break;
            }

            consecutiveFailures++;
        }

        var total = window.Count;
        var failed = window.Count(s => s == AutomationRunStatus.Failed);
        var errorRate = total > 0 ? (double)failed / total : 0d;
        var windowFull = total >= options.EvaluationWindowSize;

        // Ordered checks — first match wins and short-circuits.
        if (consecutiveFailures >= options.ConsecutiveFailureThreshold)
        {
            await DisableAsync(
                state,
                $"{consecutiveFailures} consecutive failures (threshold {options.ConsecutiveFailureThreshold}).",
                cancellationToken);
            return;
        }

        if (windowFull
            && errorRate >= options.DisableErrorRate
            && state.WarningIssuedUtc is { } warnedAt
            && DateTime.UtcNow - warnedAt >= options.GracePeriodAfterWarning)
        {
            await DisableAsync(
                state,
                $"Error rate {errorRate:P0} exceeded the disable threshold {options.DisableErrorRate:P0} after the grace period.",
                cancellationToken);
            return;
        }

        if (consecutiveFailures >= options.ConsecutiveWarningThreshold && state.Health == AutomationHealth.Healthy)
        {
            var previous = state.Health;
            state.Health = AutomationHealth.Degraded;
            state.WarningIssuedUtc = DateTime.UtcNow;
            await _healthRepository.SaveAsync(state, cancellationToken);

            Publish(
                state,
                previous,
                $"{consecutiveFailures} consecutive failures (warning at {options.ConsecutiveWarningThreshold}, disable at {options.ConsecutiveFailureThreshold}).");
            return;
        }

        if (windowFull && errorRate >= options.WarningErrorRate && state.Health == AutomationHealth.Healthy)
        {
            var previous = state.Health;
            state.Health = AutomationHealth.Degraded;
            state.WarningIssuedUtc = DateTime.UtcNow;
            await _healthRepository.SaveAsync(state, cancellationToken);

            Publish(
                state,
                previous,
                $"Error rate {errorRate:P0} exceeded the warning threshold {options.WarningErrorRate:P0}.");
        }
    }

    private async Task DisableAsync(AutomationHealthState state, string reason, CancellationToken cancellationToken)
    {
        var previous = state.Health;
        state.Health = AutomationHealth.Disabled;
        state.DisabledUtc = DateTime.UtcNow;
        await _healthRepository.SaveAsync(state, cancellationToken);

        Publish(state, previous, reason);

        _logger.LogWarning(
            "Automation {AutomationId} auto-disabled by the circuit breaker: {Reason}",
            state.AutomationId, reason);
    }

    private void Publish(AutomationHealthState state, AutomationHealth previousHealth, string? reason)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();
        var messages = _eventMessagesFactory.Get();
        scope.Notifications.Publish(new AutomationHealthChangedNotification(state, previousHealth, reason, messages));
        scope.Complete();
    }
}
