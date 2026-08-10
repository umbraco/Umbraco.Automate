using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Persistence;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// EF Core database context for Umbraco Automate OpenIddict entities.
/// Owns the OAuth credential table only — OpenIddict's own tables are managed by CMS.
/// </summary>
public class OpenIddictDbContext : DbContext
{
    internal DbSet<OAuthCredentialsEntity> OAuthCredentials { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIddictDbContext"/> class.
    /// </summary>
    public OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options)
        : base(options)
    {
    }

    private static readonly AutomateMigrationsAssemblies MigrationsAssemblies = new(
        SqlServer: "Umbraco.Automate.OpenIddict.Persistence.SqlServer",
        Sqlite: "Umbraco.Automate.OpenIddict.Persistence.Sqlite");

    /// <summary>
    /// Configures the EF Core database provider with the correct migrations assembly.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
        => AutomateDbProvider.Configure(options, connectionString, providerName, MigrationsAssemblies);

    /// <summary>
    /// Configures the EF Core database provider against an already-open connection owned by someone
    /// else — the ambient Umbraco scope — so writes join that connection's transaction.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string providerName)
        => AutomateDbProvider.Configure(options, connection, providerName, MigrationsAssemblies);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OAuthCredentialsEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateOpenIddictCredentials");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Provider).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccessToken).IsRequired();
            entity.Property(e => e.RefreshToken);
            entity.Property(e => e.UserAccessToken);
            entity.Property(e => e.ExpiresUtc);
            entity.Property(e => e.Scopes).HasMaxLength(2000);
            entity.Property(e => e.AccountLabel).HasMaxLength(500);
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Provider);
        });
    }
}
