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

    /// <summary>
    /// Configures the EF Core database provider with the correct migrations assembly.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
    {
        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.SqlServer");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                    x.EnableRetryOnFailure();
                });
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.Sqlite");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
        }
    }

    /// <summary>
    /// Configures the EF Core database provider against an already-open connection owned by someone
    /// else — the ambient Umbraco scope — so writes join that connection's transaction.
    /// </summary>
    /// <remarks>
    /// Deliberately does not enable retry-on-failure for SQL Server: EF Core's retrying execution
    /// strategy refuses to run inside a user-initiated transaction, which is exactly what an enlisted
    /// context always has. Retries are the caller's business here, since the caller owns the
    /// transaction that any retry would have to restart.
    /// </remarks>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string providerName)
    {
        switch (providerName)
        {
            case Umbraco.Cms.Core.Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connection, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.SqlServer");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                });
                break;

            case Umbraco.Cms.Core.Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connection, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.OpenIddict.Persistence.Sqlite");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Database provider '{providerName}' is not supported. Supported: SQL Server, SQLite.");
        }
    }

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
