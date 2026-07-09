using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Unit tests for <see cref="WorkflowLockProvider"/> — WorkflowCore's <c>IDistributedLockProvider</c>
/// backed by <see cref="IWorkflowLockStore"/> — against a mocked store and a hand-rolled fake
/// <see cref="TimeProvider"/> (no <c>Microsoft.Extensions.TimeProvider.Testing</c> package is
/// referenced anywhere in this solution).
/// </summary>
public class WorkflowLockProviderTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset start) => _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static WorkflowLockProvider CreateProvider(
        Mock<IWorkflowLockStore> store, FakeTimeProvider timeProvider, WorkflowLockOptions? options = null)
    {
        return new WorkflowLockProvider(
            store.Object,
            Options.Create(options ?? new WorkflowLockOptions()),
            timeProvider,
            NullLogger<WorkflowLockProvider>.Instance);
    }

    [Fact]
    public async Task AcquireLock_ReturnsTrue_AndCallsStore_WithOwnerTokenAndComputedExpiry_WhenStoreSucceeds()
    {
        var start = new DateTimeOffset(2026, 7, 8, 10, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(start);
        var options = new WorkflowLockOptions { LeaseDuration = TimeSpan.FromSeconds(30) };

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                "lock-1", It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var provider = CreateProvider(store, timeProvider, options);

        var acquired = await provider.AcquireLock("lock-1", CancellationToken.None);

        acquired.ShouldBeTrue();
        store.Verify(s => s.TryAcquireAsync(
            "lock-1",
            It.IsAny<Guid>(),
            start.UtcDateTime,
            start.UtcDateTime + options.LeaseDuration,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcquireLock_ReturnsFalse_WhenStoreFails()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var provider = CreateProvider(store, timeProvider);

        var acquired = await provider.AcquireLock("lock-1", CancellationToken.None);

        acquired.ShouldBeFalse();
    }

    [Fact]
    public async Task AcquireLock_UsesSameOwnerToken_AcrossMultipleCalls()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var capturedTokens = new List<Guid>();

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, DateTime, DateTime, CancellationToken>((_, token, _, _, _) => capturedTokens.Add(token))
            .ReturnsAsync(true);

        var provider = CreateProvider(store, timeProvider);

        await provider.AcquireLock("lock-1", CancellationToken.None);
        await provider.AcquireLock("lock-2", CancellationToken.None);

        capturedTokens.Count.ShouldBe(2);
        capturedTokens[0].ShouldNotBe(Guid.Empty);
        capturedTokens[0].ShouldBe(capturedTokens[1]);
    }

    [Fact]
    public async Task ReleaseLock_CallsStore_WithSameOwnerTokenUsedToAcquire()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        Guid? acquireToken = null;

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, DateTime, DateTime, CancellationToken>((_, token, _, _, _) => acquireToken = token)
            .ReturnsAsync(true);
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = CreateProvider(store, timeProvider);

        await provider.AcquireLock("lock-1", CancellationToken.None);
        await provider.ReleaseLock("lock-1");

        acquireToken.ShouldNotBeNull();
        store.Verify(s => s.ReleaseAsync("lock-1", acquireToken!.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Start_PeriodicallyRenews_OnlyIdsCurrentlyOwned()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new WorkflowLockOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(30),
            RenewalInterval = TimeSpan.FromMilliseconds(30),
        };

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, Guid _, DateTime _, DateTime _, CancellationToken _) => id != "fails-to-acquire");
        store.Setup(s => s.RenewAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = CreateProvider(store, timeProvider, options);

        try
        {
            await provider.AcquireLock("owned-1", CancellationToken.None);
            await provider.AcquireLock("owned-2", CancellationToken.None);
            var failedAcquire = await provider.AcquireLock("fails-to-acquire", CancellationToken.None);
            failedAcquire.ShouldBeFalse();

            await provider.Start();

            // Poll for the background renewal loop to have ticked at least once (real time —
            // PeriodicTimer isn't driven by TimeProvider — see WorkflowLockProviderTests remarks).
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            IReadOnlyCollection<string>? renewedIds = null;
            store.Setup(s => s.RenewAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyCollection<string>, Guid, DateTime, CancellationToken>((ids, _, _, _) => renewedIds = ids)
                .Returns(Task.CompletedTask);

            while (renewedIds is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            renewedIds.ShouldNotBeNull();
            renewedIds.ShouldContain("owned-1");
            renewedIds.ShouldContain("owned-2");
            renewedIds.ShouldNotContain("fails-to-acquire");
        }
        finally
        {
            await provider.Stop();
        }
    }

    [Fact]
    public async Task Stop_ReleasesEveryOwnedId()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new WorkflowLockOptions { RenewalInterval = TimeSpan.FromMilliseconds(50) };

        var released = new List<string>();
        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(s => s.RenewAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, CancellationToken>((id, _, _) => released.Add(id))
            .Returns(Task.CompletedTask);

        var provider = CreateProvider(store, timeProvider, options);

        await provider.AcquireLock("owned-1", CancellationToken.None);
        await provider.AcquireLock("owned-2", CancellationToken.None);
        await provider.Start();

        await provider.Stop();

        released.ShouldContain("owned-1");
        released.ShouldContain("owned-2");
        released.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Stop_DoesNotThrow_WhenNothingWasEverAcquiredOrStarted()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new Mock<IWorkflowLockStore>();
        var provider = CreateProvider(store, timeProvider);

        await Should.NotThrowAsync(() => provider.Stop());
    }

    [Fact]
    public async Task Stop_CalledTwice_DoesNotThrow()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var options = new WorkflowLockOptions { RenewalInterval = TimeSpan.FromMilliseconds(50) };

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        store.Setup(s => s.RenewAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var provider = CreateProvider(store, timeProvider, options);

        await provider.AcquireLock("owned-1", CancellationToken.None);
        await provider.Start();
        await provider.Stop();

        await Should.NotThrowAsync(() => provider.Stop());
    }

    [Fact]
    public async Task FailedAcquire_IsNotTrackedAsOwned_SoReleaseIsNotCalledForIt()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var released = new List<string>();

        var store = new Mock<IWorkflowLockStore>();
        store.Setup(s => s.TryAcquireAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, CancellationToken>((id, _, _) => released.Add(id))
            .Returns(Task.CompletedTask);

        var provider = CreateProvider(store, timeProvider);

        var acquired = await provider.AcquireLock("never-owned", CancellationToken.None);
        acquired.ShouldBeFalse();

        await provider.Start();
        await provider.Stop();

        released.ShouldNotContain("never-owned");
        store.Verify(s => s.ReleaseAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
