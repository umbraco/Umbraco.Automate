using Microsoft.EntityFrameworkCore.Diagnostics;
using Umbraco.Automate.Core;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// EF Core save interceptor for <see cref="UmbracoAutomateDbContext"/> that defers writes until
/// Automate's own startup migrations have completed (<see cref="AutomateReadinessSignal"/>).
/// Without this, an early caller — e.g. Umbraco Deploy's disk-triggered import — could write
/// against a database that hasn't finished migrating yet.
/// </summary>
/// <remarks>
/// <para>
/// This is a backstop, not the primary ordering guarantee. The schema is created during component
/// initialization (<c>AutomateSchemaComponent</c>), which every boot path runs before publishing
/// <c>UmbracoApplicationStartingNotification</c>, so in practice the signal is already resolved by
/// the time any other caller runs. That matters: waiting here cannot be the answer for a caller that
/// runs <em>inside</em> the notification whose completion resolves the signal, because such a wait
/// can never be satisfied. See https://github.com/umbraco/Umbraco.Automate/issues/198.
/// </para>
/// <para>
/// Not applied to the standalone context used to migrate (see <c>AutomateSchemaInitializer</c>) —
/// that context must be able to write (via <c>Database.MigrateAsync</c>, which bypasses
/// <see cref="SavingChangesAsync"/> entirely) before the signal it is responsible for setting
/// has fired.
/// </para>
/// <para>
/// If startup migrations fail, the signal is set to a faulted state and any save waiting on it
/// throws <see cref="Core.AutomateNotReadyException"/> immediately rather than hanging forever.
/// A failure is not the only way the signal can stay unresolved, though: below
/// <c>RuntimeLevel.Run</c> the schema is deliberately not migrated and the signal is left pending, so
/// the synchronous path below waits with a timeout rather than blocking its thread indefinitely.
/// </para>
/// </remarks>
internal sealed class AutomateReadinessInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// How long a synchronous save waits for the schema to become ready before giving up.
    /// </summary>
    /// <remarks>
    /// Only ever reached during the pre-ready startup window, and generous enough that a first-boot
    /// migration on a slow server is not mistaken for a stuck one. The asynchronous path is not
    /// bounded this way because it observes its caller's <see cref="CancellationToken"/>.
    /// </remarks>
    private static readonly TimeSpan SynchronousSaveTimeout = TimeSpan.FromSeconds(30);

    private readonly AutomateReadinessSignal _readinessSignal;

    public AutomateReadinessInterceptor(AutomateReadinessSignal readinessSignal)
    {
        _readinessSignal = readinessSignal;
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await _readinessSignal.WaitAsync(cancellationToken).ConfigureAwait(false);

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        // Gate the synchronous save path too, so a SaveChanges() call can't slip a write through
        // ungated. In steady state the signal is already completed, so this returns instantly and
        // never blocks — it only ever waits during the pre-ready startup window, and throws
        // AutomateNotReadyException if migrations failed (mirroring SavingChangesAsync).
        //
        // There is no CancellationToken on this overload, so the wait is bounded explicitly. Without
        // that, a write attempted while the signal is still pending — which is the normal state below
        // RuntimeLevel.Run, where the schema is deliberately not migrated — would block this thread
        // for the lifetime of the process.
        try
        {
            _readinessSignal.WaitAsync().WaitAsync(SynchronousSaveTimeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException ex)
        {
            throw new AutomateNotReadyException(
                $"Automate's schema was still not ready after {SynchronousSaveTimeout.TotalSeconds:0} seconds, so this write was rejected. " +
                "This usually means the site has not reached a running state, and therefore Automate's startup migrations have not run.",
                ex);
        }

        return base.SavingChanges(eventData, result);
    }
}
