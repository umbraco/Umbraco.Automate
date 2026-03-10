using Microsoft.EntityFrameworkCore;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// EF Core database context for Umbraco Automate entities.
/// </summary>
public class UmbracoAutomateDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoAutomateDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public UmbracoAutomateDbContext(DbContextOptions<UmbracoAutomateDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TODO: Configure entity mappings
    }
}
