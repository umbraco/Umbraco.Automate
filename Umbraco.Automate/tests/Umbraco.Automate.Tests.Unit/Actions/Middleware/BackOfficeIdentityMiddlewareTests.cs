using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class BackOfficeIdentityMiddlewareTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly BackOfficeIdentityMiddleware _middleware;

    public BackOfficeIdentityMiddlewareTests()
    {
        _middleware = new BackOfficeIdentityMiddleware(
            _userService.Object,
            Mock.Of<ILogger<BackOfficeIdentityMiddleware>>());
    }

    [Fact]
    public async Task ApplyAsync_NoExecutionContext_DelegatesToNext()
    {
        var context = CreateContext(executionContext: null);
        var expected = ActionResult.Success();

        var result = await _middleware.ApplyAsync(
            context,
            (_, _) => Task.FromResult(expected),
            CancellationToken.None);

        result.ShouldBeSameAs(expected);
        _userService.Verify(s => s.GetAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_ServiceAccountNotFound_ReturnsAuthenticationError()
    {
        var serviceAccountKey = Guid.NewGuid();
        var context = CreateContext(CreateExecutionContext(serviceAccountKey));

        _userService.Setup(s => s.GetAsync(serviceAccountKey)).ReturnsAsync((IUser?)null);

        var result = await _middleware.ApplyAsync(
            context,
            (_, _) => Task.FromResult(ActionResult.Success()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task ApplyAsync_ServiceAccountResolved_SetsBackOfficeIdentity()
    {
        var serviceAccountKey = Guid.NewGuid();
        var context = CreateContext(CreateExecutionContext(serviceAccountKey));

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(serviceAccountKey);
        _userService.Setup(s => s.GetAsync(serviceAccountKey)).ReturnsAsync(user.Object);

        IBackOfficeSecurity? capturedSecurity = null;

        var result = await _middleware.ApplyAsync(
            context,
            (_, _) =>
            {
                // Inside the pipeline, the AsyncLocal should be set.
                // We can verify by creating a new accessor and reading it.
                capturedSecurity = AutomateBackOfficeSecurityAccessor.GetCurrentForTesting();
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        capturedSecurity.ShouldNotBeNull();
        capturedSecurity.CurrentUser.ShouldNotBeNull();
        capturedSecurity.CurrentUser!.Key.ShouldBe(serviceAccountKey);
    }

    [Fact]
    public async Task ApplyAsync_ClearsIdentityAfterExecution()
    {
        var serviceAccountKey = Guid.NewGuid();
        var context = CreateContext(CreateExecutionContext(serviceAccountKey));

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(serviceAccountKey);
        _userService.Setup(s => s.GetAsync(serviceAccountKey)).ReturnsAsync(user.Object);

        await _middleware.ApplyAsync(
            context,
            (_, _) => Task.FromResult(ActionResult.Success()),
            CancellationToken.None);

        // After disposal, the AsyncLocal should be cleared.
        var afterSecurity = AutomateBackOfficeSecurityAccessor.GetCurrentForTesting();
        afterSecurity.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ClearsIdentityEvenOnException()
    {
        var serviceAccountKey = Guid.NewGuid();
        var context = CreateContext(CreateExecutionContext(serviceAccountKey));

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(serviceAccountKey);
        _userService.Setup(s => s.GetAsync(serviceAccountKey)).ReturnsAsync(user.Object);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _middleware.ApplyAsync(
                context,
                (_, _) => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        // After exception, the AsyncLocal should still be cleared.
        var afterSecurity = AutomateBackOfficeSecurityAccessor.GetCurrentForTesting();
        afterSecurity.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ServiceAccountResolved_NextDelegateReceivesContext()
    {
        var serviceAccountKey = Guid.NewGuid();
        var context = CreateContext(CreateExecutionContext(serviceAccountKey));

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(serviceAccountKey);
        _userService.Setup(s => s.GetAsync(serviceAccountKey)).ReturnsAsync(user.Object);

        ActionContext? capturedContext = null;

        await _middleware.ApplyAsync(
            context,
            (ctx, _) =>
            {
                capturedContext = ctx;
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        capturedContext.ShouldBeSameAs(context);
    }

    private static AutomationExecutionContext CreateExecutionContext(Guid serviceAccountKey) => new()
    {
        ServiceAccountKey = serviceAccountKey,
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Test Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Test Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "test",
        AllowedConnections = [],
    };

    private static ActionContext CreateContext(AutomationExecutionContext? executionContext = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.publishContent",
        ExecutionContext = executionContext,
    };
}
