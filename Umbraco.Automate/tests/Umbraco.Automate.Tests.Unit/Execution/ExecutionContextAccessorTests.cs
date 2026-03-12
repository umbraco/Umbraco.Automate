using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class ExecutionContextAccessorTests
{
    [Fact]
    public void ExecutionContext_IsNullByDefault()
    {
        var accessor = new ExecutionContextAccessor();

        accessor.ExecutionContext.ShouldBeNull();
    }

    [Fact]
    public void Set_MakesContextAvailable()
    {
        var accessor = new ExecutionContextAccessor();
        var context = CreateContext();

        using var _ = ExecutionContextAccessor.Set(context);

        accessor.ExecutionContext.ShouldBe(context);
    }

    [Fact]
    public void Dispose_ClearsContext()
    {
        var accessor = new ExecutionContextAccessor();
        var context = CreateContext();

        var scope = ExecutionContextAccessor.Set(context);
        scope.Dispose();

        accessor.ExecutionContext.ShouldBeNull();
    }

    [Fact]
    public async Task Context_FlowsAcrossAsyncCalls()
    {
        var accessor = new ExecutionContextAccessor();
        var context = CreateContext();

        using var _ = ExecutionContextAccessor.Set(context);

        var capturedInTask = await Task.Run(() => accessor.ExecutionContext);

        capturedInTask.ShouldBe(context);
    }

    private static AutomationExecutionContext CreateContext() => new()
    {
        ServiceAccountKey = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Test Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Test Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "user",
        InitiatorId = "test@example.com",
    };
}
