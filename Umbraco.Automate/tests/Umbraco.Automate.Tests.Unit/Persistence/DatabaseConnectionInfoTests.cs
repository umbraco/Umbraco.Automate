using Microsoft.Extensions.Configuration;
using Shouldly;
using Umbraco.Automate.Core.Persistence;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class DatabaseConnectionInfoTests
{
    [Fact]
    public void Resolve_with_dedicated_dsn_returns_it()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoAutomateDbDSN"] = "Server=automate;Database=Automate;",
            ["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
        });

        var (cs, provider) = DatabaseConnectionInfo.Resolve(config);

        cs.ShouldBe("Server=automate;Database=Automate;");
        provider.ShouldBe("Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Resolve_with_use_umbraco_db_dsn_falls_back_to_cms_connection()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = "Server=cms;Database=Umbraco;",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
            ["Umbraco:Automate:UseUmbracoDbDSN"] = "true",
        });

        var (cs, provider) = DatabaseConnectionInfo.Resolve(config);

        cs.ShouldBe("Server=cms;Database=Umbraco;");
        provider.ShouldBe("Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Resolve_normalises_legacy_System_Data_SqlClient_provider()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = "Server=cms;Database=Umbraco;",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "System.Data.SqlClient",
            ["Umbraco:Automate:UseUmbracoDbDSN"] = "true",
        });

        var (_, provider) = DatabaseConnectionInfo.Resolve(config);

        provider.ShouldBe("Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Resolve_throws_when_both_dedicated_dsn_and_flag_are_set()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoAutomateDbDSN"] = "Server=automate;",
            ["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
            ["ConnectionStrings:umbracoDbDSN"] = "Server=cms;",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
            ["Umbraco:Automate:UseUmbracoDbDSN"] = "true",
        });

        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config))
            .Message.ShouldContain("Both");
    }

    [Fact]
    public void Resolve_throws_when_flag_is_set_but_umbraco_dsn_is_missing()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Umbraco:Automate:UseUmbracoDbDSN"] = "true",
        });

        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config))
            .Message.ShouldContain("umbracoDbDSN");
    }

    [Fact]
    public void Resolve_throws_when_nothing_is_configured()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config))
            .Message.ShouldContain("requires an explicit database configuration");
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
