using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Scoping;
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

    /// <summary>
    /// Engine internals ask for the detached factory by type so their writes can never join a caller's
    /// transaction. If the decoration ever swallowed that registration, the engine would silently start
    /// enlisting and nothing else would notice.
    /// </summary>
    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_KeepsThePooledFactoryReachableAsDetached()
    {
        ServiceProvider serviceProvider = BuildServiceProvider();

        IDetachedDbContextFactory<UmbracoAutomateDbContext> detached = serviceProvider
            .GetRequiredService<IDetachedDbContextFactory<UmbracoAutomateDbContext>>();

        detached.ShouldNotBeAssignableTo<AmbientDbContextFactory<UmbracoAutomateDbContext>>();

        using UmbracoAutomateDbContext context = detached.CreateDbContext();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    /// <summary>
    /// The engine stores must take the detached factory, not the ambient one. Constructor injection is
    /// the only thing enforcing that, so it is asserted rather than trusted.
    /// </summary>
    [Theory]
    [InlineData(typeof(EFCoreWorkflowPersistenceProvider))]
    [InlineData(typeof(EFCoreWorkflowLockStore))]
    public void EngineStores_TakeTheDetachedFactory(Type storeType)
    {
        IEnumerable<Type> parameterTypes = storeType
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        parameterTypes.ShouldContain(typeof(IDetachedDbContextFactory<UmbracoAutomateDbContext>));
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
    /// The other tests here substitute <see cref="IAmbientAutomateConnection"/>, so nothing exercises
    /// the implementation the extension actually registers. It depends on
    /// <c>Lazy&lt;IScopeAccessor&gt;</c>, which plain Microsoft DI cannot resolve — only Umbraco's
    /// <c>Lazy&lt;&gt;</c> registration makes it work, and that is a dependency on the host this
    /// package does not own.
    /// </summary>
    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_RegistersAnAmbientConnectionThatResolvesInAnUmbracoHost()
    {
        ServiceProvider serviceProvider = BuildServiceProvider(useRealAmbientConnection: true);

        serviceProvider.GetRequiredService<IAmbientAutomateConnection>()
            .ShouldBeOfType<UmbracoAmbientAutomateConnection>();

        // Resolving the factory and creating a context walks the whole chain, Lazy<IScopeAccessor>
        // included.
        using UmbracoAutomateDbContext context = serviceProvider
            .GetRequiredService<IDbContextFactory<UmbracoAutomateDbContext>>()
            .CreateDbContext();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    /// <summary>
    /// Reproduces https://github.com/umbraco/Umbraco.Automate/issues/226: resolving
    /// <c>IDbContextFactory&lt;UmbracoAutomateDbContext&gt;</c> — which is exactly what the generic
    /// host does while building every <c>IHostedService</c>'s constructor graph at
    /// <c>Host.StartAsync</c>, unconditionally and before Umbraco's runtime level is known — must not
    /// throw just because no connection string has been configured yet (a fresh, not-yet-installed
    /// site; an ephemeral CI boot serving only <c>swagger.json</c>).
    /// </summary>
    [Fact]
    public void EnlistDbContextFactoryInAmbientScope_DoesNotThrowWhenNoConnectionStringIsConfigured()
    {
        ServiceProvider serviceProvider = BuildServiceProvider(configureConnectionString: false);

        // No Should.Throw here is the point: this used to be an unhandled InvalidOperationException
        // that took down the whole host before Umbraco ever got a chance to report RuntimeLevel.Install.
        using UmbracoAutomateDbContext context = serviceProvider
            .GetRequiredService<IDbContextFactory<UmbracoAutomateDbContext>>()
            .CreateDbContext();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    /// <summary>
    /// Replicates the persistence-layer registration block from
    /// <c>UmbracoBuilderExtensions.AddUmbracoAutomatePersistence</c>. Kept in step with it by hand,
    /// as <c>IUmbracoBuilder</c> is not constructible outside a full Umbraco host — except for the
    /// pooled factory's options delegate, which is shared with production via
    /// <c>AutomatePooledDbContextOptions.Configure</c> so this test exercises the real fallback
    /// behaviour rather than a hand-copied approximation of it.
    /// </summary>
    /// <param name="useRealAmbientConnection">
    /// When <c>true</c>, leaves <see cref="IAmbientAutomateConnection"/> to the extension and registers
    /// what an Umbraco host provides for it instead of substituting the whole thing.
    /// </param>
    /// <param name="configureConnectionString">
    /// When <c>false</c>, omits every Automate connection-string entry — the state of a fresh,
    /// not-yet-installed site — instead of the working in-memory SQLite one.
    /// </param>
    private static ServiceProvider BuildServiceProvider(
        bool useRealAmbientConnection = false,
        bool configureConnectionString = true)
    {
        var services = new ServiceCollection();

        IConfiguration configuration = configureConnectionString
            ? new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:umbracoAutomateDbDSN"] = "Data Source=:memory:",
                    ["ConnectionStrings:umbracoAutomateDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
                })
                .Build()
            : new ConfigurationBuilder().Build();

        services.AddSingleton(configuration);
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        services.AddSingleton<AutomateReadinessSignal>();
        services.AddLogging();

        if (useRealAmbientConnection)
        {
            // What the host brings: Umbraco's open-generic Lazy<> support
            // (Umbraco.Cms.Core.DependencyInjection.ServiceCollectionExtensions) and the scope accessor.
            services.AddTransient(typeof(Lazy<>), typeof(LazyResolve<>));
            services.AddSingleton(Mock.Of<IScopeAccessor>());
        }
        else
        {
            services.AddSingleton(Mock.Of<IAmbientAutomateConnection>());
        }

        services.AddUmbracoDbContext<UmbracoAutomateDbContext>(
            AutomatePooledDbContextOptions.Configure,
            shareUmbracoConnection: false);

        services.EnlistDbContextFactoryInAmbientScope(
            (_, connection, providerName) =>
            {
                var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
                UmbracoAutomateDbContext.ConfigureProvider(options, connection, providerName);

                return new UmbracoAutomateDbContext(options.Options);
            });

        return services.BuildServiceProvider();
    }
}
