using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;

namespace Umbraco.Automate.OpenIddict.Persistence.Sqlite;

/// <summary>
/// Design-time factory for creating <see cref="OpenIddictDbContext"/> with SQLite.
/// </summary>
public class OpenIddictDbContextFactory : IDesignTimeDbContextFactory<OpenIddictDbContext>
{
    /// <inheritdoc />
    public OpenIddictDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenIddictDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=UmbracoAutomateOpenIddict.db",
            x => x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.Sqlite"));
        return new OpenIddictDbContext(optionsBuilder.Options);
    }
}
