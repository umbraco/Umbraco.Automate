using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Permissive <see cref="ICircuitBreakerService"/> for integration tests that wire the executor
/// but do not exercise the circuit breaker — it allows every run and reports Healthy.
/// </summary>
public sealed class StubCircuitBreakerService : ICircuitBreakerService
{
    public Task EvaluateAsync(AutomationRun run, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> IsRunAllowedAsync(Guid automationId, string initiatorType, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task ResetAsync(Guid automationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<AutomationHealthState> GetHealthAsync(Guid automationId, CancellationToken cancellationToken = default)
        => Task.FromResult(new AutomationHealthState { AutomationId = automationId });

    public Task<IReadOnlyDictionary<Guid, AutomationHealthState>> GetHealthAsync(
        IReadOnlyCollection<Guid> automationIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, AutomationHealthState>>(
            new Dictionary<Guid, AutomationHealthState>());
}
