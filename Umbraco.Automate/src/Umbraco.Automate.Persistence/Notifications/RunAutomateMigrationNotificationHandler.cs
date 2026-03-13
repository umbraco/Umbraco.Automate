using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Persistence.Notifications;

/// <summary>
/// Notification handler that runs pending EF Core migrations on application startup.
/// </summary>
public class RunAutomateMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly AutomateReadinessSignal _readinessSignal;

    /// <summary>
    /// Initializes a new instance of <see cref="RunAutomateMigrationNotificationHandler"/>.
    /// </summary>
    public RunAutomateMigrationNotificationHandler(
        IConfiguration configuration,
        AutomateReadinessSignal readinessSignal)
    {
        _configuration = configuration;
        _readinessSignal = readinessSignal;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        // Create a standalone DbContext rather than using IDbContextFactory. Umbraco's EFCoreScope
        // infrastructure shares NPoco connections (wrapped with MiniProfiler's ProfiledDbConnection)
        // onto pooled EF Core contexts via SetDbConnection(). These tainted contexts cause
        // NullReferenceException in SqliteDatabaseCreator.Exists() when the ProfiledDbConnection's
        // inner connection is disposed. Creating the context directly avoids the pooled factory.
        // See: https://github.com/umbraco/Umbraco-CMS/issues/22124
        var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(_configuration);
        var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(optionsBuilder, connectionString, providerName);

        await using UmbracoAutomateDbContext dbContext = new UmbracoAutomateDbContext(optionsBuilder.Options);

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        _readinessSignal.Signal();
    }
}
