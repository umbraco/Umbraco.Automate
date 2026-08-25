using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;
using Umbraco.Automate.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.FaultHandling;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Tests for the <c>ConfigureProvider</c> overload that binds the DbContext to a connection it does
/// not own.
/// </summary>
/// <remarks>
/// The connection an ambient Umbraco scope hands over is never a bare provider connection: NPoco
/// applies Umbraco's connection interceptors, which wrap it in a <c>ProfiledDbConnection</c> and a
/// <c>RetryDbConnection</c>. Both providers must accept that. SQL Server has no integration coverage
/// for the enlisted path because it needs a live server, and configuring the provider is where a
/// rejected connection type would surface, so it is at least pinned here.
/// </remarks>
public class EnlistedProviderConfigurationTests
{
    private const string SqliteProvider = Umbraco.Cms.Core.Constants.ProviderNames.SQLLite;
    private const string SqlServerProvider = Umbraco.Cms.Core.Constants.ProviderNames.SQLServer;

    [Fact]
    public void ConfigureProvider_AcceptsAWrappedSqlServerConnection()
    {
        using DbConnection connection = Wrap(
            new SqlConnection("Server=db.example.net;Database=umbraco;Integrated Security=true"));

        using UmbracoAutomateDbContext context = CreateContext(connection, SqlServerProvider);

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.SqlServer");
        context.Database.GetDbConnection().ShouldBeSameAs(connection);
    }

    [Fact]
    public void ConfigureProvider_AcceptsAWrappedSqliteConnection()
    {
        using DbConnection connection = Wrap(new SqliteConnection("Data Source=:memory:"));

        using UmbracoAutomateDbContext context = CreateContext(connection, SqliteProvider);

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
        context.Database.GetDbConnection().ShouldBeSameAs(connection);
    }

    [Fact]
    public void ConfigureProvider_RejectsAnUnsupportedProvider()
        => Should.Throw<InvalidOperationException>(
                () => CreateContext(new SqliteConnection("Data Source=:memory:"), "Npgsql"))
            .Message.ShouldContain("not supported");

    // Mirrors the interceptor order NPoco applies: MiniProfiler first, retry policy outermost.
    private static DbConnection Wrap(DbConnection connection)
        => new RetryDbConnection(
            new ProfiledDbConnection(connection, MiniProfiler.Current),
            conRetryPolicy: null,
            cmdRetryPolicy: null);

    private static UmbracoAutomateDbContext CreateContext(DbConnection connection, string providerName)
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(options, connection, providerName);

        return new UmbracoAutomateDbContext(options.Options);
    }
}
