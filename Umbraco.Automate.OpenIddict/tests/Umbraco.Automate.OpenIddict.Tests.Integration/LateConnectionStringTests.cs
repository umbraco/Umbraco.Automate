// Story S4: docs/plans/531-lazy-dbcontext-factory/STORIES.md (#531)
using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// OAuth connections on a site that boots with no Automate connection string must pick one up
/// once it appears, without restarting the process. Composes the real <c>AddOpenIddictDbContext</c>
/// registration, as <see cref="PooledDbContextOptionsTests"/> does.
/// </summary>
public class LateConnectionStringTests
{
    private const string Placeholder = "Data Source=:memory:";
    private const string RealConnectionString = "Data Source=file:openiddict531?mode=memory&cache=shared";

    private const string PlaceholderWarning =
        "Umbraco Automate OpenIddict has no database connection string configured yet. OAuth connections " +
        "stay inactive until one is configured. A connection string added by the installer is picked up " +
        "straight away; one added any other way needs the application to restart.";

    private const string FoundMessage =
        "Umbraco Automate OpenIddict found its database connection string and OAuth connections are now available.";

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
            using OpenIddictDbContext context = CreateContext(_serviceProvider);

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
        private readonly ServiceProvider _serviceProvider;

        public GivenTheConnectionStringIsAddedAfterBoot()
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _serviceProvider = BuildServiceProvider(configuration, _logs);

            // Boot: the factory is resolved and used while no connection string exists.
            CreateContext(_serviceProvider).Dispose();

            // Then the installer writes the connection string.
            configuration["ConnectionStrings:umbracoAutomateDbDSN"] = RealConnectionString;
            configuration["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = "Microsoft.Data.Sqlite";
        }

        public void Dispose() => _serviceProvider.Dispose();

        [Fact]
        public void UsesTheConfiguredConnection()
        {
            using OpenIddictDbContext context = CreateContext(_serviceProvider);

            context.Database.GetConnectionString().ShouldBe(RealConnectionString);
        }

        [Fact]
        public void LogsTheSwitchOnce()
        {
            CreateContext(_serviceProvider).Dispose();
            CreateContext(_serviceProvider).Dispose();

            _logs.Messages(LogLevel.Information).ShouldBe([FoundMessage]);
        }
    }

    private static OpenIddictDbContext CreateContext(IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>().CreateDbContext();

    /// <summary>
    /// The production DbContext registration plus the few host services it needs.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(IConfiguration configuration, RecordingLoggerProvider logs)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddSingleton<IAmbientAutomateConnection, NoAmbientConnection>();

        services.AddOpenIddictDbContext();

        return services.BuildServiceProvider();
    }

    private sealed class NoAmbientConnection : IAmbientAutomateConnection
    {
        public DbTransaction? Transaction => null;
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<string> Messages(LogLevel level)
            => _entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToList();

        // EF Core's own logging (Microsoft.*) would drown out Automate's lines.
        public ILogger CreateLogger(string categoryName)
            => categoryName.StartsWith("Microsoft.", StringComparison.Ordinal)
                ? NullLogger.Instance
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
