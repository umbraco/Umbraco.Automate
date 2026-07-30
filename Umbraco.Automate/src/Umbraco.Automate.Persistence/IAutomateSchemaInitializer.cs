using Umbraco.Automate.Core;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// Creates or upgrades Automate's own database schema, at most once per process, and resolves
/// <see cref="AutomateReadinessSignal"/> with the outcome.
/// </summary>
/// <remarks>
/// Callers do not need to coordinate: the first call performs the migration and any concurrent or
/// later call observes the same result. This exists so that the migration is not owned by a single
/// startup notification handler, whose position in the boot sequence is not the same on every boot
/// path — see <c>AutomateSchemaComponent</c> for the ordering this is designed to guarantee.
/// </remarks>
public interface IAutomateSchemaInitializer
{
    /// <summary>
    /// Ensures Automate's schema is up to date, running any pending EF Core migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// Does <strong>not</strong> throw when migrating fails. The failure is logged and recorded on
    /// <see cref="AutomateReadinessSignal"/> (via <see cref="AutomateReadinessSignal.SignalFailed"/>),
    /// so waiters fail fast with <see cref="AutomateNotReadyException"/> instead of hanging, and a
    /// broken Automate schema does not prevent the rest of the site from starting. Cancellation does
    /// propagate, and leaves the schema state unresolved so a later call can retry.
    /// </remarks>
    Task EnsureMigratedAsync(CancellationToken cancellationToken = default);
}
