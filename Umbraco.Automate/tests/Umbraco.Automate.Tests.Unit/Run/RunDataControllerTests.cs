using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Run.Controllers;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Automate.Web.Authorization;

namespace Umbraco.Automate.Tests.Unit.Run;

/// <summary>
/// Covers the on-demand run data endpoints: step run input/output and run trigger data. Masking
/// and truncation belong to <see cref="IAutomationRunService"/> and are covered in
/// <c>AutomationRunServiceTests</c>; these tests cover authorization, 404s and response mapping.
/// </summary>
public class RunDataControllerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Automation _automation;

    public RunDataControllerTests()
    {
        _automation = new AutomationBuilder().WithWorkspaceId(_workspaceId).Build();
        _automationService
            .Setup(s => s.GetAutomationAsync(_automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_automation);
        GivenWorkspaceMember(true);
    }

    [Fact]
    public async Task GetStepRunData_ReturnsTheServiceDisplayValues()
    {
        // The service returns the payloads already masked and truncated; the controller passes
        // them through unchanged.
        var data = GivenStepRunData(input: """{ "token": "***" }""", output: """{ "body": "aaa" }""", outputTruncated: true);

        var result = await StepRunController().GetStepRunData(data.RunId, data.StepRunId);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StepRunDataResponseModel>();
        model.Input.ShouldBe(data.Input);
        model.InputTruncated.ShouldBeFalse();
        model.Output.ShouldBe(data.Output);
        model.OutputTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetStepRunData_NothingRecorded_ReturnsNullValues()
    {
        var data = GivenStepRunData(input: null, output: null);

        var result = await StepRunController().GetStepRunData(data.RunId, data.StepRunId);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<StepRunDataResponseModel>();
        model.Input.ShouldBeNull();
        model.Output.ShouldBeNull();
    }

    [Fact]
    public async Task GetStepRunData_NonMember_Returns403()
    {
        GivenWorkspaceMember(false);
        var data = GivenStepRunData(input: "{}", output: "{}");

        var result = await StepRunController().GetStepRunData(data.RunId, data.StepRunId);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetStepRunData_AuthorizesAgainstTheWorkspaceAccessPolicy()
    {
        var data = GivenStepRunData(input: null, output: null);

        await StepRunController().GetStepRunData(data.RunId, data.StepRunId);

        _authorizationService.Verify(
            a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), AutomateAuthorizationPolicies.WorkspaceAccess),
            Times.Once);
    }

    [Fact]
    public async Task GetStepRunData_UnknownStepRun_Returns404()
    {
        var result = await StepRunController().GetStepRunData(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeOfType<NotFoundObjectResult>();
        _authorizationService.Verify(
            a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStepRunData_AutomationDeleted_Returns404()
    {
        var data = new StepRunData
        {
            RunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            ActionAlias = "test.action",
        };
        _runService
            .Setup(s => s.GetStepRunDataAsync(data.RunId, data.StepRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);

        var result = await StepRunController().GetStepRunData(data.RunId, data.StepRunId);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTriggerData_ReturnsTheServiceDisplayValue()
    {
        const string triggerData = """{ "apiKey": "***" }""";
        var runId = GivenTriggerData(triggerData, truncated: true);

        var result = await TriggerController().GetTriggerData(runId);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<RunTriggerDataResponseModel>();
        model.TriggerData.ShouldBe(triggerData);
        model.TriggerDataTruncated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTriggerData_NoTriggerData_ReturnsNullValue()
    {
        var runId = GivenTriggerData(null);

        var result = await TriggerController().GetTriggerData(runId);

        var model = result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<RunTriggerDataResponseModel>();
        model.TriggerData.ShouldBeNull();
    }

    [Fact]
    public async Task GetTriggerData_NonMember_Returns403()
    {
        GivenWorkspaceMember(false);
        var runId = GivenTriggerData("{}");

        var result = await TriggerController().GetTriggerData(runId);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetTriggerData_UnknownRun_Returns404()
    {
        var result = await TriggerController().GetTriggerData(Guid.NewGuid());

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    private StepRunData GivenStepRunData(string? input, string? output, bool outputTruncated = false)
    {
        var data = new StepRunData
        {
            RunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            AutomationId = _automation.Id,
            ActionAlias = "test.action",
            Input = input,
            Output = output,
            OutputTruncated = outputTruncated,
        };
        _runService
            .Setup(s => s.GetStepRunDataAsync(data.RunId, data.StepRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        return data;
    }

    private Guid GivenTriggerData(string? triggerData, bool truncated = false)
    {
        var runId = Guid.NewGuid();
        _runService
            .Setup(s => s.GetTriggerDataAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunTriggerData
            {
                RunId = runId,
                AutomationId = _automation.Id,
                TriggerData = triggerData,
                TriggerDataTruncated = truncated,
            });
        return runId;
    }

    private void GivenWorkspaceMember(bool isMember)
        => _authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(isMember ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    private StepRunDataRunController StepRunController()
        => WithContext(new StepRunDataRunController(
            _automationService.Object,
            _runService.Object,
            _authorizationService.Object));

    private TriggerDataRunController TriggerController()
        => WithContext(new TriggerDataRunController(
            _automationService.Object,
            _runService.Object,
            _authorizationService.Object));

    private static T WithContext<T>(T controller)
        where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }
}
