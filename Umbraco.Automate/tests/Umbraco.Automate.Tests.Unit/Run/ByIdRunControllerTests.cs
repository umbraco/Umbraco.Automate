using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Run.Controllers;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Tests.Unit.Run;

/// <summary>
/// Covers the trigger alias on a single run, which comes from the automation version that ran
/// rather than the automation as it is now.
/// </summary>
public class ByIdRunControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IUmbracoMapper> _mapper = new();

    private readonly Automation _automation;
    private readonly AutomationRun _run;

    public ByIdRunControllerTests()
    {
        _automation = new AutomationBuilder().WithTrigger("current.trigger").WithVersion(3).Build();
        _run = new AutomationRunBuilder().WithAutomationId(_automation.Id).WithAutomationVersion(2).Build();

        _automationService
            .Setup(s => s.GetAutomationAsync(_automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_automation);
        _runService
            .Setup(s => s.GetRunAsync(_run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_run);
        _authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        _mapper
            .Setup(m => m.Map<AutomationRunResponseModel>(_run))
            .Returns(() => new AutomationRunResponseModel { Id = _run.Id, AutomationId = _automation.Id });
    }

    [Fact]
    public async Task GetRunById_UsesTheTriggerFromTheVersionThatRan()
    {
        var snapshot = new AutomationBuilder().WithId(_automation.Id).WithTrigger("original.trigger").WithVersion(2).Build();
        _automationService
            .Setup(s => s.GetAutomationVersionSnapshotAsync(_automation.Id, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var result = await Controller().GetRunById(_run.Id);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<AutomationRunResponseModel>();
        model.TriggerAlias.ShouldBe("original.trigger");
    }

    [Fact]
    public async Task GetRunById_NoVersionSnapshot_FallsBackToTheCurrentTrigger()
    {
        var result = await Controller().GetRunById(_run.Id);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<AutomationRunResponseModel>();
        model.TriggerAlias.ShouldBe("current.trigger");
    }

    private ByIdRunController Controller()
        => new(_automationService.Object, _runService.Object, _authorizationService.Object, _mapper.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
