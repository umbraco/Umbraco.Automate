using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Automate.Core.Persistence;

namespace Umbraco.Automate.Persistence.SqlServer;

/// <summary>
/// Design-time factory for creating <see cref="UmbracoAutomateDbContext"/> with SQL Server.
/// </summary>
public class UmbracoAutomateDbContextFactory : IDesignTimeDbContextFactory<UmbracoAutomateDbContext>
{
    /// <inheritdoc />
    public UmbracoAutomateDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.;Database=UmbracoAutomate;Trusted_Connection=True;",
            x =>
            {
                x.MigrationsAssembly("Umbraco.Automate.Persistence.SqlServer");
                x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
            });
        return new UmbracoAutomateDbContext(optionsBuilder.Options);
    }
}
