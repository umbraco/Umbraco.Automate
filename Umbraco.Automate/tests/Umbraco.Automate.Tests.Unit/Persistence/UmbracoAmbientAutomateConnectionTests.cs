using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Tests for <see cref="UmbracoAmbientAutomateConnection"/> — the piece that reads Umbraco's ambient
/// scope and decides whether Automate may share its transaction.
/// </summary>
public class UmbracoAmbientAutomateConnectionTests
{
    private const string CmsConnectionString = @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db";
    private const string SqliteProvider = "Microsoft.Data.Sqlite";

    [Fact]
    public void Transaction_IsAmbientTransaction_WhenAutomateSharesTheUmbracoDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction ambientTransaction = connection.BeginTransaction();

        IAmbientAutomateConnection sut = CreateSut(
            ambientTransaction,
            ("Umbraco:Automate:UseNamedConnectionString", "umbracoDbDSN"));

        sut.Transaction.ShouldBeSameAs(ambientTransaction);
    }

    [Fact]
    public void Transaction_IsNull_WhenAutomateHasItsOwnDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction ambientTransaction = connection.BeginTransaction();

        // Default configuration: Automate resolves umbracoAutomateDbDSN, a different file. Sharing the
        // CMS connection here would write Automate's tables into the CMS database.
        IAmbientAutomateConnection sut = CreateSut(
            ambientTransaction,
            ("ConnectionStrings:umbracoAutomateDbDSN", @"Data Source=C:\site\umbraco\Data\Umbraco.Automate.sqlite.db"),
            ("ConnectionStrings:umbracoAutomateDbDSN_ProviderName", SqliteProvider));

        sut.Transaction.ShouldBeNull();
    }

    [Fact]
    public void Transaction_IsNull_WhenThereIsNoAmbientScope()
    {
        IAmbientAutomateConnection sut = CreateSut(
            ambientTransaction: null,
            ("Umbraco:Automate:UseNamedConnectionString", "umbracoDbDSN"));

        sut.Transaction.ShouldBeNull();
    }

    [Fact]
    public void Transaction_IsNull_WhenAutomateHasNoConnectionStringConfigured()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction ambientTransaction = connection.BeginTransaction();

        // No umbracoAutomateDbDSN entry at all: DatabaseConnectionInfo.Resolve throws, and the real
        // error must surface from the DbContext factory rather than from here.
        IAmbientAutomateConnection sut = CreateSut(ambientTransaction);

        sut.Transaction.ShouldBeNull();
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        return connection;
    }

    private static UmbracoAmbientAutomateConnection CreateSut(
        DbTransaction? ambientTransaction,
        params (string Key, string? Value)[] additionalConfiguration)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = CmsConnectionString,
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = SqliteProvider,
        };

        foreach ((var key, var value) in additionalConfiguration)
        {
            settings[key] = value;
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        // Bind ConnectionStrings the way Umbraco does (UmbracoBuilder.Configuration.cs), so the
        // default option resolves umbracoDbDSN rather than an empty instance.
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddOptions();
        services.AddSingleton<IConfigureOptions<ConnectionStrings>, ConfigureConnectionStrings>();

        IOptionsMonitor<ConnectionStrings> connectionStrings = services
            .BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<ConnectionStrings>>();

        return new UmbracoAmbientAutomateConnection(
            new Lazy<IScopeAccessor>(() => CreateScopeAccessor(ambientTransaction)),
            connectionStrings,
            configuration);
    }

    private static IScopeAccessor CreateScopeAccessor(DbTransaction? ambientTransaction)
    {
        if (ambientTransaction is null)
        {
            return Mock.Of<IScopeAccessor>(accessor => accessor.AmbientScope == null);
        }

        var database = Mock.Of<IUmbracoDatabase>(db => db.Transaction == ambientTransaction);
        var scope = Mock.Of<IScope>(s => s.Database == database);

        return Mock.Of<IScopeAccessor>(accessor => accessor.AmbientScope == scope);
    }
}
