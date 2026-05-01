using Shouldly;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class AutomationOriginAccessorTests
{
    [Fact]
    public void Current_DefaultsToNull()
    {
        var accessor = new AutomationOriginAccessor();

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void Push_SetsCurrentOrigin()
    {
        var accessor = new AutomationOriginAccessor();
        var origin = CreateOrigin();

        using var _ = accessor.Push(origin);

        accessor.Current.ShouldBe(origin);
    }

    [Fact]
    public void Push_DisposingScope_RestoresPrevious()
    {
        var accessor = new AutomationOriginAccessor();
        var origin = CreateOrigin();

        using (accessor.Push(origin))
        {
            accessor.Current.ShouldBe(origin);
        }

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public void Push_NestedScopes_RestoreInOrder()
    {
        var accessor = new AutomationOriginAccessor();
        var outer = CreateOrigin();
        var inner = CreateOrigin();

        using (accessor.Push(outer))
        {
            accessor.Current.ShouldBe(outer);

            using (accessor.Push(inner))
            {
                accessor.Current.ShouldBe(inner);
            }

            accessor.Current.ShouldBe(outer);
        }

        accessor.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Current_FlowsAcrossAwaits()
    {
        // Models the production case: middleware pushes, action awaits a CMS service,
        // notification handler reads — all on the same async flow.
        var accessor = new AutomationOriginAccessor();
        var origin = CreateOrigin();
        AutomationOrigin? observed = null;

        using var _ = accessor.Push(origin);

        await Task.Yield();
        await Task.Run(() => observed = accessor.Current);

        observed.ShouldBe(origin);
    }

    [Fact]
    public async Task Current_IsolatedAcrossParallelFlows()
    {
        // Two independent Push scopes started from sibling tasks must not see each other's
        // origin. AsyncLocal copy-on-write semantics make this work; verifying it explicitly
        // because a regression here would silently mis-attribute origins under concurrent load.
        var accessor = new AutomationOriginAccessor();
        var first = CreateOrigin();
        var second = CreateOrigin();

        var t1 = Task.Run(async () =>
        {
            using var _ = accessor.Push(first);
            await Task.Delay(10);
            return accessor.Current;
        });

        var t2 = Task.Run(async () =>
        {
            using var _ = accessor.Push(second);
            await Task.Delay(10);
            return accessor.Current;
        });

        var results = await Task.WhenAll(t1, t2);

        results[0].ShouldBe(first);
        results[1].ShouldBe(second);
    }

    private static AutomationOrigin CreateOrigin() => new(
        RunId: Guid.NewGuid(),
        AutomationId: Guid.NewGuid(),
        WorkspaceId: Guid.NewGuid(),
        ChainDepth: 0);
}
