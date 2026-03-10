using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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
        optionsBuilder.UseSqlServer("Server=.;Database=UmbracoAutomate;Trusted_Connection=True;");
        return new UmbracoAutomateDbContext(optionsBuilder.Options);
    }
}
