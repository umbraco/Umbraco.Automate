using Microsoft.Extensions.Configuration;
using Shouldly;
using Umbraco.Automate.Core.Persistence;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class DatabaseConnectionInfoTests
{
    [Fact]
    public void Resolve_uses_umbracoAutomateDbDSN_by_default()
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
    public void Resolve_follows_UseNamedConnectionString_to_a_different_entry()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = "Server=cms;Database=Umbraco;",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.SqlClient",
            ["Umbraco:Automate:UseNamedConnectionString"] = "umbracoDbDSN",
        });

        var (cs, provider) = DatabaseConnectionInfo.Resolve(config);

        cs.ShouldBe("Server=cms;Database=Umbraco;");
        provider.ShouldBe("Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Resolve_can_target_any_named_connection_string()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SharedProductDb"] = "Server=shared;Database=Shared;",
            ["ConnectionStrings:SharedProductDb_ProviderName"] = "Microsoft.Data.SqlClient",
            ["Umbraco:Automate:UseNamedConnectionString"] = "SharedProductDb",
        });

        var (cs, _) = DatabaseConnectionInfo.Resolve(config);

        cs.ShouldBe("Server=shared;Database=Shared;");
    }

    [Fact]
    public void Resolve_normalises_legacy_System_Data_SqlClient_provider()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = "Server=cms;Database=Umbraco;",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "System.Data.SqlClient",
            ["Umbraco:Automate:UseNamedConnectionString"] = "umbracoDbDSN",
        });

        var (_, provider) = DatabaseConnectionInfo.Resolve(config);

        provider.ShouldBe("Microsoft.Data.SqlClient");
    }

    [Fact]
    public void Resolve_throws_when_the_named_connection_string_does_not_exist()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Umbraco:Automate:UseNamedConnectionString"] = "doesNotExist",
        });

        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config))
            .Message.ShouldContain("doesNotExist");
    }

    [Fact]
    public void Resolve_throws_when_no_connection_string_is_configured()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config))
            .Message.ShouldContain("umbracoAutomateDbDSN");
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
