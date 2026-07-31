using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Automation.Controllers;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class TriggerAutomationControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<ICircuitBreakerService> _circuitBreaker = new();
    private readonly TriggerAutomationController _controller;

    public TriggerAutomationControllerTests()
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        _circuitBreaker
            .Setup(s => s.IsRunAllowedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _controller = new TriggerAutomationController(
            _automationService.Object,
            authorizationService.Object,
            _executor.Object,
            _circuitBreaker.Object,
            Mock.Of<IBackOfficeSecurityAccessor>());
    }

    [Fact]
    public async Task TriggerAutomation_WithoutRequestBody_PassesNoTriggerOutputData()
    {
        var automation = GivenPublishedAutomation();

        var result = await _controller.TriggerAutomation(automation.Id, request: null);

        result.ShouldBeOfType<AcceptedResult>();
        _executor.Verify(
            e => e.ExecuteAsync(
                automation,
                TriggerInitiatorType.User,
                It.IsAny<string?>(),
                null,
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAutomation_WithTriggerOutputData_PassesUnwrappedDataToExecutor()
    {
        var automation = GivenPublishedAutomation();
        var request = RequestWithOutputData("""
            {
                "method": "POST",
                "body": "{\"title\":\"Hello\"}",
                "headers": { "Content-Type": "application/json" }
            }
            """);

        Dictionary<string, object?>? captured = null;
        _executor
            .Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>?>()))
            .Callback((
                Automation _,
                string _,
                string? _,
                Dictionary<string, object?>? data,
                CancellationToken _,
                IReadOnlyList<Guid>? _) => captured = data)
            .ReturnsAsync(Guid.NewGuid());

        var result = await _controller.TriggerAutomation(automation.Id, request);

        result.ShouldBeOfType<AcceptedResult>();
        captured.ShouldNotBeNull();
        captured["method"].ShouldBe("POST");
        captured["body"].ShouldBe("""{"title":"Hello"}""");

        // Nested objects must arrive as dictionaries, not JsonElement, so BindingEvaluator
        // can traverse paths like trigger.headers.Content-Type and the values survive the
        // Newtonsoft round-trip in the WorkflowCore persistence layer.
        var headers = captured["headers"].ShouldBeAssignableTo<Dictionary<string, object?>>();
        headers!["Content-Type"].ShouldBe("application/json");
    }

    [Fact]
    public async Task TriggerAutomation_WithEmptyTriggerOutputData_PassesNoTriggerOutputData()
    {
        var automation = GivenPublishedAutomation();

        var result = await _controller.TriggerAutomation(automation.Id, RequestWithOutputData("{}"));

        result.ShouldBeOfType<AcceptedResult>();
        _executor.Verify(
            e => e.ExecuteAsync(
                automation,
                TriggerInitiatorType.User,
                It.IsAny<string?>(),
                null,
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAutomation_NotPublished_Returns409AndDoesNotExecute()
    {
        var automation = new AutomationBuilder()
            .WithStatus(AutomationStatus.Draft)
            .WithWebhookTrigger()
            .Build();
        _automationService
            .Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _controller.TriggerAutomation(automation.Id, RequestWithOutputData("""{"body":"x"}"""));

        result.ShouldBeOfType<ConflictObjectResult>();
        _executor.VerifyNoOtherCalls();
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

    private static TriggerAutomationRequestModel RequestWithOutputData(string json)
        => new()
        {
            TriggerOutputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json),
        };
}
