using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Automation.Controllers;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class WebhookUrlAutomationControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IHostingEnvironment> _hostingEnvironment = new();
    private readonly Mock<IOptionsMonitor<WebRoutingSettings>> _webRoutingSettings = new();
    private WebRoutingSettings _settings = new();

    public WebhookUrlAutomationControllerTests()
    {
        _hostingEnvironment
            .Setup(h => h.ToAbsolute(It.IsAny<string>()))
            .Returns<string>(path => path);
        _webRoutingSettings.Setup(s => s.CurrentValue).Returns(() => _settings);
    }

    [Fact]
    public async Task GetWebhookUrl_AutomationNotFound_Returns404()
    {
        var controller = GivenController();

        var result = await controller.GetWebhookUrl(Guid.NewGuid());

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetWebhookUrl_WithoutConfiguredAppUrl_BuildsFromTheCurrentRequest()
    {
        var automation = GivenPublishedAutomation();
        var controller = GivenController(requestScheme: "https", requestHost: "example.com");

        var result = await controller.GetWebhookUrl(automation.Id);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var model = ok.Value.ShouldBeOfType<WebhookUrlResponseModel>();
        model.Url.ShouldBe($"https://example.com/automate/webhook/{automation.Id}");
    }

    [Fact]
    public async Task GetWebhookUrl_WithConfiguredAppUrl_PrefersItOverTheCurrentRequest()
    {
        _settings = new WebRoutingSettings { UmbracoApplicationUrl = "https://public.example.com" };
        var automation = GivenPublishedAutomation();

        // A load-balanced node's own request host must not leak into the URL when an admin has
        // pinned the public one via config.
        var controller = GivenController(requestScheme: "http", requestHost: "internal-node-7:8080");

        var result = await controller.GetWebhookUrl(automation.Id);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var model = ok.Value.ShouldBeOfType<WebhookUrlResponseModel>();
        model.Url.ShouldBe($"https://public.example.com/automate/webhook/{automation.Id}");
    }

    private WebhookUrlAutomationController GivenController(string requestScheme = "https", string requestHost = "127.0.0.1")
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var controller = new WebhookUrlAutomationController(
            _automationService.Object,
            authorizationService.Object,
            _hostingEnvironment.Object,
            _webRoutingSettings.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = requestScheme;
        httpContext.Request.Host = new HostString(requestHost);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    private Automation GivenPublishedAutomation()
    {
        var automation = new AutomationBuilder()
            .WithStatus(AutomationStatus.Published)
            .WithWebhookTrigger()
            .Build();

        _automationService
            .Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        return automation;
    }
}
