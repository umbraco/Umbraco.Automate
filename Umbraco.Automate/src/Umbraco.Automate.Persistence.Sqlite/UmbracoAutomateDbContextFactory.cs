using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Umbraco.Automate.Persistence.Sqlite;

/// <summary>
/// Design-time factory for creating <see cref="UmbracoAutomateDbContext"/> with SQLite.
/// </summary>
public class UmbracoAutomateDbContextFactory : IDesignTimeDbContextFactory<UmbracoAutomateDbContext>
{
    /// <inheritdoc />
    public UmbracoAutomateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        optionsBuilder.UseSqlite(
            "Data Source=UmbracoAutomate.db",
            x => x.MigrationsAssembly("Umbraco.Automate.Persistence.Sqlite"));
        return new UmbracoAutomateDbContext(optionsBuilder.Options);
    }
}
