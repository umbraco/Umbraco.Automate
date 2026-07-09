using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore <see cref="IDistributedLockProvider"/> backed by <see cref="IWorkflowLockStore"/>,
/// using a lease (owner token + expiry) so locks are held across nodes rather than in a single
/// process's memory (WorkflowCore's default <c>SingleNodeLockProvider</c>).
/// </summary>
internal sealed class WorkflowLockProvider : IDistributedLockProvider
{
    private readonly IWorkflowLockStore _store;
    private readonly IOptions<WorkflowLockOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkflowLockProvider> _logger;
    private readonly Guid _ownerToken = Guid.NewGuid();
    private readonly ConcurrentDictionary<string, byte> _owned = new();

    private CancellationTokenSource? _renewalCts;
    private Task? _renewalLoop;

    public WorkflowLockProvider(
        IWorkflowLockStore store,
        IOptions<WorkflowLockOptions> options,
        TimeProvider timeProvider,
        ILogger<WorkflowLockProvider> logger)
    {
        _store = store;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<bool> AcquireLock(string Id, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresUtc = now + _options.Value.LeaseDuration;

        var acquired = await _store.TryAcquireAsync(Id, _ownerToken, now, expiresUtc, cancellationToken);
        if (acquired)
        {
            _owned[Id] = 0;
        }

        return acquired;
    }

    public async Task ReleaseLock(string Id)
    {
        await _store.ReleaseAsync(Id, _ownerToken, CancellationToken.None);
        _owned.TryRemove(Id, out _);
    }

    public Task Start()
    {
        _renewalCts = new CancellationTokenSource();
        _renewalLoop = Task.Run(() => RunRenewalLoopAsync(_renewalCts.Token));
        return Task.CompletedTask;
    }

    public async Task Stop()
    {
        if (_renewalCts is null)
        {
            return;
        }

        await _renewalCts.CancelAsync();

        if (_renewalLoop is not null)
        {
            try
            {
                await _renewalLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Best-effort clean release of everything this process still owns, so other nodes don't
        // wait out the full lease on a graceful shutdown.
        try
        {
            var ids = _owned.Keys.ToList();
            foreach (var id in ids)
            {
                await _store.ReleaseAsync(id, _ownerToken, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release owned workflow locks during shutdown");
        }

        _owned.Clear();
        _renewalCts.Dispose();
        _renewalCts = null;
        _renewalLoop = null;
    }

    private async Task RunRenewalLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.Value.RenewalInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_owned.IsEmpty)
                {
                    continue;
                }

                try
                {
                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    var expiresUtc = now + _options.Value.LeaseDuration;
                    var ids = _owned.Keys.ToList();

                    await _store.RenewAsync(ids, _ownerToken, expiresUtc, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to renew owned workflow locks");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
