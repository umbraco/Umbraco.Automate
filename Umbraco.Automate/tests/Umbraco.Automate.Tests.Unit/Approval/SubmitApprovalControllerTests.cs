using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Approval.Controllers;
using Umbraco.Automate.Web.Api.Management.Approval.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Unit.Approval;

public class SubmitApprovalControllerTests
{
    private readonly Mock<IWorkflowHost> _workflowHost = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly SubmitApprovalController _controller;

    public SubmitApprovalControllerTests()
    {
        _controller = new SubmitApprovalController(
            _workflowHost.Object,
            _runService.Object,
            _automationService.Object,
            _authorizationService.Object,
            _securityAccessor.Object,
            Mock.Of<ILogger<SubmitApprovalController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task SubmitDecision_RunNotFound_Returns404()
    {
        _runService.Setup(s => s.GetRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutomationRun?)null);

        var result = await _controller.SubmitDecision(Guid.NewGuid(), Guid.NewGuid(), Approve());

        result.ShouldBeOfType<NotFoundObjectResult>();
        VerifyNoEventPublished();
    }

    [Fact]
    public async Task SubmitDecision_AutomationNotFound_Returns404()
    {
        var run = new AutomationRunBuilder().Build();
        _runService.Setup(s => s.GetRunAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _automationService.Setup(s => s.GetAutomationAsync(run.AutomationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _controller.SubmitDecision(run.Id, Guid.NewGuid(), Approve());

        result.ShouldBeOfType<NotFoundObjectResult>();
        VerifyNoEventPublished();
    }

    [Fact]
    public async Task SubmitDecision_UserNotWorkspaceMember_Returns403AndDoesNotPublish()
    {
        var automation = new AutomationBuilder().Build();
        var run = new AutomationRunBuilder().WithAutomationId(automation.Id).Build();
        SetupRunAndAutomation(run, automation);
        SetupAuthorization(succeeds: false);

        var result = await _controller.SubmitDecision(run.Id, Guid.NewGuid(), Approve());

        var status = result.ShouldBeOfType<StatusCodeResult>();
        status.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        VerifyNoEventPublished();
    }

    [Fact]
    public async Task SubmitDecision_UserIsWorkspaceMember_PublishesDecisionWithApproverIdentity()
    {
        var automation = new AutomationBuilder().Build();
        var run = new AutomationRunBuilder().WithAutomationId(automation.Id).Build();
        var stepId = Guid.NewGuid();
        var userKey = Guid.NewGuid();
        SetupRunAndAutomation(run, automation);
        SetupAuthorization(succeeds: true);
        SetupCurrentUser(userKey);

        ApprovalDecision? published = null;
        _workflowHost
            .Setup(h => h.PublishEvent(
                RequestApprovalAction.ApprovalEventName,
                $"{run.Id}:{stepId}",
                It.IsAny<object>(),
                It.IsAny<DateTime?>()))
            .Callback<string, string, object, DateTime?>((_, _, data, _) => published = data as ApprovalDecision)
            .Returns(Task.CompletedTask);

        var result = await _controller.SubmitDecision(run.Id, stepId, Approve("Looks good"));

        result.ShouldBeOfType<OkResult>();
        published.ShouldNotBeNull();
        published!.ApprovedByUserKey.ShouldBe(userKey);
        published.Outcome.ShouldBe(ApprovalOutcome.Approved);
        published.Comment.ShouldBe("Looks good");
    }

    private static ApprovalDecisionRequestModel Approve(string? comment = null)
        => new() { Outcome = ApprovalOutcome.Approved, Comment = comment };

    private void SetupRunAndAutomation(AutomationRun run, Automation automation)
    {
        _runService.Setup(s => s.GetRunAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);
        _automationService.Setup(s => s.GetAutomationAsync(run.AutomationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
    }

    private void SetupAuthorization(bool succeeds)
        => _authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    private void SetupCurrentUser(Guid userKey)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(userKey);
        var security = new Mock<IBackOfficeSecurity>();
        security.Setup(s => s.CurrentUser).Returns(user.Object);
        _securityAccessor.Setup(a => a.BackOfficeSecurity).Returns(security.Object);
    }

    private void VerifyNoEventPublished()
        => _workflowHost.Verify(
            h => h.PublishEvent(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<DateTime?>()),
            Times.Never);
}
