using Shouldly;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class IdempotencyKeyFactoryTests
{
    [Fact]
    public void SameContentAndVersion_ProducesSameKey()
    {
        // A duplicate notification for the exact same publish produces an identical key
        // — the outbox uses this to collapse the duplicate into a single message.
        var contentKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForContentEvent("test.trigger", contentKey, versionId: 42);
        var b = IdempotencyKeyFactory.ForContentEvent("test.trigger", contentKey, versionId: 42);
        a.ShouldBe(b);
    }

    [Fact]
    public void DifferentVersions_ProduceDifferentKeys()
    {
        // Two separate publishes of the same content increment the version id, so each
        // one gets its own key and neither is dedup'd away.
        var contentKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForContentEvent("test.trigger", contentKey, versionId: 1);
        var b = IdempotencyKeyFactory.ForContentEvent("test.trigger", contentKey, versionId: 2);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void DifferentAliases_ProduceDifferentKeys()
    {
        var contentKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForContentEvent("a.trigger", contentKey, 1);
        var b = IdempotencyKeyFactory.ForContentEvent("b.trigger", contentKey, 1);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void DifferentContentKeys_ProduceDifferentKeys()
    {
        var a = IdempotencyKeyFactory.ForContentEvent("test.trigger", Guid.NewGuid(), 1);
        var b = IdempotencyKeyFactory.ForContentEvent("test.trigger", Guid.NewGuid(), 1);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void KeyFormat_IsStable()
    {
        var contentKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = IdempotencyKeyFactory.ForContentEvent("test.trigger", contentKey, 42);
        key.ShouldBe("test.trigger:11111111-1111-1111-1111-111111111111:v42");
    }

    [Fact]
    public void ForContentBatch_EmptyBatch_ReturnsNull()
        => IdempotencyKeyFactory.ForContentBatch("test.batch", []).ShouldBeNull();

    [Fact]
    public void ForContentBatch_SameItems_ProducesSameKey()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1), (b, 2)]);
        var key2 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1), (b, 2)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForContentBatch_OrderInsensitive()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1), (b, 2)]);
        var key2 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(b, 2), (a, 1)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForContentBatch_DifferentVersions_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 2)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForContentBatch_DifferentMembership_ProducesDifferentKeys()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForContentBatch("test.batch", [(a, 1), (b, 1)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForContentBatch_DifferentAliases_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForContentBatch("a.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForContentBatch("b.batch", [(a, 1)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForContentBatch_KeyFormatHasExpectedShape()
    {
        var contentKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = IdempotencyKeyFactory.ForContentBatch("test.batch", [(contentKey, 42)]);

        // Format: {alias}:batch:{base64url-sha256-no-padding} → 17 prefix + 43 hash chars.
        key.ShouldNotBeNull();
        key.ShouldStartWith("test.batch:batch:");
        var hashPart = key["test.batch:batch:".Length..];
        hashPart.Length.ShouldBe(43);
        hashPart.ShouldNotContain('=');
        hashPart.ShouldNotContain('+');
        hashPart.ShouldNotContain('/');
    }

    [Fact]
    public void ForContentSaveEvent_SameVersionAndUpdateDate_ProducesSameKey()
    {
        // A duplicate save notification carries the same VersionId and UpdateDate,
        // so the keys match and the outbox collapses the duplicate.
        var contentKey = Guid.NewGuid();
        var updateDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var a = IdempotencyKeyFactory.ForContentSaveEvent("test.trigger", contentKey, versionId: 5, updateDate);
        var b = IdempotencyKeyFactory.ForContentSaveEvent("test.trigger", contentKey, versionId: 5, updateDate);
        a.ShouldBe(b);
    }

    [Fact]
    public void ForContentSaveEvent_SameVersionDifferentUpdateDate_ProducesDifferentKeys()
    {
        // Sequential draft saves share the VersionId but bump UpdateDate — each must
        // produce a distinct key so the second save isn't dedup'd against the first.
        var contentKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForContentSaveEvent("test.trigger", contentKey, 5, new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var b = IdempotencyKeyFactory.ForContentSaveEvent("test.trigger", contentKey, 5, new DateTime(2026, 4, 20, 10, 0, 1, DateTimeKind.Utc));
        a.ShouldNotBe(b);
    }

    [Fact]
    public void ForContentSaveEvent_KeyFormat_IsStable()
    {
        var contentKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var updateDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var key = IdempotencyKeyFactory.ForContentSaveEvent("test.trigger", contentKey, 42, updateDate);
        key.ShouldBe($"test.trigger:11111111-1111-1111-1111-111111111111:v42:u{updateDate.Ticks}");
    }

    [Fact]
    public void ForContentSaveBatch_EmptyBatch_ReturnsNull()
        => IdempotencyKeyFactory.ForContentSaveBatch("test.batch", []).ShouldBeNull();

    [Fact]
    public void ForContentSaveBatch_SameItems_ProducesSameKey()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 5, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);
        var key2 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForContentSaveBatch_SameVersionsDifferentUpdateDates_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 1, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(a, 1, t1)]);
        var key2 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(a, 1, t2)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForContentSaveBatch_OrderInsensitive()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 5, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);
        var key2 = IdempotencyKeyFactory.ForContentSaveBatch("test.batch", [(b, 2, t2), (a, 1, t1)]);

        key1.ShouldBe(key2);
    }
}
