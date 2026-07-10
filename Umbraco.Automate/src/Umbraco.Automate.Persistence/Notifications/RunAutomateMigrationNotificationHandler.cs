using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;
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
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly ILogger<RunAutomateMigrationNotificationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RunAutomateMigrationNotificationHandler"/>.
    /// </summary>
    public RunAutomateMigrationNotificationHandler(
        IConfiguration configuration,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        AutomateReadinessSignal readinessSignal,
        ILogger<RunAutomateMigrationNotificationHandler> logger)
    {
        _configuration = configuration;
        _connectionStrings = connectionStrings;
        _readinessSignal = readinessSignal;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a standalone DbContext rather than using IDbContextFactory. Umbraco's EFCoreScope
            // infrastructure shares NPoco connections (wrapped with MiniProfiler's ProfiledDbConnection)
            // onto pooled EF Core contexts via SetDbConnection(). These tainted contexts cause
            // NullReferenceException in SqliteDatabaseCreator.Exists() when the ProfiledDbConnection's
            // inner connection is disposed. Creating the context directly avoids the pooled factory.
            // See: https://github.com/umbraco/Umbraco-CMS/issues/22124
            var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(_connectionStrings, _configuration);
            var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
            UmbracoAutomateDbContext.ConfigureProvider(optionsBuilder, connectionString, providerName);

            await using UmbracoAutomateDbContext dbContext = new UmbracoAutomateDbContext(optionsBuilder.Options);

            IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                _logger.LogInformation("Running {Count} pending Automate migrations", pending.Count());
                await dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Automate migrations completed successfully");
            }

            _readinessSignal.Signal();
        }
        catch (Exception ex)
        {
            // Every Automate database write is gated on this signal (AutomateReadinessInterceptor).
            // If migrations fail, signal the failure too, so waiters fail fast with a clear exception
            // instead of hanging indefinitely on a signal that will never arrive.
            _logger.LogError(ex, "Automate startup migrations failed; Automate database writes will be rejected until this is resolved");
            _readinessSignal.SignalFailed(ex);
            throw;
        }
    }
}
