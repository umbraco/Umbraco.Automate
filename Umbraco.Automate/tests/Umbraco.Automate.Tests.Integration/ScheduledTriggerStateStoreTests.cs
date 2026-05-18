using Umbraco.Automate.Persistence.Triggers;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

// See also UtcDateTimeRoundTripTests for the cross-cutting converter coverage
// across other entities (AutomationRun, etc.).

/// <summary>
/// Verifies the state store survives the DB round-trip without losing
/// <see cref="DateTimeKind.Utc"/> — Cronos requires Utc-kinded DateTimes
/// when calculating the next occurrence, so dropping the kind silently
/// disabled scheduled triggers after their first run.
/// </summary>
public class ScheduledTriggerStateStoreTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly ScheduledTriggerStateStore _store;

    public ScheduledTriggerStateStoreTests()
    {
        _fixture = new EfCoreTestFixture();
        _store = new ScheduledTriggerStateStore(new TestDbContextFactory(_fixture.CreateContext));
    }

    [Fact]
    public async Task GetLastFiredAsync_ReturnsNull_WhenNeverFired()
    {
        var result = await _store.GetLastFiredAsync(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetLastFiredAsync_PreservesUtcKind_AfterRoundTrip()
    {
        var automationId = Guid.NewGuid();
        var written = new DateTime(2026, 5, 18, 9, 0, 0, DateTimeKind.Utc);

        await _store.SetLastFiredAsync(automationId, written, CancellationToken.None);
        var loaded = await _store.GetLastFiredAsync(automationId, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Value.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.Value.ShouldBe(written);
    }

    [Fact]
    public async Task SetLastFiredAsync_NormalizesLocalKind_ToUtc()
    {
        var automationId = Guid.NewGuid();
        var local = new DateTime(2026, 5, 18, 11, 0, 0, DateTimeKind.Local);

        await _store.SetLastFiredAsync(automationId, local, CancellationToken.None);
        var loaded = await _store.GetLastFiredAsync(automationId, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Value.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.Value.ShouldBe(local.ToUniversalTime());
    }

    [Fact]
    public async Task SetLastFiredAsync_OverwritesExistingValue()
    {
        var automationId = Guid.NewGuid();
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await _store.SetLastFiredAsync(automationId, first, CancellationToken.None);
        await _store.SetLastFiredAsync(automationId, second, CancellationToken.None);

        var loaded = await _store.GetLastFiredAsync(automationId, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded.Value.ShouldBe(second);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
