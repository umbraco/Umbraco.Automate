using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Persistence.Notifications;

/// <summary>
/// Notification handler that runs pending EF Core migrations on application startup.
/// </summary>
public class RunAutomateMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly IRuntimeState _runtimeState;

    /// <summary>
    /// Initializes a new instance of <see cref="RunAutomateMigrationNotificationHandler"/>.
    /// </summary>
    public RunAutomateMigrationNotificationHandler(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        IRuntimeState runtimeState)
    {
        _dbContextFactory = dbContextFactory;
        _runtimeState = runtimeState;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        // Skip migrations when the database is not yet available (e.g. fresh install).
        if (_runtimeState.Level < RuntimeLevel.Run)
        {
            return;
        }

        await using UmbracoAutomateDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        OutboxHealthCheck.MigrationsComplete = true;
    }
}
