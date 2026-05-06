using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Testing.Builders;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class TriggerSubscriptionRegistryTests
{
    private static AutomateReadinessSignal Ready()
    {
        var s = new AutomateReadinessSignal();
        s.Signal();
        return s;
    }

    private static Mock<IAutomationService> AutomationService(params Automation[] automations)
    {
        var svc = new Mock<IAutomationService>();
        svc.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(automations);
        return svc;
    }

    [Fact]
    public async Task HasSubscribers_BeforeReady_ReturnsTrue()
    {
        // Defensive: don't query the DB or drop events while migrations are still running.
        var svc = AutomationService();
        var notReady = new AutomateReadinessSignal();

        var registry = new TriggerSubscriptionRegistry(
            svc.Object, notReady, Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        var result = await registry.HasSubscribersAsync("anything");

        result.ShouldBeTrue();
        svc.Verify(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasSubscribers_PublishedAutomation_ReturnsTrue()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Published)
            .Build();

        var registry = new TriggerSubscriptionRegistry(
            AutomationService(automation).Object,
            Ready(),
            Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        (await registry.HasSubscribersAsync("umbracoAutomate.contentPublished")).ShouldBeTrue();
    }

    [Fact]
    public async Task HasSubscribers_UnpublishedAutomation_ReturnsFalse()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Draft)
            .Build();

        var registry = new TriggerSubscriptionRegistry(
            AutomationService(automation).Object,
            Ready(),
            Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        (await registry.HasSubscribersAsync("umbracoAutomate.contentPublished")).ShouldBeFalse();
    }

    [Fact]
    public async Task HasSubscribers_InactiveAutomation_ReturnsFalse()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Inactive)
            .Build();

        var registry = new TriggerSubscriptionRegistry(
            AutomationService(automation).Object,
            Ready(),
            Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        (await registry.HasSubscribersAsync("umbracoAutomate.contentPublished")).ShouldBeFalse();
    }

    [Fact]
    public async Task HasSubscribers_DifferentAlias_ReturnsFalse()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Published)
            .Build();

        var registry = new TriggerSubscriptionRegistry(
            AutomationService(automation).Object,
            Ready(),
            Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        (await registry.HasSubscribersAsync("umbracoAutomate.contentSaved")).ShouldBeFalse();
    }

    [Fact]
    public async Task HasSubscribers_AliasMatchIsCaseInsensitive()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Published)
            .Build();

        var registry = new TriggerSubscriptionRegistry(
            AutomationService(automation).Object,
            Ready(),
            Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        (await registry.HasSubscribersAsync("UMBRACOAUTOMATE.CONTENTPUBLISHED")).ShouldBeTrue();
    }

    [Fact]
    public async Task HasSubscribers_CachesResult()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished")
            .WithStatus(AutomationStatus.Published)
            .Build();

        var svc = AutomationService(automation);
        var registry = new TriggerSubscriptionRegistry(
            svc.Object, Ready(), Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        await registry.HasSubscribersAsync("umbracoAutomate.contentPublished");
        await registry.HasSubscribersAsync("umbracoAutomate.contentPublished");
        await registry.HasSubscribersAsync("umbracoAutomate.contentSaved");

        // Three lookups, one DB query — the cache should hold the answer after the first call.
        svc.Verify(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalidate_CausesRebuildOnNextLookup()
    {
        var svc = AutomationService();
        var registry = new TriggerSubscriptionRegistry(
            svc.Object, Ready(), Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        await registry.HasSubscribersAsync("anything");
        registry.Invalidate();
        await registry.HasSubscribersAsync("anything");

        svc.Verify(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ConcurrentLookups_OnlyBuildOnce()
    {
        // Multiple parallel callers on a cold cache should serialise on the gate so the
        // automation service is hit exactly once.
        var svc = AutomationService();
        var registry = new TriggerSubscriptionRegistry(
            svc.Object, Ready(), Mock.Of<ILogger<TriggerSubscriptionRegistry>>());

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => registry.HasSubscribersAsync("anything"))
            .ToArray();

        await Task.WhenAll(tasks);

        svc.Verify(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
