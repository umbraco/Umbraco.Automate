using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpAuthenticationMiddlewareTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IEntityVersionService> _versionService = new();
    private readonly TriggerCollection _triggers;
    private readonly Guid _automationId = Guid.NewGuid();
    private bool _nextCalled;

    public McpAuthenticationMiddlewareTests()
    {
        // A real resolver (not a bare Mock.Of<IEditableModelResolver>()) is required here: an
        // unconfigured mock returns null from ResolveModel<T>, which would make the middleware
        // refuse every request as having no tool name, masking the very behavior these tests
        // exercise. This matches the resolver setup used by WebhookEndpointControllerTests.
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        _triggers = new TriggerCollection(() => [new McpTrigger(new TriggerInfrastructure(modelResolver))]);
    }

    private const string Secret = "correct-secret";
    private const string ValidAuthorization = "Bearer " + Secret;

    private static Dictionary<string, object?> McpSettings(string? secret = Secret, string toolName = "DoThing")
    {
        var settings = new Dictionary<string, object?>
        {
            ["toolName"] = toolName,
            ["toolDescription"] = "Does the thing.",
        };
        if (secret is not null)
        {
            settings["secret"] = secret;
        }

        return settings;
    }

    /// <summary>
    /// Sets up a published automation whose current row and published snapshot are the same.
    /// </summary>
    private Automation SetupPublished(Dictionary<string, object?> settings, string triggerAlias = McpTrigger.WellKnownAlias)
    {
        Automation automation = new AutomationBuilder()
            .WithId(_automationId)
            .WithTrigger(triggerAlias, settings);
        SetupCurrent(automation);
        SetupSnapshot(automation);
        return automation;
    }

    private void SetupCurrent(Automation? automation)
        => _automationService.Setup(s => s.GetAutomationAsync(_automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

    private void SetupSnapshot(Automation? snapshot)
        => _versionService.Setup(s => s.GetVersionSnapshotAsync<Automation>(_automationId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

    private async Task<HttpContext> InvokeAsync(string? authorization = null, bool withRouteValue = true)
    {
        var middleware = new McpAuthenticationMiddleware(
            _ =>
            {
                _nextCalled = true;
                return Task.CompletedTask;
            },
            NullLogger<McpAuthenticationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        if (withRouteValue)
        {
            context.Request.RouteValues = new RouteValueDictionary { ["automationId"] = _automationId.ToString() };
        }

        if (authorization is not null)
        {
            context.Request.Headers.Authorization = authorization;
        }

        await middleware.InvokeAsync(context, _automationService.Object, _versionService.Object, _triggers);
        return context;
    }

    private void ShouldBeRefused(HttpContext context)
    {
        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_NoAutomationIdRouteValue_Returns401()
        => ShouldBeRefused(await InvokeAsync(withRouteValue: false));

    [Fact]
    public async Task InvokeAsync_AutomationNotFound_Returns401()
    {
        SetupCurrent(null);

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_AutomationNotPublished_Returns401()
    {
        SetupCurrent(new AutomationBuilder().WithId(_automationId).AsDraft().WithTrigger(McpTrigger.WellKnownAlias, McpSettings()));

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_PublishedSnapshotMissing_Returns401()
    {
        SetupCurrent(new AutomationBuilder().WithId(_automationId).WithTrigger(McpTrigger.WellKnownAlias, McpSettings()));
        SetupSnapshot(null);

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_NotAnMcpTrigger_Returns401()
    {
        SetupPublished(McpSettings(), triggerAlias: "umbracoAutomate.manual");

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_InvalidToolName_Returns401RatherThanThrowing()
    {
        SetupPublished(McpSettings(toolName: "Search products"));

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_NoTriggerSettings_Returns401()
    {
        SetupPublished([]);

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_NoSecretConfigured_Returns401RatherThanServingUnauthenticated()
    {
        SetupPublished(McpSettings(secret: null));

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_CorrectBearerToken_CallsNextWithThePublishedAutomation()
    {
        var published = SetupPublished(McpSettings());

        var context = await InvokeAsync(ValidAuthorization);

        _nextCalled.ShouldBeTrue();
        context.Items[McpHttpContextItems.AutomationKey].ShouldBeSameAs(published);
        context.Items[McpHttpContextItems.SettingsKey].ShouldBeOfType<McpTriggerSettings>().ToolName.ShouldBe("DoThing");
    }

    [Fact]
    public async Task InvokeAsync_MissingAuthorizationHeader_Returns401()
    {
        SetupPublished(McpSettings());

        ShouldBeRefused(await InvokeAsync());
    }

    [Fact]
    public async Task InvokeAsync_NonBearerScheme_Returns401()
    {
        SetupPublished(McpSettings());

        ShouldBeRefused(await InvokeAsync("Basic xyz"));
    }

    [Fact]
    public async Task InvokeAsync_WrongBearerToken_Returns401()
    {
        SetupPublished(McpSettings());

        ShouldBeRefused(await InvokeAsync("Bearer wrong-secret"));
    }

    [Fact]
    public async Task InvokeAsync_DraftChangedTheSecret_OnlyThePublishedSecretWorks()
    {
        SetupCurrent(new AutomationBuilder().WithId(_automationId).WithTrigger(McpTrigger.WellKnownAlias, McpSettings(secret: "draft-secret")));
        SetupSnapshot(new AutomationBuilder().WithId(_automationId).WithTrigger(McpTrigger.WellKnownAlias, McpSettings(secret: "published-secret")));

        ShouldBeRefused(await InvokeAsync("Bearer draft-secret"));

        await InvokeAsync("Bearer published-secret");
        _nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_DraftDiffersFromPublished_HandsOnThePublishedVersion()
    {
        SetupCurrent(new AutomationBuilder().WithId(_automationId).WithTrigger(McpTrigger.WellKnownAlias, McpSettings(toolName: "DraftName")));
        Automation published = new AutomationBuilder().WithId(_automationId).WithTrigger(McpTrigger.WellKnownAlias, McpSettings(toolName: "PublishedName"));
        SetupSnapshot(published);

        var context = await InvokeAsync(ValidAuthorization);

        _nextCalled.ShouldBeTrue();
        context.Items[McpHttpContextItems.AutomationKey].ShouldBeSameAs(published);
        context.Items[McpHttpContextItems.SettingsKey].ShouldBeOfType<McpTriggerSettings>().ToolName.ShouldBe("PublishedName");
    }
}
