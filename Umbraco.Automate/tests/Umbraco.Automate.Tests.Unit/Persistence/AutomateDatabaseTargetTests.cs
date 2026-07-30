using Umbraco.Automate.Persistence.Scoping;

namespace Umbraco.Automate.Tests.Unit.Persistence;

/// <summary>
/// Tests for <see cref="AutomateDatabaseTarget"/>, the gate that decides whether Automate may enlist
/// in the ambient Umbraco transaction. A false positive here would send Automate's writes down a
/// connection that cannot see its tables, so the interesting cases are the near-misses.
/// </summary>
public class AutomateDatabaseTargetTests
{
    private const string Sqlite = "Microsoft.Data.Sqlite";
    private const string SqlServer = "Microsoft.Data.SqlClient";

    [Fact]
    public void IsSameDatabase_TrueForIdenticalSqliteConnectionStrings()
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db;Cache=Shared", Sqlite,
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db;Cache=Shared", Sqlite)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_TrueWhenOnlyKeywordsAndCasingDiffer()
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Cache=Shared;Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db;Pooling=True", Sqlite,
                @"data source=C:\SITE\umbraco\Data\Umbraco.sqlite.db;Foreign Keys=True", Sqlite)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_TrueForEquivalentSqlitePaths()
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db", Sqlite,
                @"Data Source=C:\site\umbraco\Data\..\Data\Umbraco.sqlite.db", Sqlite)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_FalseForDifferentSqliteFiles()
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db", Sqlite,
                @"Data Source=C:\site\umbraco\Data\Umbraco.Automate.sqlite.db", Sqlite)
            .ShouldBeFalse();

    [Fact]
    public void IsSameDatabase_FalseWhenProvidersDiffer()
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db", Sqlite,
                @"Data Source=C:\site\umbraco\Data\Umbraco.sqlite.db", SqlServer)
            .ShouldBeFalse();

    [Theory]
    [InlineData("Microsoft.Data.SQLite")]
    [InlineData("microsoft.data.sqlite")]
    public void IsSameDatabase_AcceptsAlternateSqliteProviderSpellings(string providerName)
        => AutomateDatabaseTarget.IsSameDatabase(
                @"Data Source=C:\site\Umbraco.sqlite.db", Sqlite,
                @"Data Source=C:\site\Umbraco.sqlite.db", providerName)
            .ShouldBeTrue();

    /// <summary>
    /// Umbraco Cloud and Azure still emit the legacy provider name, and Automate normalises it to the
    /// modern one — so the two sides of a shared Cloud connection legitimately disagree on spelling.
    /// </summary>
    [Fact]
    public void IsSameDatabase_TreatsLegacySqlClientProviderNameAsSqlServer()
        => AutomateDatabaseTarget.IsSameDatabase(
                "Server=tcp:db.example.net;Initial Catalog=umbraco-site;User ID=u;Password=p", "System.Data.SqlClient",
                "Server=tcp:db.example.net;Initial Catalog=umbraco-site;User ID=u;Password=p", SqlServer)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_TrueForSqlServerCatalogSynonyms()
        => AutomateDatabaseTarget.IsSameDatabase(
                "Server=db.example.net;Database=umbraco-site;Integrated Security=true", SqlServer,
                "Data Source=db.example.net;Initial Catalog=umbraco-site;Integrated Security=true", SqlServer)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_TrueForLocalServerSynonyms()
        => AutomateDatabaseTarget.IsSameDatabase(
                "Server=(local);Database=umbraco;Integrated Security=true", SqlServer,
                "Server=localhost;Database=umbraco;Integrated Security=true", SqlServer)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_FalseForDifferentCatalogsOnTheSameServer()
        => AutomateDatabaseTarget.IsSameDatabase(
                "Server=db.example.net;Database=umbraco;Integrated Security=true", SqlServer,
                "Server=db.example.net;Database=umbraco-automate;Integrated Security=true", SqlServer)
            .ShouldBeFalse();

    [Fact]
    public void IsSameDatabase_FalseForTheSameCatalogOnDifferentServers()
        => AutomateDatabaseTarget.IsSameDatabase(
                "Server=db1.example.net;Database=umbraco;Integrated Security=true", SqlServer,
                "Server=db2.example.net;Database=umbraco;Integrated Security=true", SqlServer)
            .ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSameDatabase_FalseWhenEitherConnectionStringIsMissing(string? connectionString)
    {
        const string configured = @"Data Source=C:\site\Umbraco.sqlite.db";

        AutomateDatabaseTarget.IsSameDatabase(connectionString, Sqlite, configured, Sqlite).ShouldBeFalse();
        AutomateDatabaseTarget.IsSameDatabase(configured, Sqlite, connectionString, Sqlite).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Npgsql")]
    public void IsSameDatabase_FalseForUnknownProviders(string? providerName)
        => AutomateDatabaseTarget.IsSameDatabase(
                "Host=db;Database=umbraco", providerName,
                "Host=db;Database=umbraco", providerName)
            .ShouldBeFalse();

    /// <summary>
    /// An in-memory database is identified by name rather than by a path, so it must never be run
    /// through path normalisation — two differently-named in-memory databases are not the same one.
    /// </summary>
    [Fact]
    public void IsSameDatabase_ComparesInMemorySqliteDatabasesByName()
    {
        AutomateDatabaseTarget.IsSameDatabase(
                "Data Source=file:shared?mode=memory&cache=shared", Sqlite,
                "Data Source=file:shared?mode=memory&cache=shared", Sqlite)
            .ShouldBeTrue();

        AutomateDatabaseTarget.IsSameDatabase(
                "Data Source=file:one?mode=memory&cache=shared", Sqlite,
                "Data Source=file:two?mode=memory&cache=shared", Sqlite)
            .ShouldBeFalse();
    }

    /// <summary>
    /// Umbraco resolves <c>|DataDirectory|</c> before EF Core sees its connection string; Automate
    /// reads its own straight from configuration. Both spellings must resolve to the same file.
    /// </summary>
    [Fact]
    public void IsSameDatabase_ResolvesTheDataDirectoryPlaceholder()
    {
        var dataDirectory = Path.Combine(Path.GetTempPath(), "automate-data-directory-test");
        var previous = AppDomain.CurrentDomain.GetData(
            Umbraco.Cms.Core.Constants.System.DataDirectoryName);

        AppDomain.CurrentDomain.SetData(
            Umbraco.Cms.Core.Constants.System.DataDirectoryName, dataDirectory);

        try
        {
            AutomateDatabaseTarget.IsSameDatabase(
                    $@"Data Source={Path.Combine(dataDirectory, "Umbraco.sqlite.db")}", Sqlite,
                    @"Data Source=|DataDirectory|\Umbraco.sqlite.db", Sqlite)
                .ShouldBeTrue();
        }
        finally
        {
            AppDomain.CurrentDomain.SetData(
                Umbraco.Cms.Core.Constants.System.DataDirectoryName, previous);
        }
    }
}
