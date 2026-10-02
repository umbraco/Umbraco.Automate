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
using Umbraco.Automate.Web.Api.Webhook;
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
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(configuration));
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
    public async Task ReceiveWebhook_AutomationNotFound_Returns401()
    {
        // An unknown automation answers like a bad credential, so a caller can't probe for IDs.
        _automationService.Setup(s => s.GetAutomationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _controller.ReceiveWebhook(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationNotPublished_Returns409()
    {
        var automation = CreateAutomationWithSecret("tok", AutomationStatus.Draft);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationUnpublished_Returns409()
    {
        var automation = CreateAutomationWithSecret("tok", AutomationStatus.Unpublished);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_AutomationUnpublished_WithoutSecret_Returns401()
    {
        // The publish state is only reported to an authenticated caller.
        var automation = CreateAutomationWithSecret("tok", AutomationStatus.Unpublished);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_TriggerNotWebhook_Returns401()
    {
        var automation = CreateAutomation(triggerAlias: "umbracoAutomate.manual");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_MethodNotAllowed_Returns405()
    {
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Method = "DELETE";
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(405);
    }

    [Fact]
    public async Task ReceiveWebhook_MethodNotAllowed_WithoutSecret_Returns401()
    {
        // The allowed method is only reported to an authenticated caller.
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Method = "DELETE";

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReceiveWebhook_PayloadTooLarge_Returns413BeforeLookingUpTheAutomation()
    {
        // Size is checked first, so an oversized request gets the same answer for every automation.
        _controller.ControllerContext.HttpContext.Request.ContentLength = new WebhookOptions().MaxPayloadBytes + 1;

        var result = await _controller.ReceiveWebhook(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(413);
        _automationService.Verify(s => s.GetAutomationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveWebhook_ChunkedBody_IsRead()
    {
        // A chunked request has no Content-Length, but still has a body.
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";
        SetRequestBody("""{"chunked":true}""");
        _controller.ControllerContext.HttpContext.Request.ContentLength = null;

        var captured = CaptureDispatchedOutput();

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured().ShouldNotBeNull();
        captured()!.Body.ShouldBe("""{"chunked":true}""");
    }

    [Fact]
    public async Task ReceiveWebhook_ChunkedBodyOverTheLimit_Returns413()
    {
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";
        SetRequestBody(new string('b', (int)new WebhookOptions().MaxPayloadBytes + 1));
        _controller.ControllerContext.HttpContext.Request.ContentLength = null;

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(413);
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReceiveWebhook_RewindsABodyAlreadyReadByRouting()
    {
        // Umbraco's routing parses a form-encoded body before the action runs. The body is
        // buffered for this route, so the action rewinds it rather than reading an empty stream.
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";
        SetRequestBody("a=1&b=two");
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/x-www-form-urlencoded";
        _controller.ControllerContext.HttpContext.Request.Body.Seek(0, SeekOrigin.End);

        var captured = CaptureDispatchedOutput();

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured().ShouldNotBeNull();
        captured()!.Body.ShouldBe("a=1&b=two");
    }

    [Fact]
    public async Task ReceiveWebhook_LeavesSecretHeaderOutOfTheOutput()
    {
        // The output is stored with the run and readable by steps, so the credential must not be in it.
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["x-webhook-secret"] = "tok";
        _controller.ControllerContext.HttpContext.Request.Headers["X-Request-Id"] = "abc";

        var captured = CaptureDispatchedOutput();

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured().ShouldNotBeNull();
        captured()!.Headers.Keys.ShouldNotContain(k => k.Equals("X-Webhook-Secret", StringComparison.OrdinalIgnoreCase));
        captured()!.Headers["X-Request-Id"].ShouldBe("abc");
    }

    [Fact]
    public async Task ReceiveWebhook_LeavesSecretQueryParameterOutOfTheOutput()
    {
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.QueryString = new QueryString("?secret=tok&foo=bar");

        var captured = CaptureDispatchedOutput();

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured().ShouldNotBeNull();
        captured()!.Query.Keys.ShouldNotContain("secret");
        captured()!.Query["foo"].ShouldBe("bar");
    }

    [Fact]
    public async Task ReceiveWebhook_LeavesHmacSignatureHeaderOutOfTheOutput()
    {
        // The signature covers only the body, so a stored signature and body could be replayed.
        var key = "hmac-secret-key";
        var automation = CreateAutomation(
            authenticatorAlias: HmacSha256WebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new HmacSha256WebhookAuthenticatorSettings { SigningKey = key });
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var body = """{"event":"test"}""";
        SetRequestBody(body);
        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Signature"] = $"sha256={ComputeHmacSha256(body, key)}";

        var captured = CaptureDispatchedOutput();

        await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        captured().ShouldNotBeNull();
        captured()!.Headers.Keys.ShouldNotContain("X-Webhook-Signature");
        captured()!.Body.ShouldBe(body);
    }

    [Fact]
    public async Task ReceiveWebhook_ServerBodySizeLimitTripped_Returns413()
    {
        // The limit set before routing makes the server throw on a read past it.
        var automation = CreateAutomationWithSecret("tok");
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Webhook-Secret"] = "tok";
        _controller.ControllerContext.HttpContext.Request.Body = new ThrowingStream(
            new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        var result = await _controller.ReceiveWebhook(automation.Id, CancellationToken.None);

        result.ShouldBeOfType<ObjectResult>().StatusCode.ShouldBe(413);
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ReceiveWebhook_DisablesFormValueModelBinding()
    {
        // Without this, MVC reads a form-encoded body into its form value providers before the
        // action runs, and the action then reads an empty stream.
        var action = typeof(WebhookEndpointController).GetMethod(nameof(WebhookEndpointController.ReceiveWebhook))!;

        action.GetCustomAttributes(typeof(DisableFormValueModelBindingAttribute), inherit: false).ShouldNotBeEmpty();
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

    private Func<WebhookTriggerOutput?> CaptureDispatchedOutput()
    {
        WebhookTriggerOutput? captured = null;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<TriggerEvent>(), It.IsAny<CancellationToken>()))
            .Callback<TriggerEvent, CancellationToken>((e, _) => captured = (e as TriggerEvent<WebhookTriggerOutput>)?.Output)
            .Returns(Task.CompletedTask);

        return () => captured;
    }

    private static Automation CreateAutomationWithSecret(string secret, AutomationStatus status = AutomationStatus.Published)
        => CreateAutomation(
            status,
            authenticatorAlias: PlainSecretWebhookAuthenticator.WellKnownAlias,
            authenticatorSettings: new PlainSecretWebhookAuthenticatorSettings { Secret = secret });

    private void SetRequestBody(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        _controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);
        _controller.ControllerContext.HttpContext.Request.ContentLength = bytes.Length;
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";
    }

    private sealed class ThrowingStream(Exception exception) : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw exception;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw exception;
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
