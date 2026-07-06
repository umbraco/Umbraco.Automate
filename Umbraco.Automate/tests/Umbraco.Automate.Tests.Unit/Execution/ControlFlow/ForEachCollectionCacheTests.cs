using Shouldly;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class ForEachCollectionCacheTests
{
    private readonly BindingEvaluator _bindingEvaluator = new(new BindingFilterCollection(Array.Empty<IBindingFilter>));

    private ForEachCollectionCache CreateCache() => new(_bindingEvaluator);

    private static AutomationWorkflowData CreateData() => new()
    {
        RunId = Guid.NewGuid(),
        AutomationId = Guid.NewGuid(),
        TriggerOutput = [],
    };

    [Fact]
    public void GetOrMaterializeCollection_SecondCall_ReturnsCachedList()
    {
        var cache = CreateCache();
        var data = CreateData();
        data.TriggerOutput["items"] = "[\"a\",\"b\"]";
        var containerId = Guid.NewGuid();

        var first = cache.GetOrMaterializeCollection(data, containerId, null, "${trigger.items}");

        // Mutating the underlying data must not affect the cached collection.
        data.TriggerOutput["items"] = "[]";
        var second = cache.GetOrMaterializeCollection(data, containerId, null, "${trigger.items}");

        second.ShouldBeSameAs(first);
        second.Count.ShouldBe(2);
    }

    [Fact]
    public void ResolveItem_WithStashedCollection_ReturnsIndexedItem()
    {
        var cache = CreateCache();
        var data = CreateData();
        var containerId = Guid.NewGuid();
        data.ContainerCollections[containerId] = "alpha, beta, gamma";

        var item = cache.ResolveItem(data, new ForEachIterationContext(null, 1, containerId));

        item.ShouldBe("beta");
    }

    [Fact]
    public void ResolveItem_ContainerWithoutStashedCollection_ReturnsNull()
    {
        // While/Parallel containers stash no collection — their iterations carry no item.
        var cache = CreateCache();
        var data = CreateData();

        var item = cache.ResolveItem(data, new ForEachIterationContext(null, 0, Guid.NewGuid()));

        item.ShouldBeNull();
    }

    [Fact]
    public void ResolveItem_IndexBeyondCollection_ReturnsNull()
    {
        var cache = CreateCache();
        var data = CreateData();
        var containerId = Guid.NewGuid();
        data.ContainerCollections[containerId] = "a, b";

        cache.ResolveItem(data, new ForEachIterationContext(null, 5, containerId)).ShouldBeNull();
    }

    [Fact]
    public void ResolveItem_FreshCacheInstance_RematerializesNestedCollections()
    {
        // Restart recovery: a fresh cache (empty, as after a process restart) must resolve
        // an inner iteration's item by recursively re-evaluating the stashed collection
        // expressions — the inner collection references the outer loop's item.
        var data = CreateData();
        data.TriggerOutput["groups"] = "[[\"a\",\"b\"],[\"c\",\"d\"]]";
        var outerContainerId = Guid.NewGuid();
        var innerContainerId = Guid.NewGuid();
        data.ContainerCollections[outerContainerId] = "${trigger.groups}";
        data.ContainerCollections[innerContainerId] = "${loop.item}";

        var outerIteration = new ForEachIterationContext(null, 1, outerContainerId);
        var innerIteration = new ForEachIterationContext(null, 0, innerContainerId, outerIteration);

        var item = CreateCache().ResolveItem(data, innerIteration);

        item.ShouldBe("c");
    }

    [Fact]
    public void EvictCollection_RemovesEntry_SoNextAccessRematerializes()
    {
        var cache = CreateCache();
        var data = CreateData();
        data.TriggerOutput["items"] = "[\"a\",\"b\"]";
        var containerId = Guid.NewGuid();

        cache.GetOrMaterializeCollection(data, containerId, null, "${trigger.items}").Count.ShouldBe(2);

        data.TriggerOutput["items"] = "[\"only\"]";
        cache.EvictCollection(data.RunId, containerId, null);

        cache.GetOrMaterializeCollection(data, containerId, null, "${trigger.items}").Count.ShouldBe(1);
    }
}
