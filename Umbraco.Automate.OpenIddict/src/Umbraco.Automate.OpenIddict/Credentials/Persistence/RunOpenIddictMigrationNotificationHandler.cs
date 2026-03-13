using Microsoft.EntityFrameworkCore;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// Notification handler that runs pending EF Core migrations for the OpenIddict credential table on startup.
/// </summary>
internal sealed class RunOpenIddictMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDbContextFactory<OpenIddictDbContext> _dbContextFactory;

    public RunOpenIddictMigrationNotificationHandler(IDbContextFactory<OpenIddictDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        await using OpenIddictDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
