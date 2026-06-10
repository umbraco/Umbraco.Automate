using Microsoft.Extensions.Logging;
using OpenIddict.Client;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class OAuthCredentialsServiceTests
{
    private readonly Mock<IOAuthCredentialsRepository> _repo = new();
    private readonly OAuthCredentialsService _service;

    public OAuthCredentialsServiceTests()
    {
        var clientService = new OpenIddictClientService(Mock.Of<IServiceProvider>());
        _service = new OAuthCredentialsService(
            _repo.Object,
            clientService,
            Mock.Of<ILogger<OAuthCredentialsService>>());
    }

    [Fact]
    public async Task GetCredentialsAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var credentials = new OAuthCredentials { Id = id, Provider = "Slack", AccessToken = "tok" };
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        var result = await _service.GetCredentialsAsync(id);

        result.ShouldNotBeNull();
        result.Provider.ShouldBe("Slack");
    }

    [Fact]
    public async Task CreateCredentialsAsync_AssignsIdWhenEmpty()
    {
        var credentials = new OAuthCredentials { Provider = "GitHub", AccessToken = "tok" };
        _repo.Setup(r => r.SaveAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) => c);

        var result = await _service.CreateCredentialsAsync(credentials);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCredentialsAsync_PreservesExistingId()
    {
        var id = Guid.NewGuid();
        var credentials = new OAuthCredentials { Id = id, Provider = "GitHub", AccessToken = "tok" };
        _repo.Setup(r => r.SaveAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) => c);

        var result = await _service.CreateCredentialsAsync(credentials);

        result.Id.ShouldBe(id);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsToken_WhenNotExpired()
    {
        var id = Guid.NewGuid();
        var credentials = new OAuthCredentials
        {
            Id = id,
            Provider = "Slack",
            AccessToken = "valid-token",
            ExpiresUtc = DateTime.UtcNow.AddHours(1),
        };
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        var token = await _service.GetValidAccessTokenAsync(id);

        token.ShouldBe("valid-token");
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsToken_WhenNoExpiry()
    {
        var id = Guid.NewGuid();
        var credentials = new OAuthCredentials
        {
            Id = id,
            Provider = "Slack",
            AccessToken = "no-expiry-token",
            ExpiresUtc = null,
        };
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        var token = await _service.GetValidAccessTokenAsync(id);

        token.ShouldBe("no-expiry-token");
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsNull_WhenExpiredWithNoRefreshToken()
    {
        var id = Guid.NewGuid();
        var credentials = new OAuthCredentials
        {
            Id = id,
            Provider = "Slack",
            AccessToken = "expired-token",
            ExpiresUtc = DateTime.UtcNow.AddMinutes(-10),
            RefreshToken = null,
        };
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        var token = await _service.GetValidAccessTokenAsync(id);

        token.ShouldBeNull();
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ReturnsNull_WhenCredentialsNotFound()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredentials?)null);

        var token = await _service.GetValidAccessTokenAsync(Guid.NewGuid());

        token.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteCredentialsAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.DeleteCredentialsAsync(id);

        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCredentialsAsync_SetsDateModified()
    {
        var credentials = new OAuthCredentials
        {
            Id = Guid.NewGuid(),
            Provider = "Slack",
            AccessToken = "updated-token",
            DateModified = DateTime.UtcNow.AddDays(-1),
        };
        _repo.Setup(r => r.SaveAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) => c);

        var before = DateTime.UtcNow;
        await _service.UpdateCredentialsAsync(credentials);

        credentials.DateModified.ShouldBeGreaterThanOrEqualTo(before);
    }
}
