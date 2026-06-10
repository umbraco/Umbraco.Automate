using Umbraco.Automate.Core.Security;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// Integration tests for OAuth credentials persistence via real EF Core + in-memory SQLite.
/// </summary>
public class CredentialsPersistenceTests : IDisposable
{
    private readonly OpenIddictTestFixture _fixture;
    private readonly EFCoreOAuthCredentialsRepository _repository;

    public CredentialsPersistenceTests()
    {
        _fixture = new OpenIddictTestFixture();
        _repository = new EFCoreOAuthCredentialsRepository(new TestDbContextFactory(_fixture), new OAuthCredentialsFactory(new PassthroughFieldProtector()));
    }

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var credentials = new OAuthCredentials
        {
            Id = Guid.NewGuid(),
            Provider = "Slack",
            AccessToken = "xoxb-test-token",
            RefreshToken = "xoxr-refresh",
            ExpiresUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Scopes = "chat:write,channels:read",
            AccountLabel = "Test Workspace",
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        await _repository.SaveAsync(credentials);

        var loaded = await _repository.GetAsync(credentials.Id);

        loaded.ShouldNotBeNull();
        loaded.Provider.ShouldBe("Slack");
        loaded.AccessToken.ShouldBe("xoxb-test-token");
        loaded.RefreshToken.ShouldBe("xoxr-refresh");
        loaded.ExpiresUtc.ShouldNotBeNull();
        loaded.Scopes.ShouldBe("chat:write,channels:read");
        loaded.AccountLabel.ShouldBe("Test Workspace");
    }

    [Fact]
    public async Task Save_UpdatesExistingCredentials()
    {
        var credentials = new OAuthCredentials
        {
            Id = Guid.NewGuid(),
            Provider = "GitHub",
            AccessToken = "ghp-original",
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        await _repository.SaveAsync(credentials);

        credentials.AccessToken = "ghp-refreshed";
        credentials.DateModified = DateTime.UtcNow;
        await _repository.SaveAsync(credentials);

        var loaded = await _repository.GetAsync(credentials.Id);

        loaded.ShouldNotBeNull();
        loaded.AccessToken.ShouldBe("ghp-refreshed");
    }

    [Fact]
    public async Task Delete_RemovesCredentials()
    {
        var credentials = new OAuthCredentials
        {
            Id = Guid.NewGuid(),
            Provider = "Slack",
            AccessToken = "tok",
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        await _repository.SaveAsync(credentials);
        var deleted = await _repository.DeleteAsync(credentials.Id);

        deleted.ShouldBeTrue();
        (await _repository.GetAsync(credentials.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenNotFound()
    {
        var result = await _repository.DeleteAsync(Guid.NewGuid());
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNotFound()
    {
        var result = await _repository.GetAsync(Guid.NewGuid());
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Save_PreservesNullOptionalFields()
    {
        var credentials = new OAuthCredentials
        {
            Id = Guid.NewGuid(),
            Provider = "Dropbox",
            AccessToken = "tok",
            RefreshToken = null,
            ExpiresUtc = null,
            Scopes = null,
            AccountLabel = null,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        await _repository.SaveAsync(credentials);

        var loaded = await _repository.GetAsync(credentials.Id);

        loaded.ShouldNotBeNull();
        loaded.RefreshToken.ShouldBeNull();
        loaded.ExpiresUtc.ShouldBeNull();
        loaded.Scopes.ShouldBeNull();
        loaded.AccountLabel.ShouldBeNull();
    }

    public void Dispose() => _fixture.Dispose();
}
