using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// Notification handler that runs pending EF Core migrations for the OpenIddict credential table on startup.
/// </summary>
internal sealed class RunOpenIddictMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;

    public RunOpenIddictMigrationNotificationHandler(
        IConfiguration configuration,
        IOptionsMonitor<ConnectionStrings> connectionStrings)
    {
        _configuration = configuration;
        _connectionStrings = connectionStrings;
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
        var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(_connectionStrings, _configuration);
        var optionsBuilder = new DbContextOptionsBuilder<OpenIddictDbContext>();
        OpenIddictDbContext.ConfigureProvider(optionsBuilder, connectionString, providerName);

        await using OpenIddictDbContext dbContext = new OpenIddictDbContext(optionsBuilder.Options);

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        if (pending.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
