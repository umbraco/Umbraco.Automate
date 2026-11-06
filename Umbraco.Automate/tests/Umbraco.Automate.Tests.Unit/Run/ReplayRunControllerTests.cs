using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Run.Controllers;

namespace Umbraco.Automate.Tests.Unit.Run;

public class ReplayRunControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<ICircuitBreakerService> _circuitBreaker = new();

    private readonly AutomationRun _run;

    public ReplayRunControllerTests()
    {
        var automation = new AutomationBuilder().WithStatus(AutomationStatus.Published).WithWebhookTrigger().Build();
        _run = new AutomationRunBuilder().WithAutomationId(automation.Id).AsFailed().Build();

        _automationService
            .Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _runService
            .Setup(s => s.GetRunAsync(_run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_run);
        _authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        _circuitBreaker
            .Setup(s => s.IsRunAllowedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task ReplayRun_EngineNotRunning_Returns409()
    {
        _executor
            .Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>?>()))
            .ThrowsAsync(new AutomationEngineNotRunningException());

        var result = await Controller().ReplayRun(_run.Id);

        result.ShouldBeOfType<ConflictObjectResult>();
    }

    private ReplayRunController Controller()
        => new(_runService.Object, _automationService.Object, _authorizationService.Object, _executor.Object, _circuitBreaker.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
