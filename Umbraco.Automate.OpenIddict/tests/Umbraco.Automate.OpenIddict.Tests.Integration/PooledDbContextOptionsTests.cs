using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// Composition guard for the pooled <see cref="OpenIddictDbContext"/> factory registered by
/// <c>AddPersistence</c>.
/// </summary>
public class PooledDbContextOptionsTests
{
    /// <summary>
    /// Reproduces https://github.com/umbraco/Umbraco.Automate/issues/487: resolving
    /// <c>IDbContextFactory&lt;OpenIddictDbContext&gt;</c>, which the generic host does while building
    /// every <c>IHostedService</c>'s constructor graph at <c>Host.StartAsync</c>, must not throw just
    /// because no connection string has been configured yet (a fresh, not-yet-installed site; an
    /// ephemeral CI boot serving only <c>swagger.json</c>).
    /// </summary>
    [Fact]
    public void ResolvingTheFactory_DoesNotThrowWhenNoConnectionStringIsConfigured()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddSingleton(configuration);
        services.Configure<ConnectionStrings>(configuration.GetSection("ConnectionStrings"));
        services.AddLogging();
        services.AddSingleton<IAmbientAutomateConnection, NoAmbientConnection>();

        // The DbContext registration AddPersistence uses, so this exercises the real fallback.
        services.AddOpenIddictDbContext();

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        using OpenIddictDbContext context = serviceProvider
            .GetRequiredService<IDbContextFactory<OpenIddictDbContext>>()
            .CreateDbContext();

        context.Database.ProviderName.ShouldBe("Microsoft.EntityFrameworkCore.Sqlite");
    }

    private sealed class NoAmbientConnection : IAmbientAutomateConnection
    {
        public DbTransaction? Transaction => null;
    }
}
