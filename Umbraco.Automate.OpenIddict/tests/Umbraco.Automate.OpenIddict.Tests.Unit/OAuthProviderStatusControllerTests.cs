using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Controllers;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Providers;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

/// <summary>
/// Tests for the status endpoint added by #106, which lets the backoffice UI warn before a user
/// clicks "Authenticate with X" rather than discovering a missing client ID/secret via an
/// exception in the auth popup.
/// </summary>
public class OAuthProviderStatusControllerTests
{
    private readonly Mock<IOAuthProviderConfigurationSource> _configurationSource = new();

    [Fact]
    public void Status_IsConfigured_True_WhenClientIdAndSecretBothPresent()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var response = Status("Slack");

        response.IsConfigured.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "client-secret")]
    [InlineData("client-id", null)]
    [InlineData("", "client-secret")]
    [InlineData("client-id", "   ")]
    public void Status_IsConfigured_False_WhenClientIdOrSecretIsMissingOrBlank(string? clientId, string? clientSecret)
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = clientId, ClientSecret = clientSecret });

        var response = Status("Slack");

        response.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Status_IsConfigured_False_WhenProviderHasNoConfigurationSectionAtAll()
    {
        // No "Umbraco:Automate:Providers:Slack" section at all — distinct from an empty one.
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var response = Status("Slack");

        response.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void Status_SetupDocsUrl_ResolvedFromMatchingConnectionType()
    {
        const string docsUrl = "https://docs.umbraco.com/umbraco-automate/add-ons/slack/installation#create-a-slack-app";
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var response = Status("Slack", new TestOAuthConnectionType("Slack", docsUrl));

        response.SetupDocsUrl.ShouldBe(docsUrl);
    }

    [Fact]
    public void Status_SetupDocsUrl_MatchesProviderNameCaseInsensitively()
    {
        // The connection type's ProviderName casing (e.g. "Slack") may not match the casing the
        // frontend sends through the route, so the lookup must not be case-sensitive.
        const string docsUrl = "https://example.com/docs";
        _configurationSource.Setup(s => s.GetConfiguration("slack")).Returns((OAuthProviderConfiguration?)null);

        var response = Status("slack", new TestOAuthConnectionType("Slack", docsUrl));

        response.SetupDocsUrl.ShouldBe(docsUrl);
    }

    [Fact]
    public void Status_SetupDocsUrl_Null_WhenNoConnectionTypeMatchesProvider()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Google")).Returns((OAuthProviderConfiguration?)null);

        var response = Status("Google", new TestOAuthConnectionType("Slack", "https://example.com/docs"));

        response.SetupDocsUrl.ShouldBeNull();
    }

    [Fact]
    public void Status_SetupDocsUrl_Null_WhenMatchingConnectionTypeDeclaresNone()
    {
        // Mirrors an already-published provider that hasn't overridden SetupDocsUrl yet — the
        // base class defaults it to null, so this must not throw or misbehave.
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var response = Status("Slack", new TestOAuthConnectionType("Slack", setupDocsUrl: null));

        response.SetupDocsUrl.ShouldBeNull();
    }

    private OAuthProviderStatusResponseModel Status(string provider, params IConnectionType[] connectionTypes)
    {
        var controller = new OAuthProviderStatusController(
            _configurationSource.Object,
            new ConnectionTypeCollection(() => connectionTypes));

        var result = controller.Status(provider);

        return result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<OAuthProviderStatusResponseModel>();
    }

    private sealed class TestSettings
    {
        public Guid? OAuthCredentialsId { get; set; }
    }

    [ConnectionType("test-oauth", "Test OAuth")]
    private sealed class TestOAuthConnectionType : OAuthConnectionTypeBase<TestSettings>
    {
        public TestOAuthConnectionType(string providerName, string? setupDocsUrl)
            : base(new ConnectionTypeInfrastructure(Mock.Of<IEditableModelResolver>()), Mock.Of<IOAuthCredentialsService>())
        {
            ProviderName = providerName;
            SetupDocsUrl = setupDocsUrl;
        }

        public override string ProviderName { get; }

        public override string? SetupDocsUrl { get; }
    }
}
