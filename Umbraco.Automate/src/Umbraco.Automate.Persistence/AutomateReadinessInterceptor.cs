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
/// </para>
/// </remarks>
internal sealed class AutomateReadinessInterceptor : SaveChangesInterceptor
{
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
        _readinessSignal.WaitAsync().GetAwaiter().GetResult();

        return base.SavingChanges(eventData, result);
    }
}
