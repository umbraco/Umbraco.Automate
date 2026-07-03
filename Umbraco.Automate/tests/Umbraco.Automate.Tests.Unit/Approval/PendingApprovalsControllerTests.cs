using System.Security.Principal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Approval.Controllers;
using Umbraco.Automate.Web.Api.Management.Approval.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Approval;

public class PendingApprovalsControllerTests
{
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<IAuthorizationHelper> _authorizationHelper = new();
    private readonly PendingApprovalsController _controller;

    public PendingApprovalsControllerTests()
    {
        _controller = new PendingApprovalsController(
            _runService.Object,
            _automationService.Object,
            _workspaceService.Object,
            _authorizationHelper.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetPendingApprovals_Admin_ReturnsAllWorkspaces()
    {
        var (workspaceA, workspaceB) = (Guid.NewGuid(), Guid.NewGuid());
        SetupPending(workspaceA, workspaceB);
        SetupUser(CreateUser(isAdmin: true));

        var models = await GetModels();

        models.Count.ShouldBe(2);
        _workspaceService.Verify(
            s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPendingApprovals_NonAdmin_ReturnsOnlyAccessibleWorkspaces()
    {
        var (workspaceA, workspaceB) = (Guid.NewGuid(), Guid.NewGuid());
        SetupPending(workspaceA, workspaceB);

        var groupKey = Guid.NewGuid();
        SetupUser(CreateUser(isAdmin: false, groupKeys: [groupKey]));
        _workspaceService
            .Setup(s => s.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { workspaceA });

        var models = await GetModels();

        models.Count.ShouldBe(1);
        models[0].AutomationName.ShouldBe("Workspace A automation");
    }

    [Fact]
    public async Task GetPendingApprovals_PopulatesPromptFromOutputData()
    {
        var workspace = Guid.NewGuid();
        var automation = new AutomationBuilder().WithWorkspaceId(workspace).WithName("A").Build();
        var run = new AutomationRunBuilder().WithAutomationId(automation.Id).WithWorkspaceId(workspace).Build();
        var stepId = Guid.NewGuid();
        var stepRun = new StepRun
        {
            RunId = run.Id,
            StepId = stepId,
            ActionAlias = RequestApprovalAction.ApprovalActionAlias,
            Status = StepRunStatus.WaitingForInput,
            StartedUtc = DateTime.UtcNow,
            OutputData = $$"""{"prompt":"Please approve the release","runId":"{{run.Id}}","stepId":"{{stepId}}","automationId":"{{automation.Id}}","requestedUtc":"2026-07-03T00:00:00Z"}""",
        };

        _runService
            .Setup(s => s.GetStepRunsByStatusAsync(
                RequestApprovalAction.ApprovalActionAlias, StepRunStatus.WaitingForInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(run, stepRun)]);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        SetupUser(CreateUser(isAdmin: true));

        var models = await GetModels();

        models.Count.ShouldBe(1);
        models[0].Prompt.ShouldBe("Please approve the release");
    }

    [Fact]
    public async Task GetPendingApprovals_MultipleRunsOfSameAutomation_LooksUpAutomationOnce()
    {
        var workspace = Guid.NewGuid();
        var automation = new AutomationBuilder().WithWorkspaceId(workspace).WithName("A").Build();
        var runA = new AutomationRunBuilder().WithAutomationId(automation.Id).WithWorkspaceId(workspace).Build();
        var runB = new AutomationRunBuilder().WithAutomationId(automation.Id).WithWorkspaceId(workspace).Build();

        _runService
            .Setup(s => s.GetStepRunsByStatusAsync(
                RequestApprovalAction.ApprovalActionAlias, StepRunStatus.WaitingForInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(runA, StepFor(runA)), (runB, StepFor(runB))]);
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        SetupUser(CreateUser(isAdmin: true));

        var models = await GetModels();

        models.Count.ShouldBe(2);
        _automationService.Verify(
            s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<List<PendingApprovalResponseModel>> GetModels()
    {
        var result = await _controller.GetPendingApprovals(CancellationToken.None);
        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        return ((IEnumerable<PendingApprovalResponseModel>)ok.Value!).ToList();
    }

    private void SetupPending(Guid workspaceA, Guid workspaceB)
    {
        var automationA = new AutomationBuilder().WithWorkspaceId(workspaceA).WithName("Workspace A automation").Build();
        var automationB = new AutomationBuilder().WithWorkspaceId(workspaceB).WithName("Workspace B automation").Build();
        var runA = new AutomationRunBuilder().WithAutomationId(automationA.Id).WithWorkspaceId(workspaceA).Build();
        var runB = new AutomationRunBuilder().WithAutomationId(automationB.Id).WithWorkspaceId(workspaceB).Build();

        _runService
            .Setup(s => s.GetStepRunsByStatusAsync(
                RequestApprovalAction.ApprovalActionAlias, StepRunStatus.WaitingForInput, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(runA, StepFor(runA)), (runB, StepFor(runB))]);
        _automationService.Setup(s => s.GetAutomationAsync(automationA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automationA);
        _automationService.Setup(s => s.GetAutomationAsync(automationB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automationB);
    }

    private static StepRun StepFor(AutomationRun run) => new()
    {
        RunId = run.Id,
        StepId = Guid.NewGuid(),
        ActionAlias = RequestApprovalAction.ApprovalActionAlias,
        Status = StepRunStatus.WaitingForInput,
        StartedUtc = DateTime.UtcNow,
    };

    private void SetupUser(IUser user)
        => _authorizationHelper.Setup(h => h.GetUmbracoUser(It.IsAny<IPrincipal>())).Returns(user);

    private static IUser CreateUser(bool isAdmin, IEnumerable<Guid>? groupKeys = null)
    {
        var user = new Mock<IUser>();
        var groups = new List<IReadOnlyUserGroup>();

        if (isAdmin)
        {
            var adminGroup = new Mock<IReadOnlyUserGroup>();
            adminGroup.Setup(g => g.Alias).Returns(Umbraco.Cms.Core.Constants.Security.AdminGroupAlias);
            adminGroup.Setup(g => g.Key).Returns(Guid.NewGuid());
            groups.Add(adminGroup.Object);
        }

        foreach (var key in groupKeys ?? [])
        {
            var group = new Mock<IReadOnlyUserGroup>();
            group.Setup(g => g.Alias).Returns($"group-{key:N}");
            group.Setup(g => g.Key).Returns(key);
            groups.Add(group.Object);
        }

        user.Setup(u => u.Groups).Returns(groups);
        return user.Object;
    }
}
