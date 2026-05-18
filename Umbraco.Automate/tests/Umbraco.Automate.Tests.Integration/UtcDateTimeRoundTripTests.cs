using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence.Outbox;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Triggers;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Verifies the global UTC ValueConverter on <c>UmbracoAutomateDbContext</c>
/// applies to every DateTime column in the model — not just the one that
/// surfaced the bug. Without this, every `*Utc` timestamp would silently
/// shift by the server's local offset when consumed by anything that
/// reads <see cref="DateTime.Kind"/> (Cronos, the API's UtcDateTimeJsonConverter,
/// ToUniversalTime, DateTimeOffset constructors, etc.).
/// </summary>
public class UtcDateTimeRoundTripTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;

    public UtcDateTimeRoundTripTests() => _fixture = new EfCoreTestFixture();

    [Fact]
    public async Task AutomationRun_StartedAndCompletedUtc_RoundTripAsUtc()
    {
        var id = Guid.NewGuid();
        var started = new DateTime(2026, 5, 18, 9, 0, 0, DateTimeKind.Utc);
        var completed = new DateTime(2026, 5, 18, 9, 5, 0, DateTimeKind.Utc);

        await using (var db = _fixture.CreateContext())
        {
            db.AutomationRuns.Add(new AutomationRunEntity
            {
                Id = id,
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                Status = 0,
                StartedUtc = started,
                CompletedUtc = completed,
                InitiatedBy = "test",
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var loaded = await db.AutomationRuns.SingleAsync(r => r.Id == id);

            loaded.StartedUtc.ShouldNotBeNull();
            loaded.StartedUtc.Value.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.StartedUtc.Value.ShouldBe(started);

            loaded.CompletedUtc.ShouldNotBeNull();
            loaded.CompletedUtc.Value.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.CompletedUtc.Value.ShouldBe(completed);
        }
    }

    [Fact]
    public async Task OutboxMessage_AllUtcFields_RoundTripAsUtc()
    {
        var createdAt = new DateTime(2026, 5, 18, 9, 0, 0, DateTimeKind.Utc);
        var retryAt = new DateTime(2026, 5, 18, 9, 1, 0, DateTimeKind.Utc);
        var claimedAt = new DateTime(2026, 5, 18, 9, 2, 0, DateTimeKind.Utc);
        long id;

        await using (var db = _fixture.CreateContext())
        {
            var entity = new OutboxMessageEntity
            {
                Topic = "test",
                Body = "{}",
                CreatedUtc = createdAt,
                NextRetryUtc = retryAt,
                ClaimedUtc = claimedAt,
                Status = 0,
            };
            db.OutboxMessages.Add(entity);
            await db.SaveChangesAsync();
            id = entity.Id;
        }

        await using (var db = _fixture.CreateContext())
        {
            var loaded = await db.OutboxMessages.SingleAsync(m => m.Id == id);

            loaded.CreatedUtc.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.CreatedUtc.ShouldBe(createdAt);

            loaded.NextRetryUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.NextRetryUtc.Value.ShouldBe(retryAt);

            loaded.ClaimedUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.ClaimedUtc.Value.ShouldBe(claimedAt);
        }
    }

    [Fact]
    public async Task LocalDateTime_OnWrite_NormalizesToUtcInstant()
    {
        // The converter should treat Kind=Local as "convert to UTC", so a 11:00
        // Local timestamp in CPH (UTC+2 summer / +1 winter) lands at 09:00 or 10:00 UTC.
        var local = new DateTime(2026, 6, 18, 11, 0, 0, DateTimeKind.Local);
        var expectedUtc = local.ToUniversalTime();
        var automationId = Guid.NewGuid();

        await using (var db = _fixture.CreateContext())
        {
            db.ScheduledTriggerStates.Add(new ScheduledTriggerStateEntity
            {
                AutomationId = automationId,
                LastFiredUtc = local,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var loaded = await db.ScheduledTriggerStates.SingleAsync(s => s.AutomationId == automationId);
            loaded.LastFiredUtc.Kind.ShouldBe(DateTimeKind.Utc);
            loaded.LastFiredUtc.ShouldBe(expectedUtc);
        }
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
