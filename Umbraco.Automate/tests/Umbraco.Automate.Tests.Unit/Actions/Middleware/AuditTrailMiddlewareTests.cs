using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Execution;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class AuditTrailMiddlewareTests
{
    private readonly Mock<IAuditEntryService> _auditEntryService = new();
    private readonly AuditTrailMiddleware _middleware;

    public AuditTrailMiddlewareTests()
    {
        _middleware = new AuditTrailMiddleware(
            _auditEntryService.Object,
            Mock.Of<ILogger<AuditTrailMiddleware>>());
    }

    private static AutomationExecutionContext CreateExecutionContext() => new()
    {
        ServiceAccountKey = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Test Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Test Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "system",
        AllowedConnections = [],
    };

    private static ActionContext CreateContext(IAction? action = null, AutomationExecutionContext? executionContext = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.publishContent",
        Action = action,
        ExecutionContext = executionContext,
    };

    [Fact]
    public async Task ApplyAsync_CmsAction_Success_WritesAuditEntry()
    {
        var cmsAction = new Mock<IAction>();
        cmsAction.As<ICmsAction>();

        var ctx = CreateContext(cmsAction.Object, CreateExecutionContext());
        var successResult = ActionResult.Success(new { Key = "value" });

        _auditEntryService
            .Setup(a => a.WriteAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.Is<string>(t => t.Contains("publishContent")),
                It.IsAny<string>()))
            .ReturnsAsync(Mock.Of<IAuditEntry>());

        var result = await _middleware.ApplyAsync(
            ctx,
            (_, _) => Task.FromResult(successResult),
            CancellationToken.None);

        result.ShouldBeSameAs(successResult);
        _auditEntryService.Verify(
            a => a.WriteAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.Is<string>(t => t.Contains("publishContent")),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_NonCmsAction_DoesNotWriteAudit()
    {
        var action = Mock.Of<IAction>();
        var ctx = CreateContext(action, CreateExecutionContext());
        var successResult = ActionResult.Success();

        var result = await _middleware.ApplyAsync(
            ctx,
            (_, _) => Task.FromResult(successResult),
            CancellationToken.None);

        result.ShouldBeSameAs(successResult);
        _auditEntryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApplyAsync_CmsAction_Failed_DoesNotWriteAudit()
    {
        var cmsAction = new Mock<IAction>();
        cmsAction.As<ICmsAction>();

        var ctx = CreateContext(cmsAction.Object, CreateExecutionContext());
        var failedResult = ActionResult.Failed(new InvalidOperationException("fail"));

        var result = await _middleware.ApplyAsync(
            ctx,
            (_, _) => Task.FromResult(failedResult),
            CancellationToken.None);

        result.ShouldBeSameAs(failedResult);
        _auditEntryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApplyAsync_CmsAction_NoExecutionContext_DoesNotWriteAudit()
    {
        var cmsAction = new Mock<IAction>();
        cmsAction.As<ICmsAction>();

        var ctx = CreateContext(cmsAction.Object, executionContext: null);
        var successResult = ActionResult.Success();

        var result = await _middleware.ApplyAsync(
            ctx,
            (_, _) => Task.FromResult(successResult),
            CancellationToken.None);

        result.ShouldBeSameAs(successResult);
        _auditEntryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApplyAsync_AuditServiceThrows_DoesNotFailAction()
    {
        var cmsAction = new Mock<IAction>();
        cmsAction.As<ICmsAction>();

        var ctx = CreateContext(cmsAction.Object, CreateExecutionContext());
        var successResult = ActionResult.Success();

        _auditEntryService
            .Setup(a => a.WriteAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("audit failed"));

        var result = await _middleware.ApplyAsync(
            ctx,
            (_, _) => Task.FromResult(successResult),
            CancellationToken.None);

        result.ShouldBeSameAs(successResult);
    }
}
