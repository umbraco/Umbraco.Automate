// Stories S1, S2, S3: docs/plans/531-lazy-dbcontext-factory/STORIES.md (#531)
using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Persistence;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// A site that boots with no Automate connection string must pick one up once it appears, for
/// example after the backoffice installer writes it, without restarting the process. Composes the
/// real <c>AddUmbracoAutomateDbContext</c> registration, as
/// <see cref="AmbientDbContextFactoryCompositionTests"/> does.
/// </summary>
public class ResolvingPooledDbContextFactoryTests
{
    private const string Placeholder = "Data Source=:memory:";
    private const string ProviderName = "Microsoft.Data.Sqlite";

    private const string PlaceholderWarning =
        "Umbraco Automate has no database connection string configured yet. Automate stays inactive " +
        "until one is configured. A connection string added by the installer is picked up straight away; " +
        "one added any other way needs the application to restart.";

    private const string FoundMessage =
        "Umbraco Automate found its database connection string and is now using it.";

    public class GivenNoConnectionStringResolves : IDisposable
    {
        private readonly RecordingLoggerProvider _logs = new();
        private readonly ServiceProvider _serviceProvider;

        public GivenNoConnectionStringResolves()
        {
            _serviceProvider = BuildServiceProvider(new ConfigurationBuilder().Build(), _logs);
        }

        public void Dispose() => _serviceProvider.Dispose();

        [Fact]
        public void CreatesAContextOnThePlaceholder()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            context.Database.GetConnectionString().ShouldBe(Placeholder);
        }

        [Fact]
        public void CreatingAContextDoesNotThrow()
            => Should.NotThrow(() => CreateContext(_serviceProvider).Dispose());

        [Fact]
        public void StaysOnThePlaceholderOnTheNextCreate()
        {
            CreateContext(_serviceProvider).Dispose();

            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            context.Database.GetConnectionString().ShouldBe(Placeholder);
        }

        [Fact]
        public void LogsTheWarningOnceWithTheNewWording()
        {
            CreateContext(_serviceProvider).Dispose();
            CreateContext(_serviceProvider).Dispose();

            _logs.Messages(LogLevel.Warning).ShouldBe([PlaceholderWarning]);
        }
    }

    public class GivenTheConnectionStringIsAddedAfterBoot : IDisposable
    {
        private readonly RecordingLoggerProvider _logs = new();
        private readonly SharedSqliteDatabase _database = new();
        private readonly ServiceProvider _serviceProvider;

        public GivenTheConnectionStringIsAddedAfterBoot()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _serviceProvider = BuildServiceProvider(configuration, _logs);

            // Boot: the factory is resolved and used while no connection string exists.
            CreateContext(_serviceProvider).Dispose();

            // Then the installer writes the connection string.
            configuration["ConnectionStrings:umbracoAutomateDbDSN"] = _database.ConnectionString;
            configuration["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = ProviderName;
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _database.Dispose();
        }

        [Fact]
        public void UsesTheConfiguredConnection()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            context.Database.GetConnectionString().ShouldBe(_database.ConnectionString);
        }

        [Fact]
        public void ReachesTheRealDatabase()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            Should.NotThrow(() => context.Automations.Any());
        }

        [Fact]
        public void TheDetachedFactoryUsesTheConfiguredConnection()
        {
            using UmbracoAutomateDbContext context = _serviceProvider
                .GetRequiredService<IDetachedDbContextFactory<UmbracoAutomateDbContext>>()
                .CreateDbContext();

            context.Database.GetConnectionString().ShouldBe(_database.ConnectionString);
        }

        [Fact]
        public void LogsTheSwitchOnce()
        {
            CreateContext(_serviceProvider).Dispose();
            CreateContext(_serviceProvider).Dispose();

            _logs.Messages(LogLevel.Information).ShouldBe([FoundMessage]);
        }

        [Fact]
        public void KeepsTheReadinessGate()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            context.GetService<IDbContextOptions>()
                .FindExtension<CoreOptionsExtension>()!
                .Interceptors!
                .ShouldContain(interceptor => interceptor is AutomateReadinessInterceptor);
        }

        [Fact]
        public void KeepsAutomatesMigrationsHistoryTable()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>())
                .MigrationsHistoryTableName
                .ShouldBe("__UmbracoAutomate_MigrationsHistory");
        }

        [Fact]
        public void RacingCreatesBuildOnePool()
        {
            Parallel.For(0, 50, _ => CreateContext(_serviceProvider).Dispose());

            _logs.Messages(LogLevel.Information).Count(message => message == FoundMessage).ShouldBe(1);
        }
    }

    public class GivenTheConnectionStringResolvesAtBoot : IDisposable
    {
        private readonly RecordingLoggerProvider _logs = new();
        private readonly SharedSqliteDatabase _database = new();
        private readonly ServiceProvider _serviceProvider;

        public GivenTheConnectionStringResolvesAtBoot()
        {
            _serviceProvider = BuildServiceProvider(ConfiguredFor(_database), _logs);
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _database.Dispose();
        }

        [Fact]
        public void NeverBuildsASecondPool()
        {
            CreateContext(_serviceProvider).Dispose();
            CreateContext(_serviceProvider).Dispose();

            _logs.Messages(LogLevel.Information).ShouldNotContain(FoundMessage);
        }
    }

    public class GivenAnAmbientCmsTransactionOnASharedDatabase : IDisposable
    {
        private readonly SharedSqliteDatabase _database = new();
        private readonly SqliteConnection _ambientConnection;
        private readonly DbTransaction _ambientTransaction;
        private readonly ServiceProvider _serviceProvider;

        public GivenAnAmbientCmsTransactionOnASharedDatabase()
        {
            _ambientConnection = new SqliteConnection(_database.ConnectionString);
            _ambientConnection.Open();
            _ambientTransaction = _ambientConnection.BeginTransaction();

            _serviceProvider = BuildServiceProvider(
                ConfiguredFor(_database),
                new RecordingLoggerProvider(),
                new FixedAmbientConnection(_ambientTransaction));
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _ambientTransaction.Dispose();
            _ambientConnection.Dispose();
            _database.Dispose();
        }

        [Fact]
        public void EnlistsTheContextOnTheAmbientTransaction()
        {
            using UmbracoAutomateDbContext context = CreateContext(_serviceProvider);

            context.Database.CurrentTransaction!.GetDbTransaction().ShouldBeSameAs(_ambientTransaction);
        }
    }

    private static UmbracoAutomateDbContext CreateContext(IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<IDbContextFactory<UmbracoAutomateDbContext>>().CreateDbContext();

    private static IConfiguration ConfiguredFor(SharedSqliteDatabase database)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoAutomateDbDSN"] = database.ConnectionString,
                ["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = ProviderName,
            })
            .Build();

    /// <summary>
    /// The production DbContext registration plus the few host services it needs.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(
        IConfiguration configuration,
        RecordingLoggerProvider logs,
        IAmbientAutomateConnection? ambientConnection = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        services.AddSingleton<AutomateReadinessSignal>();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddSingleton(ambientConnection ?? new FixedAmbientConnection(null));

        services.AddUmbracoAutomateDbContext();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A named shared-cache in-memory SQLite database with Automate's schema, kept alive for the test.
    /// </summary>
    private sealed class SharedSqliteDatabase : IDisposable
    {
        private readonly SqliteConnection _keepAlive;

        public SharedSqliteDatabase()
        {
            ConnectionString = $"Data Source=file:automate531_{Guid.NewGuid():N}?mode=memory&cache=shared";
            _keepAlive = new SqliteConnection(ConnectionString);
            _keepAlive.Open();

            var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>().UseSqlite(ConnectionString).Options;
            using var context = new UmbracoAutomateDbContext(options);
            context.Database.EnsureCreated();
        }

        public string ConnectionString { get; }

        public void Dispose() => _keepAlive.Dispose();
    }

    private sealed class FixedAmbientConnection(DbTransaction? transaction) : IAmbientAutomateConnection
    {
        public DbTransaction? Transaction => transaction;
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<string> Messages(LogLevel level)
            => _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();

        // EF Core's own logging (Microsoft.*) would drown out Automate's lines.
        public ILogger CreateLogger(string categoryName)
            => categoryName.StartsWith("Microsoft.", StringComparison.Ordinal)
                ? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance
                : new RecordingLogger(_entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
