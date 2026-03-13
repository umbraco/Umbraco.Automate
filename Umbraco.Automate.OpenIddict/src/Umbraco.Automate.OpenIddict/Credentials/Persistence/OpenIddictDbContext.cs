using Microsoft.EntityFrameworkCore;

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
            entity.Property(e => e.ExpiresUtc);
            entity.Property(e => e.Scopes).HasMaxLength(2000);
            entity.Property(e => e.AccountLabel).HasMaxLength(500);
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Provider);
        });
    }
}
