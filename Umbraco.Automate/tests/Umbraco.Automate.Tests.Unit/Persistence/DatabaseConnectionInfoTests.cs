using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.Configuration.Models;

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

    [Fact]
    public void Resolve_triggers_options_pipeline_so_a_deferred_connection_string_is_materialised()
    {
        // Mirrors Umbraco Cloud / Deploy: umbracoDbDSN is absent from configuration and is only
        // synthesised when the ConnectionStrings options are first resolved.
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Umbraco:Automate:UseNamedConnectionString"] = "umbracoDbDSN",
        });

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IConfigureOptions<ConnectionStrings>>(
            new DeferredConnectionStringConfigurator(config));
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>();

        // Not present until the options pipeline runs.
        config.GetConnectionString("umbracoDbDSN").ShouldBeNullOrEmpty();

        // The IConfiguration-only overload cannot see it.
        Should.Throw<InvalidOperationException>(() => DatabaseConnectionInfo.Resolve(config));

        // The options-aware overload triggers the configurator, which populates it.
        var (cs, providerName) = DatabaseConnectionInfo.Resolve(monitor, config);

        cs.ShouldBe("Data Source=Umbraco.sqlite.db;");
        providerName.ShouldBe("Microsoft.Data.Sqlite");
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    /// <summary>
    /// Stand-in for Umbraco Deploy's <c>ConfigureConnectionStrings</c>: writes a generated DSN
    /// back into <see cref="IConfiguration"/> only when the options are resolved.
    /// </summary>
    private sealed class DeferredConnectionStringConfigurator : IConfigureNamedOptions<ConnectionStrings>
    {
        private readonly IConfiguration _config;

        public DeferredConnectionStringConfigurator(IConfiguration config) => _config = config;

        public void Configure(ConnectionStrings options) => Configure(Options.DefaultName, options);

        public void Configure(string? name, ConnectionStrings options)
        {
            _config["ConnectionStrings:umbracoDbDSN"] = "Data Source=Umbraco.sqlite.db;";
            _config["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite";
            options.ConnectionString = "Data Source=Umbraco.sqlite.db;";
            options.ProviderName = "Microsoft.Data.Sqlite";
        }
    }
}
