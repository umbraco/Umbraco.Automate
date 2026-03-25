using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IOAuthCredentialsRepository"/>.
/// Uses <see cref="IDbContextFactory{TContext}"/> directly (not Umbraco's EFCoreScope)
/// to ensure the dedicated <c>umbracoAutomateDbDSN</c> connection string is used at runtime.
/// </summary>
internal sealed class EFCoreOAuthCredentialsRepository : IOAuthCredentialsRepository
{
    private readonly IDbContextFactory<OpenIddictDbContext> _dbContextFactory;
    private readonly OAuthCredentialsFactory _factory;

    public EFCoreOAuthCredentialsRepository(
        IDbContextFactory<OpenIddictDbContext> dbContextFactory,
        OAuthCredentialsFactory factory)
    {
        _dbContextFactory = dbContextFactory;
        _factory = factory;
    }

    public async Task<OAuthCredentials?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.OAuthCredentials.FindAsync([id], cancellationToken);
        return entity is null ? null : _factory.BuildDomain(entity);
    }

    public async Task<OAuthCredentials> SaveAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.OAuthCredentials.FindAsync([credentials.Id], cancellationToken);
        if (existing is null)
        {
            OAuthCredentialsEntity entity = _factory.BuildEntity(credentials);
            db.OAuthCredentials.Add(entity);
        }
        else
        {
            _factory.UpdateEntity(existing, credentials);
        }

        await db.SaveChangesAsync(cancellationToken);
        return credentials;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.OAuthCredentials.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.OAuthCredentials.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
