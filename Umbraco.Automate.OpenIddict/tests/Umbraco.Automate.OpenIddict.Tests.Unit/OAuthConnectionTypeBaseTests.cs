using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class OAuthConnectionTypeBaseTests
{
    private readonly Mock<IOAuthCredentialsService> _credentialsService = new();
    private readonly TestOAuthConnectionType _connectionType;

    public OAuthConnectionTypeBaseTests()
    {
        _connectionType = new TestOAuthConnectionType(_credentialsService.Object);
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenSettingsNull()
    {
        var result = await _connectionType.ValidateAsync(settings: null, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenSettingsLackCredentialsIdProperty()
    {
        // Settings POCOs without the OAuthCredentialsId convention property — not
        // expected in practice, but the base should fail loudly rather than quietly succeed.
        var badSettings = new { Unrelated = "value" };

        var result = await _connectionType.ValidateAsync(badSettings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Message.ShouldNotBeNull().ShouldContain("OAuthCredentialsId");
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenCredentialsIdIsEmpty()
    {
        var settings = new TestSettings { OAuthCredentialsId = Guid.Empty };

        var result = await _connectionType.ValidateAsync(settings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Message.ShouldNotBeNull().ShouldContain("authenticated");
        _credentialsService.Verify(
            s => s.GetValidAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenCredentialsIdIsNull()
    {
        var settings = new TestSettings { OAuthCredentialsId = null };

        var result = await _connectionType.ValidateAsync(settings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        _credentialsService.Verify(
            s => s.GetValidAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenTokenCannotBeResolved()
    {
        // Null return from GetValidAccessTokenAsync means expired + no refresh — re-auth needed.
        var id = Guid.NewGuid();
        _credentialsService.Setup(s => s.GetValidAccessTokenAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var settings = new TestSettings { OAuthCredentialsId = id };

        var result = await _connectionType.ValidateAsync(settings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Message.ShouldNotBeNull().ShouldContain("expired");
    }

    [Fact]
    public async Task ValidateAsync_Failure_WhenCredentialsServiceThrows()
    {
        var id = Guid.NewGuid();
        _credentialsService.Setup(s => s.GetValidAccessTokenAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kaboom"));
        var settings = new TestSettings { OAuthCredentialsId = id };

        var result = await _connectionType.ValidateAsync(settings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Details.ShouldContain("kaboom");
    }

    [Fact]
    public async Task ValidateAsync_Success_WhenTokenResolves()
    {
        var id = Guid.NewGuid();
        _credentialsService.Setup(s => s.GetValidAccessTokenAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("access-token");
        var settings = new TestSettings { OAuthCredentialsId = id };

        var result = await _connectionType.ValidateAsync(settings, CancellationToken.None);

        result.Status.ShouldBe(ConnectionValidationStatus.Success);
    }

    [Fact]
    public async Task ValidateAsync_RethrowsCancellation()
    {
        var id = Guid.NewGuid();
        _credentialsService.Setup(s => s.GetValidAccessTokenAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var settings = new TestSettings { OAuthCredentialsId = id };

        await Should.ThrowAsync<OperationCanceledException>(
            () => _connectionType.ValidateAsync(settings, CancellationToken.None));
    }

    private sealed class TestSettings
    {
        public Guid? OAuthCredentialsId { get; set; }
    }

    [ConnectionType("test-oauth", "Test OAuth")]
    private sealed class TestOAuthConnectionType : OAuthConnectionTypeBase<TestSettings>
    {
        public TestOAuthConnectionType(IOAuthCredentialsService credentialsService)
            : base(new ConnectionTypeInfrastructure(Mock.Of<IEditableModelResolver>()), credentialsService)
        {
        }

        public override string ProviderName => "TestProvider";
    }
}
