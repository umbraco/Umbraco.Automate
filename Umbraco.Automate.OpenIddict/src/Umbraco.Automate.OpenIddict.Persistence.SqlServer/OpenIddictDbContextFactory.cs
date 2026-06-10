using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;

namespace Umbraco.Automate.OpenIddict.Persistence.SqlServer;

/// <summary>
/// Design-time factory for creating <see cref="OpenIddictDbContext"/> with SQL Server.
/// </summary>
public class OpenIddictDbContextFactory : IDesignTimeDbContextFactory<OpenIddictDbContext>
{
    /// <inheritdoc />
    public OpenIddictDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OpenIddictDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.;Database=UmbracoAutomate;Trusted_Connection=True;",
            x =>
            {
                x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.SqlServer");
                x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
            });
        return new OpenIddictDbContext(optionsBuilder.Options);
    }
}
