using System.Security.Cryptography;
using System.Text;
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
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;
using Umbraco.Automate.Testing.Builders;
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
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new WebhookTrigger(deps) };
        });

        var authenticators = new WebhookAuthenticatorCollection(() =>
            new IWebhookAuthenticator[]
            {
                new PlainSecretWebhookAuthenticator(),
                new HmacSha256WebhookAuthenticator(),
            });

        _controller = new WebhookEndpointController(
            _automationService.Object,
            _dispatcher.Object,
            triggers,
            authenticators,
            modelResolver,
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
    public async Task ReceiveWebhook_AutomationUnpublished_Returns409()
    {
        var automation = CreateAutomation(AutomationStatus.Unpublished);
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
    public async Task ReceiveWebhook_EmptySecretSettings_Returns401()
    {
        // Default strategy with no configured secret fails authentication rather than letting
        // unauthenticated requests through silently.
        var automation = CreateAutomation();
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_ValidSecretInHeader_Returns202()
    {
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "my-secret-token" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "my-secret-token";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_ValidSecretInQuery_Returns202()
    {
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "my-secret-token" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?secret=my-secret-token");

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidSecret_Returns401()
    {
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "correct-secret" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "wrong-secret";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingSecretHeader_Returns401()
    {
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "required-secret" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // No header, no query param — secret is missing from the request.
        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_CapturesQueryParameters()
    {
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "tok" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";
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

    [Fact]
    public async Task ReceiveWebhook_TargetsTheAddressedAutomation()
    {
        // The endpoint is addressed by automation ID, so the dispatched event must target that
        // automation — otherwise the handler fans out to every published automation sharing the
        // webhook alias and runs them all.
        var automation = CreateAutomation(
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "tok" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";

        TriggerEvent? captured = null;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.TargetAutomationId.ShouldBe(automation.Id);
    }

    [Fact]
    public async Task ReceiveWebhook_ValidHmacSignature_Returns202()
    {
        var key = "hmac-secret-key";
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = key });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var body = """{"event":"test"}""";
        var signature = ComputeHmacSha256(body, key);

        SetRequestBody(body);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Signature"] = $"sha256={signature}";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_InvalidHmacSignature_Returns401()
    {
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = "hmac-secret-key" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var body = """{"event":"test"}""";
        SetRequestBody(body);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Signature"] = "sha256=badhex000000";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveWebhook_MissingSignatureHeaderWhenRequired_Returns401()
    {
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = "hmac-secret-key" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var body = """{"event":"test"}""";
        SetRequestBody(body);
        // No X-Webhook-Signature header

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_TamperedBody_Returns401()
    {
        var key = "hmac-secret-key";
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = key });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // Sign the original body but send a different one.
        var signature = ComputeHmacSha256("""{"event":"original"}""", key);
        SetRequestBody("""{"event":"tampered"}""");
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Signature"] = $"sha256={signature}";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_SignatureMode_IgnoresPlainSecretHeader()
    {
        var key = "hmac-secret-key";
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = key });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // Provide the plain secret header but not the HMAC signature — should still fail.
        SetRequestBody("""{"event":"test"}""");
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = key;

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_GetMethodWithQuerySecret_Returns202()
    {
        // GET must be routable so that webhooks configured with GET in AllowedMethods
        // don't 404 at the routing layer before the allow-list check runs.
        var automation = new AutomationBuilder()
            .WithStatus(AutomationStatus.Published)
            .WithWebhookTrigger(
                PlainSecretWebhookAuthenticator.WellKnownAlias,
                new PlainSecretWebhookAuthenticatorSettings { Secret = "ping" })
            .Build();
        automation.Trigger!.Settings["allowedMethod"] = "GET";

        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Method = "GET";
        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?secret=ping");

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_UnknownAuthenticatorAlias_FallsBackToPlainSecret()
    {
        // Unknown/stale alias shouldn't error — it should behave like plain-secret (the default).
        var automation = CreateAutomation(
            authenticatorAlias: "not-registered-provider",
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = "my-secret-token" });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "my-secret-token";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    private void SetRequestBody(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        _controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);
        _controller.ControllerContext.HttpContext.Request.ContentLength = bytes.Length;
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static Automation CreateAutomation(
        AutomationStatus status = AutomationStatus.Published,
        string triggerAlias = "umbracoAutomate.webhook",
        string? authenticatorAlias = null,
        object? authenticatorSettings = null)
    {
        var builder = new AutomationBuilder()
            .WithStatus(status);

        if (triggerAlias == "umbracoAutomate.webhook")
        {
            builder.WithWebhookTrigger(authenticatorAlias, authenticatorSettings);
        }
        else
        {
            builder.WithTrigger(triggerAlias);
        }

        return builder.Build();
    }
}
