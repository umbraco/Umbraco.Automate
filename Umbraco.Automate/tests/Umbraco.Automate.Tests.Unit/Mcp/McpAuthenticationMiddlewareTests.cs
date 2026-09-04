using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpAuthenticationMiddlewareTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly TriggerCollection _triggers;
    private bool _nextCalled;

    public McpAuthenticationMiddlewareTests()
    {
        // A real resolver (not a bare Mock.Of<IEditableModelResolver>()) is required here: an
        // unconfigured mock returns null from ResolveModel<T>, which would make the middleware's
        // secret comparison always skip, masking the very behavior these tests exercise. This
        // matches the resolver setup used by WebhookEndpointControllerTests for the same reason.
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        _triggers = new TriggerCollection(() => [new McpTrigger(new TriggerInfrastructure(modelResolver))]);
    }

    private (McpAuthenticationMiddleware Middleware, DefaultHttpContext Context) Build(Guid automationId)
    {
        var middleware = new McpAuthenticationMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary { ["automationId"] = automationId.ToString() };

        return (middleware, context);
    }

    [Fact]
    public async Task InvokeAsync_AutomationNotFound_Returns404AndDoesNotCallNext()
    {
        var automationId = Guid.NewGuid();
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);
        var (middleware, context) = Build(automationId);

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_NoSecretConfigured_CallsNext()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Do Thing",
                ["toolDescription"] = "Does the thing.",
            });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        _nextCalled.ShouldBeTrue();
        context.Items[McpHttpContextItems.AutomationKey].ShouldNotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_SecretConfigured_WrongBearerToken_Returns401()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Do Thing",
                ["toolDescription"] = "Does the thing.",
                ["secret"] = "correct-secret",
            });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);
        context.Request.Headers.Authorization = "Bearer wrong-secret";

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SecretConfigured_CorrectBearerToken_CallsNext()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Do Thing",
                ["toolDescription"] = "Does the thing.",
                ["secret"] = "correct-secret",
            });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);
        context.Request.Headers.Authorization = "Bearer correct-secret";

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        _nextCalled.ShouldBeTrue();
    }
}
