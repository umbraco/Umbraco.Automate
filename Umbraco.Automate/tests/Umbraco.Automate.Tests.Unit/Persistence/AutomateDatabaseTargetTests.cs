using Umbraco.Automate.Core.Persistence.Scoping;

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

    /// <summary>
    /// A rooted directory built with the running platform's own separator. SQLite paths must be
    /// native: <see cref="AutomateDatabaseTarget"/> canonicalises them with <see cref="Path"/>, which
    /// only recognises the separators and rooting rules of the platform it runs on — so a hard-coded
    /// <c>C:\…</c> is neither rooted nor segmented on the Linux and macOS CI agents.
    /// </summary>
    private static readonly string s_dataDirectory =
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "site", "umbraco", "Data"));

    private static string DataSource(params string[] segments)
        => $"Data Source={Path.Combine([s_dataDirectory, .. segments])}";

    [Fact]
    public void IsSameDatabase_TrueForIdenticalSqliteConnectionStrings()
        => AutomateDatabaseTarget.IsSameDatabase(
                $"{DataSource("Umbraco.sqlite.db")};Cache=Shared", Sqlite,
                $"{DataSource("Umbraco.sqlite.db")};Cache=Shared", Sqlite)
            .ShouldBeTrue();

    /// <summary>
    /// Only keyword casing and ordering are asserted here, not filename casing: paths are
    /// case-sensitive on Linux and macOS, so two SQLite files differing only in case are genuinely
    /// two different databases there.
    /// </summary>
    [Fact]
    public void IsSameDatabase_TrueWhenOnlyKeywordsAndTheirOrderDiffer()
        => AutomateDatabaseTarget.IsSameDatabase(
                $"Cache=Shared;{DataSource("Umbraco.sqlite.db")};Pooling=True", Sqlite,
                $"cache=shared;foreign keys=True;data source={Path.Combine(s_dataDirectory, "Umbraco.sqlite.db")}", Sqlite)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_TrueForEquivalentSqlitePaths()
        => AutomateDatabaseTarget.IsSameDatabase(
                DataSource("Umbraco.sqlite.db"), Sqlite,
                DataSource("..", "Data", "Umbraco.sqlite.db"), Sqlite)
            .ShouldBeTrue();

    [Fact]
    public void IsSameDatabase_FalseForDifferentSqliteFiles()
        => AutomateDatabaseTarget.IsSameDatabase(
                DataSource("Umbraco.sqlite.db"), Sqlite,
                DataSource("Umbraco.Automate.sqlite.db"), Sqlite)
            .ShouldBeFalse();

    [Fact]
    public void IsSameDatabase_FalseWhenProvidersDiffer()
        => AutomateDatabaseTarget.IsSameDatabase(
                DataSource("Umbraco.sqlite.db"), Sqlite,
                DataSource("Umbraco.sqlite.db"), SqlServer)
            .ShouldBeFalse();

    [Theory]
    [InlineData("Microsoft.Data.SQLite")]
    [InlineData("microsoft.data.sqlite")]
    public void IsSameDatabase_AcceptsAlternateSqliteProviderSpellings(string providerName)
        => AutomateDatabaseTarget.IsSameDatabase(
                DataSource("Umbraco.sqlite.db"), Sqlite,
                DataSource("Umbraco.sqlite.db"), providerName)
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
            // The placeholder is followed by the platform's own separator, as the host that wrote the
            // connection string would use.
            var placeholderPath = string.Join(
                Path.DirectorySeparatorChar,
                Umbraco.Cms.Core.Constants.System.DataDirectoryPlaceholder,
                "Umbraco.sqlite.db");

            AutomateDatabaseTarget.IsSameDatabase(
                    $"Data Source={Path.Combine(dataDirectory, "Umbraco.sqlite.db")}", Sqlite,
                    $"Data Source={placeholderPath}", Sqlite)
                .ShouldBeTrue();
        }
        finally
        {
            AppDomain.CurrentDomain.SetData(
                Umbraco.Cms.Core.Constants.System.DataDirectoryName, previous);
        }
    }
}
