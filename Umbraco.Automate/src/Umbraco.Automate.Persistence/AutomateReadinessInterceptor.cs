using Microsoft.EntityFrameworkCore.Diagnostics;
using Umbraco.Automate.Core;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// EF Core save interceptor for <see cref="UmbracoAutomateDbContext"/> that defers writes until
/// Automate's own startup migrations have completed (<see cref="AutomateReadinessSignal"/>).
/// Without this, an early caller — e.g. Umbraco Deploy's disk-triggered import, which can run
/// before Automate's <c>UmbracoApplicationStartedNotification</c> handler has migrated the
/// schema — can write against a database that hasn't finished migrating yet.
/// </summary>
/// <remarks>
/// Not applied to the standalone context the migration handler itself uses (see
/// <c>RunAutomateMigrationNotificationHandler</c>) — that context must be able to write
/// (via <c>Database.MigrateAsync</c>, which bypasses <see cref="SavingChangesAsync"/> entirely)
/// before the signal it is responsible for setting has fired.
/// If startup migrations fail, the signal is set to a faulted state and any save waiting on it
/// throws <see cref="Core.AutomateNotReadyException"/> immediately rather than hanging forever.
/// </remarks>
internal sealed class AutomateReadinessInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// How long an enlisted save waits for the readiness signal before giving up.
    /// </summary>
    /// <remarks>
    /// Only the enlisted path is bounded, and it has to be: it waits while holding the caller's
    /// transaction, which on SQLite is the single per-file write lock, so an unbounded wait there stalls
    /// every writer on the site rather than one query. Giving up instead fails the caller's transaction,
    /// which rolls back and releases the lock. Generous enough not to trip over a slow first-boot
    /// migration, since the case this interceptor exists for is precisely a caller arriving before
    /// migrations have finished.
    /// </remarks>
    internal static readonly TimeSpan EnlistedWaitTimeout = TimeSpan.FromSeconds(30);

    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly TimeSpan? _waitTimeout;

    /// <param name="readinessSignal">The signal set once startup migrations have completed.</param>
    /// <param name="waitTimeout">
    /// How long to wait for the signal, or <c>null</c> to wait indefinitely. Pass
    /// <see cref="EnlistedWaitTimeout"/> on the enlisted path; the pooled factory's context owns its
    /// own connection and blocks nobody else, so it waits without a timeout.
    /// </param>
    public AutomateReadinessInterceptor(
        AutomateReadinessSignal readinessSignal,
        TimeSpan? waitTimeout = null)
    {
        _readinessSignal = readinessSignal;
        _waitTimeout = waitTimeout;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await WaitForReadinessAsync(cancellationToken).ConfigureAwait(false);

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForReadinessAsync(CancellationToken cancellationToken)
    {
        if (_waitTimeout is not { } timeout)
        {
            await _readinessSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            await _readinessSignal.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            // Surfaced as AutomateNotReadyException, not a cancellation: the caller did not cancel
            // anything, Automate failed to become ready in time.
            throw new AutomateNotReadyException(
                new TimeoutException(
                    $"Umbraco Automate did not finish its startup migrations within {timeout.TotalSeconds:0}s, " +
                    "so a write enlisted in an ambient Umbraco transaction was abandoned rather than " +
                    "holding that transaction open indefinitely."));
        }
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        // Gate the synchronous save path too, so a SaveChanges() call can't slip a write through
        // ungated. In steady state the signal is already completed, so this returns instantly and
        // never blocks — it only ever waits during the pre-ready startup window, and throws
        // AutomateNotReadyException if migrations failed or the wait timed out (mirroring
        // SavingChangesAsync).
        WaitForReadinessAsync(CancellationToken.None).GetAwaiter().GetResult();

        return base.SavingChanges(eventData, result);
    }
}
