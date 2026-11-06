using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// How <see cref="ResolvingPooledDbContextFactory{TDbContext}"/> builds the real pool once a
/// connection string appears.
/// </summary>
/// <param name="ConfigureProvider">
/// Applies the provider and interceptors to the options, for the resolved connection string and
/// provider name. Must match what the DI pool's options delegate applies.
/// </param>
/// <param name="FoundMessage">The Information line logged once when the real pool is built.</param>
/// <param name="LoggerCategory">The logger category for that line.</param>
internal sealed record ResolvingPooledFactorySettings(
    Action<IServiceProvider, DbContextOptionsBuilder, string, string> ConfigureProvider,
    string FoundMessage,
    string LoggerCategory);

/// <summary>
/// Wraps the DI-registered pooled factory so a connection string that only appears after the pool was
/// built on <see cref="DatabaseConnectionInfo.PlaceholderConnection"/> is still picked up.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddPooledDbContextFactory</c> fixes its options when the factory is first resolved, which on a
/// not-yet-installed site is before any connection string exists (see #226). The installer then
/// writes one without a process restart, so the pool would stay on the placeholder for good. While the
/// state says the DI pool was built on the placeholder, this factory retries
/// <see cref="DatabaseConnectionInfo.Resolve(IOptionsMonitor{ConnectionStrings}, IConfiguration)"/>
/// on each create; the first success builds a real pool once, which is used from then on. A site that
/// had its connection string at boot only pays one flag check per create.
/// </para>
/// <para>
/// The real pool is never replaced, and the placeholder pool is abandoned rather than disposed: it
/// holds no live connections, as an in-memory SQLite context was never used.
/// </para>
/// <para>
/// The real pool replays only the caller's provider-setup delegate, not any separate
/// <c>ConfigureDbContext&lt;T&gt;</c> registrations. "Built once" assumes this factory is a singleton,
/// which is the lifetime <c>AddPooledDbContextFactory</c> registers.
/// </para>
/// </remarks>
internal sealed class ResolvingPooledDbContextFactory<TDbContext> : IDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    // Matches the pool size AddPooledDbContextFactory uses by default.
    private const int DefaultPoolSize = 1024;

    private readonly IDbContextFactory<TDbContext> _diPool;
    private readonly PlaceholderConnectionState<TDbContext> _state;
    private readonly IServiceProvider _serviceProvider;
    private readonly ResolvingPooledFactorySettings _settings;
    private readonly object _buildLock = new();

    private volatile IDbContextFactory<TDbContext>? _realPool;

    public ResolvingPooledDbContextFactory(
        IDbContextFactory<TDbContext> diPool,
        PlaceholderConnectionState<TDbContext> state,
        IServiceProvider serviceProvider,
        ResolvingPooledFactorySettings settings)
    {
        _diPool = diPool;
        _state = state;
        _serviceProvider = serviceProvider;
        _settings = settings;
    }

    /// <inheritdoc />
    public TDbContext CreateDbContext() => CurrentPool().CreateDbContext();

    /// <inheritdoc />
    public Task<TDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => CurrentPool().CreateDbContextAsync(cancellationToken);

    private IDbContextFactory<TDbContext> CurrentPool()
    {
        // The flag is set once (false to true) while the DI pool is constructed, before this wrapper
        // exists, and never flips back. _realPool is only ever set when the flag is true, so a site
        // whose DI pool was real at boot pays one flag read per create.
        if (!_state.BuiltOnPlaceholder)
        {
            return _diPool;
        }

        var realPool = _realPool;
        if (realPool is not null)
        {
            return realPool;
        }

        return TryBuildRealPool() ?? _diPool;
    }

    private IDbContextFactory<TDbContext>? TryBuildRealPool()
    {
        lock (_buildLock)
        {
            var existing = _realPool;
            if (existing is not null)
            {
                return existing;
            }

            string connectionString;
            string providerName;
            try
            {
                (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                    _serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    _serviceProvider.GetRequiredService<IConfiguration>());
            }
            catch (InvalidOperationException)
            {
                // Still no connection string: stay on the DI pool and look again on the next create.
                return null;
            }

            var options = new DbContextOptionsBuilder<TDbContext>();
            options.UseApplicationServiceProvider(_serviceProvider);
            _settings.ConfigureProvider(_serviceProvider, options, connectionString, providerName);

            var realPool = new PooledDbContextFactory<TDbContext>(options.Options, DefaultPoolSize);
            _realPool = realPool;

            // GetService, not GetRequiredService: logging must never be the reason a create fails.
            (_serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
                .CreateLogger(_settings.LoggerCategory)
                .LogInformation("{Message}", _settings.FoundMessage);

            return realPool;
        }
    }
}
