using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Persistence.Notifications;

/// <summary>
/// Safety net that ensures Automate's schema has been migrated before the remaining
/// <see cref="UmbracoApplicationStartedNotification"/> handlers run.
/// </summary>
/// <remarks>
/// The migration itself normally happens earlier, during component initialization
/// (<c>AutomateSchemaComponent</c>), because a Started handler is too late for callers that run on
/// <see cref="UmbracoApplicationStartingNotification"/> — see
/// <see href="https://github.com/umbraco/Umbraco.Automate/issues/198"/>. This handler is kept so
/// that the schema is still initialized on any boot path that does not initialize components first,
/// and is a no-op once it has been.
/// </remarks>
public class RunAutomateMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IAutomateSchemaInitializer _schemaInitializer;

    /// <summary>
    /// Initializes a new instance of <see cref="RunAutomateMigrationNotificationHandler"/>.
    /// </summary>
    public RunAutomateMigrationNotificationHandler(IAutomateSchemaInitializer schemaInitializer)
        => _schemaInitializer = schemaInitializer;

    /// <inheritdoc />
    public Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
        => _schemaInitializer.EnsureMigratedAsync(cancellationToken);
}
