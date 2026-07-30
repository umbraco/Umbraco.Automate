using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Extensions;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Composition guard for the <see cref="IDbContextFactory{TContext}"/> decoration in
/// <c>AddUmbracoAutomatePersistence</c>. The decoration replaces the descriptor that
/// <c>AddUmbracoDbContext</c> registered rather than adding one, so it is only correct as long as
/// the pooled factory is still resolvable through it — a silent failure here would put every
/// Automate query back on its own connection, and the SQLite deadlock back with it.
/// </summary>
public class AmbientDbContextFactoryCompositionTests
{
    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_ReplacesTheResolvedFactory()
    {
        ServiceProvider serviceProvider = BuildServiceProvider();

        serviceProvider.GetRequiredService<IDbContextFactory<UmbracoAutomateDbContext>>()
            .ShouldBeOfType<AmbientDbContextFactory<UmbracoAutomateDbContext>>();
    }

    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_StillCreatesAWorkingContext()
    {
        ServiceProvider serviceProvider = BuildServiceProvider();

        // No ambient Umbraco scope here, so this exercises the pooled factory the decorator wraps —
        // proving the replaced descriptor is still resolvable and correctly configured.
        using UmbracoAutomateDbContext context = serviceProvider
            .GetRequiredService<IDbContextFactory<UmbracoAutomateDbContext>>()
            .CreateDbContext();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_KeepsTheTransientDbContextRegistrationWorking()
    {
        ServiceProvider serviceProvider = BuildServiceProvider();

        using UmbracoAutomateDbContext context = serviceProvider
            .GetRequiredService<UmbracoAutomateDbContext>();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    /// <summary>
    /// Replicates the persistence-layer registration block from
    /// <c>UmbracoBuilderExtensions.AddUmbracoAutomatePersistence</c>. Kept in step with it by hand,
    /// as <c>IUmbracoBuilder</c> is not constructible outside a full Umbraco host.
    /// </summary>
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoAutomateDbDSN"] = "Data Source=:memory:",
                ["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            })
            .Build();

        services.AddSingleton(configuration);
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        services.AddSingleton<AutomateReadinessSignal>();
        services.AddSingleton(Mock.Of<IAmbientAutomateConnection>());

        services.AddUmbracoDbContext<UmbracoAutomateDbContext>(
            (IServiceProvider serviceProvider, DbContextOptionsBuilder options, string? _, string? _) =>
            {
                var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());
                UmbracoAutomateDbContext.ConfigureProvider(options, connectionString, providerName);
            },
            shareUmbracoConnection: false);

        services.EnlistDbContextFactoryInAmbientScope(
            (serviceProvider, connection) =>
            {
                var (_, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());

                var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
                UmbracoAutomateDbContext.ConfigureProvider(options, connection, providerName);

                return new UmbracoAutomateDbContext(options.Options);
            });

        return services.BuildServiceProvider();
    }
}
