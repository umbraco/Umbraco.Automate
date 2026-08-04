using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// Creates or upgrades Automate's schema during component initialization, which is the earliest
/// point that is guaranteed to run before anything else can query Automate's tables.
/// </summary>
/// <remarks>
/// <para>
/// Automate used to migrate from a <c>UmbracoApplicationStartedNotification</c> handler, which is
/// too late. Umbraco Deploy runs a queued boot-time restore from its
/// <c>UmbracoApplicationStartingNotification</c> handler, and its connectors read Automate's tables
/// to decide create-vs-update. On the unattended install/upgrade path that read happened before
/// Automate had created those tables, and the restore failed with a raw
/// <c>no such table: umbracoAutomateWorkspace</c> provider error.
/// See <see href="https://github.com/umbraco/Umbraco.Automate/issues/198"/>.
/// </para>
/// <para>
/// Component initialization is the fix because both boot paths run it immediately <em>before</em>
/// publishing <c>UmbracoApplicationStartingNotification</c>, so it precedes every notification
/// handler rather than racing them:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Normal boot: <c>CoreRuntime.StartAsync</c> calls <c>_components.InitializeAsync</c> and then
/// publishes the Starting notification.
/// </description></item>
/// <item><description>
/// Unattended install/upgrade: <c>UnattendedUpgradeBackgroundService.ExecuteAsync</c> does the same,
/// after the CMS migrations it is responsible for.
/// </description></item>
/// </list>
/// <para>
/// Waiting on <see cref="Core.AutomateReadinessSignal"/> from inside the restore instead would not
/// have worked: Deploy's boot restore runs inside the Starting notification, and on the unattended
/// path the signal was only set by a step that could not run until that notification returned. Any
/// wait there is unsatisfiable, so gating the read would have replaced the error with a startup
/// that never completes.
/// </para>
/// <para>
/// Nor could Automate simply tolerate the not-yet-migrated state the way a package whose tables are
/// only read by its own code can (Umbraco Forms, for instance, guards every analytics read with a
/// table-exists check). The failing read here belongs to Deploy, and a package cannot make another
/// package's read defensive. Guaranteeing the schema exists first is the only lever Automate has.
/// </para>
/// <para>
/// Deliberately thin: the runtime-level check and the once-per-process latch both live in
/// <see cref="IAutomateSchemaInitializer"/>, so the safety-net startup handler cannot disagree with
/// this component about when migrating is possible.
/// </para>
/// </remarks>
internal sealed class AutomateSchemaComponent : IAsyncComponent
{
    private readonly IAutomateSchemaInitializer _schemaInitializer;

    public AutomateSchemaComponent(IAutomateSchemaInitializer schemaInitializer)
        => _schemaInitializer = schemaInitializer;

    /// <inheritdoc />
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
        => _schemaInitializer.EnsureMigratedAsync(cancellationToken);

    /// <inheritdoc />
    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
