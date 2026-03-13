using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Tests.Common.Builders;
using Umbraco.Automate.Web.Api.Webhook.Controllers;

namespace Umbraco.Automate.Tests.Unit.Webhook;

public class WebhookEndpointControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<ITriggerDispatcher> _dispatcher = new();
    private readonly WebhookEndpointController _controller;

    public WebhookEndpointControllerTests()
    {
        var configuration = new ConfigurationBuilder().Build();
        var modelResolver = new EditableModelResolver(configuration);
        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver, Options.Create(new DeduplicationOptions()));
            return new ITrigger[] { new WebhookTrigger(deps) };
        });

        _controller = new WebhookEndpointController(
            _automationService.Object,
            _dispatcher.Object,
            triggers,
            Options.Create(new WebhookOptions()),
            Mock.Of<ILogger<WebhookEndpointController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        _controller.ControllerContext.HttpContext.Request.Method = "POST";
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationNotFound_Returns404()
    {
        _automationService.Setup(s => s.GetAutomationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _controller.ReceiveWebhook(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationNotPublished_Returns409()
    {
        var automation = CreateAutomation(AutomationStatus.Draft);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationNotEnabled_Returns409()
    {
        var automation = CreateAutomation(AutomationStatus.Published, isEnabled: false);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_TriggerNotWebhook_Returns404()
    {
        var automation = CreateAutomation(triggerAlias: "umbracoAutomate.manual");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_MethodNotAllowed_Returns405()
    {
        var automation = CreateAutomation();
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Method = "DELETE";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(405);
    }

    [Fact]
    public async Task ReceiveWebhook_NoSecret_DispatchesAndReturns202()
    {
        var automation = CreateAutomation();
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<TriggerEvent<WebhookTriggerOutput>>(e =>
                e.TriggerAlias == "umbracoAutomate.webhook" &&
                e.InitiatorType == "webhook"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiveWebhook_ValidSecretInHeader_Returns202()
    {
        var automation = CreateAutomation(secret: "my-secret-token");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "my-secret-token";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_ValidSecretInQuery_Returns202()
    {
        var automation = CreateAutomation(secret: "my-secret-token");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?secret=my-secret-token");

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidSecret_Returns401()
    {
        var automation = CreateAutomation(secret: "correct-secret");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "wrong-secret";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingSecretWhenRequired_Returns401()
    {
        var automation = CreateAutomation(secret: "required-secret");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // No header, no query param — secret is missing.
        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_CapturesQueryParameters()
    {
        var automation = CreateAutomation();
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?foo=bar&baz=123");

        TriggerEvent<WebhookTriggerOutput>? captured = null;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerEvent, CancellationToken>((e, _) => captured = e as TriggerEvent<WebhookTriggerOutput>)
            .Returns(Task.CompletedTask);

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Output.Query["foo"].ShouldBe("bar");
        captured.Output.Query["baz"].ShouldBe("123");
        captured.Output.Method.ShouldBe("POST");
    }

    private static Automation CreateAutomation(
        AutomationStatus status = AutomationStatus.Published,
        bool isEnabled = true,
        string triggerAlias = "umbracoAutomate.webhook",
        string? secret = null)
    {
        var builder = new AutomationBuilder()
            .WithStatus(status)
            .WithIsEnabled(isEnabled);

        if (triggerAlias == "umbracoAutomate.webhook")
        {
            builder.WithWebhookTrigger(secret);
        }
        else
        {
            builder.WithTrigger(triggerAlias);
        }

        return builder.Build();
    }
}
