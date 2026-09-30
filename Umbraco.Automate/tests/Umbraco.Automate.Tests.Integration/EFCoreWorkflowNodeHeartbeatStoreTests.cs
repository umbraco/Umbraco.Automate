using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreWorkflowNodeHeartbeatStore"/> against real SQLite.
/// </summary>
public class EFCoreWorkflowNodeHeartbeatStoreTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly EFCoreWorkflowNodeHeartbeatStore _store;

    public EFCoreWorkflowNodeHeartbeatStoreTests()
    {
        _store = new EFCoreWorkflowNodeHeartbeatStore(new TestDbContextFactory(_fixture.CreateContext));
    }

    [Fact]
    public async Task BeatAsync_FirstBeatCreatesRow_LaterBeatsAdvanceTheCounter()
    {
        var nodeId = Guid.NewGuid();
        var first = new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);

        await _store.BeatAsync(nodeId, first, CancellationToken.None);
        await _store.BeatAsync(nodeId, first.AddSeconds(10), CancellationToken.None);
        await _store.BeatAsync(nodeId, first.AddSeconds(20), CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var row = await db.WorkflowNodeHeartbeats.SingleAsync(h => h.NodeId == nodeId);
        row.Beat.ShouldBe(3);
        row.HeartbeatUtc.ShouldBe(first.AddSeconds(20));
    }

    [Fact]
    public async Task RemoveAsync_DeletesOnlyThatNodesRow()
    {
        var leaving = Guid.NewGuid();
        var staying = Guid.NewGuid();
        await _store.BeatAsync(leaving, DateTime.UtcNow, CancellationToken.None);
        await _store.BeatAsync(staying, DateTime.UtcNow, CancellationToken.None);

        await _store.RemoveAsync(leaving, CancellationToken.None);

        await using var db = _fixture.CreateContext();
        (await db.WorkflowNodeHeartbeats.Select(h => h.NodeId).ToListAsync()).ShouldBe([staying]);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
