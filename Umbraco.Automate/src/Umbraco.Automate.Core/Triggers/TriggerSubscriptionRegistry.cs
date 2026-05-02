using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Default <see cref="ITriggerSubscriptionRegistry"/>. Lazily caches the set of trigger
/// aliases that have at least one published automation subscribed, and rebuilds the cache
/// after invalidation.
/// </summary>
internal sealed class TriggerSubscriptionRegistry : ITriggerSubscriptionRegistry, IDisposable
{
    private readonly IAutomationService _automationService;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly ILogger<TriggerSubscriptionRegistry> _logger;

    // _gate serialises rebuilds so concurrent calls don't all hit the database.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // null = not built or invalidated; non-null = current view.
    // Volatile reads via Volatile.Read/Write so the fast path doesn't need the gate.
    private HashSet<string>? _subscribed;

    public TriggerSubscriptionRegistry(
        IAutomationService automationService,
        AutomateReadinessSignal readinessSignal,
        ILogger<TriggerSubscriptionRegistry> logger)
    {
        _automationService = automationService;
        _readinessSignal = readinessSignal;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> HasSubscribersAsync(string triggerAlias, CancellationToken cancellationToken = default)
    {
        // Fail-open before the database is ready: don't drop events while migrations run.
        if (!_readinessSignal.IsReady)
        {
            return true;
        }

        var snapshot = Volatile.Read(ref _subscribed);
        if (snapshot is not null)
        {
            return snapshot.Contains(triggerAlias);
        }

        // Slow path: build the cache under the gate.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock — another caller may have built it while we waited.
            snapshot = Volatile.Read(ref _subscribed);
            if (snapshot is null)
            {
                snapshot = await BuildSubscribedSetAsync(cancellationToken);
                Volatile.Write(ref _subscribed, snapshot);
            }
        }
        finally
        {
            _gate.Release();
        }

        return snapshot.Contains(triggerAlias);
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        Volatile.Write(ref _subscribed, null);
    }

    private async Task<HashSet<string>> BuildSubscribedSetAsync(CancellationToken cancellationToken)
    {
        var automations = await _automationService.GetAllAutomationsAsync(cancellationToken);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var automation in automations)
        {
            if (automation.Status == AutomationStatus.Published
                && automation.Trigger?.TriggerAlias is { Length: > 0 } alias)
            {
                set.Add(alias);
            }
        }

        _logger.LogDebug(
            "Trigger subscription cache rebuilt — {SubscriberCount} alias(es) have subscribers",
            set.Count);

        return set;
    }

    public void Dispose() => _gate.Dispose();
}
