using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// Notification handler that runs pending EF Core migrations for the OpenIddict credential table on startup.
/// </summary>
internal sealed class RunOpenIddictMigrationNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<RunOpenIddictMigrationNotificationHandler> _logger;

    public RunOpenIddictMigrationNotificationHandler(
        IConfiguration configuration,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        IRuntimeState runtimeState,
        ILogger<RunOpenIddictMigrationNotificationHandler> logger)
    {
        _configuration = configuration;
        _connectionStrings = connectionStrings;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        // UmbracoApplicationStartedNotification is published at every runtime level, including
        // Install, where there may be no connection string at all (a fresh site, or a DB-less CI
        // boot). There is nothing to migrate against below Run, and resolving the connection string
        // there would throw. The CMS restarts the runtime once an install completes, and that restart
        // publishes this notification again at Run. Same guard as Automate's own schema initializer.
        // See https://github.com/umbraco/Umbraco.Automate/issues/487.
        RuntimeLevel runtimeLevel = _runtimeState.Level;
        if (runtimeLevel != RuntimeLevel.Run)
        {
            _logger.LogDebug(
                "Skipping Automate OpenIddict migrations because the runtime level is {RuntimeLevel}.",
                runtimeLevel);

            return;
        }

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
