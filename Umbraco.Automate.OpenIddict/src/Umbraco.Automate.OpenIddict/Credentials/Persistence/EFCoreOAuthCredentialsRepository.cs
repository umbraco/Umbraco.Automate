using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.OpenIddict.Credentials.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IOAuthCredentialsRepository"/>.
/// </summary>
internal sealed class EFCoreOAuthCredentialsRepository : IOAuthCredentialsRepository
{
    private readonly IEFCoreScopeProvider<OpenIddictDbContext> _scopeProvider;
    private readonly OAuthCredentialsFactory _factory;

    public EFCoreOAuthCredentialsRepository(
        IEFCoreScopeProvider<OpenIddictDbContext> scopeProvider,
        OAuthCredentialsFactory factory)
    {
        _scopeProvider = scopeProvider;
        _factory = factory;
    }

    public async Task<OAuthCredentials?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<OpenIddictDbContext> scope = _scopeProvider.CreateScope();
        var result = await scope.ExecuteWithContextAsync<OAuthCredentials?>(async db =>
        {
            var entity = await db.OAuthCredentials.FindAsync([id], cancellationToken);
            return entity is null ? null : _factory.BuildDomain(entity);
        });
        scope.Complete();
        return result;
    }

    public async Task<OAuthCredentials> SaveAsync(OAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<OpenIddictDbContext> scope = _scopeProvider.CreateScope();
        await scope.ExecuteWithContextAsync<object?>(async db =>
        {
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
            return null;
        });
        scope.Complete();
        return credentials;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<OpenIddictDbContext> scope = _scopeProvider.CreateScope();
        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.OAuthCredentials.FindAsync([id], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            db.OAuthCredentials.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });
        scope.Complete();
        return deleted;
    }
}
