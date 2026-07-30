using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.Persistence;

/// <inheritdoc cref="IAutomateSchemaInitializer" />
internal sealed class AutomateSchemaInitializer : IAutomateSchemaInitializer, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly ILogger<AutomateSchemaInitializer> _logger;

    // Callers are startup-only (a component, and a notification handler as a safety net), so there is
    // no fast path around the semaphore: correctness is worth more than saving a handful of
    // uncontended waits. Anything hot must wait on AutomateReadinessSignal instead.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _attempted;

    public AutomateSchemaInitializer(
        IConfiguration configuration,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        AutomateReadinessSignal readinessSignal,
        ILogger<AutomateSchemaInitializer> logger)
    {
        _configuration = configuration;
        _connectionStrings = connectionStrings;
        _readinessSignal = readinessSignal;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EnsureMigratedAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_attempted)
            {
                return;
            }

            await MigrateAsync(cancellationToken).ConfigureAwait(false);

            // Only set once MigrateAsync has resolved the readiness signal one way or the other.
            // Cancellation throws out of MigrateAsync and deliberately leaves this false, so a
            // migration abandoned by a shutdown can be retried rather than being remembered as done.
            _attempted = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Create a standalone DbContext rather than using IDbContextFactory. Umbraco's EFCoreScope
            // infrastructure shares NPoco connections (wrapped with MiniProfiler's ProfiledDbConnection)
            // onto pooled EF Core contexts via SetDbConnection(). These tainted contexts cause
            // NullReferenceException in SqliteDatabaseCreator.Exists() when the ProfiledDbConnection's
            // inner connection is disposed. Creating the context directly avoids the pooled factory.
            // See: https://github.com/umbraco/Umbraco-CMS/issues/22124
            //
            // It also keeps this context free of AutomateReadinessInterceptor, which would otherwise
            // wait here for the very signal this method is responsible for setting.
            var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(_connectionStrings, _configuration);
            var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
            UmbracoAutomateDbContext.ConfigureProvider(optionsBuilder, connectionString, providerName);

            await using UmbracoAutomateDbContext dbContext = new UmbracoAutomateDbContext(optionsBuilder.Options);

            List<string> pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            if (pending.Count > 0)
            {
                _logger.LogInformation("Running {Count} pending Automate migrations", pending.Count);
                await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Automate migrations completed successfully");
            }

            _readinessSignal.Signal();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is shutting down mid-startup. Leave the signal unresolved rather than
            // recording a failure that is really just a cancellation.
            throw;
        }
        catch (Exception ex)
        {
            // Every Automate database write is gated on this signal (AutomateReadinessInterceptor).
            // If migrations fail, signal the failure too, so waiters fail fast with a clear exception
            // instead of hanging indefinitely on a signal that will never arrive.
            //
            // Deliberately not rethrown. This runs during component initialization, where an
            // exception would abort the whole Umbraco boot; a broken Automate schema should disable
            // Automate, not take the site down with it.
            _logger.LogError(ex, "Automate startup migrations failed; Automate database writes will be rejected until this is resolved");
            _readinessSignal.SignalFailed(ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
