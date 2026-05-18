using Shouldly;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class IdempotencyKeyFactoryTests
{
    [Fact]
    public void SameEntityAndVersion_ProducesSameKey()
    {
        // A duplicate notification for the exact same publish produces an identical key
        // — the outbox uses this to collapse the duplicate into a single message.
        var entityKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForEntityEvent("test.trigger", entityKey, versionId: 42);
        var b = IdempotencyKeyFactory.ForEntityEvent("test.trigger", entityKey, versionId: 42);
        a.ShouldBe(b);
    }

    [Fact]
    public void DifferentVersions_ProduceDifferentKeys()
    {
        // Two separate publishes of the same entity increment the version id, so each
        // one gets its own key and neither is dedup'd away.
        var entityKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForEntityEvent("test.trigger", entityKey, versionId: 1);
        var b = IdempotencyKeyFactory.ForEntityEvent("test.trigger", entityKey, versionId: 2);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void DifferentAliases_ProduceDifferentKeys()
    {
        var entityKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForEntityEvent("a.trigger", entityKey, 1);
        var b = IdempotencyKeyFactory.ForEntityEvent("b.trigger", entityKey, 1);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void DifferentEntityKeys_ProduceDifferentKeys()
    {
        var a = IdempotencyKeyFactory.ForEntityEvent("test.trigger", Guid.NewGuid(), 1);
        var b = IdempotencyKeyFactory.ForEntityEvent("test.trigger", Guid.NewGuid(), 1);
        a.ShouldNotBe(b);
    }

    [Fact]
    public void KeyFormat_IsStable()
    {
        var entityKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = IdempotencyKeyFactory.ForEntityEvent("test.trigger", entityKey, 42);
        key.ShouldBe("test.trigger:11111111-1111-1111-1111-111111111111:v42");
    }

    [Fact]
    public void ForEntityBatch_EmptyBatch_ReturnsNull()
        => IdempotencyKeyFactory.ForEntityBatch("test.batch", []).ShouldBeNull();

    [Fact]
    public void ForEntityBatch_SameItems_ProducesSameKey()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1), (b, 2)]);
        var key2 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1), (b, 2)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForEntityBatch_OrderInsensitive()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1), (b, 2)]);
        var key2 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(b, 2), (a, 1)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForEntityBatch_DifferentVersions_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 2)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForEntityBatch_DifferentMembership_ProducesDifferentKeys()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(a, 1), (b, 1)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForEntityBatch_DifferentAliases_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();

        var key1 = IdempotencyKeyFactory.ForEntityBatch("a.batch", [(a, 1)]);
        var key2 = IdempotencyKeyFactory.ForEntityBatch("b.batch", [(a, 1)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForEntityBatch_KeyFormatHasExpectedShape()
    {
        var entityKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = IdempotencyKeyFactory.ForEntityBatch("test.batch", [(entityKey, 42)]);

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
    public void ForEntitySaveEvent_SameVersionAndUpdateDate_ProducesSameKey()
    {
        // A duplicate save notification carries the same VersionId and UpdateDate,
        // so the keys match and the outbox collapses the duplicate.
        var entityKey = Guid.NewGuid();
        var updateDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var a = IdempotencyKeyFactory.ForEntitySaveEvent("test.trigger", entityKey, versionId: 5, updateDate);
        var b = IdempotencyKeyFactory.ForEntitySaveEvent("test.trigger", entityKey, versionId: 5, updateDate);
        a.ShouldBe(b);
    }

    [Fact]
    public void ForEntitySaveEvent_SameVersionDifferentUpdateDate_ProducesDifferentKeys()
    {
        // Sequential draft saves share the VersionId but bump UpdateDate — each must
        // produce a distinct key so the second save isn't dedup'd against the first.
        var entityKey = Guid.NewGuid();
        var a = IdempotencyKeyFactory.ForEntitySaveEvent("test.trigger", entityKey, 5, new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        var b = IdempotencyKeyFactory.ForEntitySaveEvent("test.trigger", entityKey, 5, new DateTime(2026, 4, 20, 10, 0, 1, DateTimeKind.Utc));
        a.ShouldNotBe(b);
    }

    [Fact]
    public void ForEntitySaveEvent_KeyFormat_IsStable()
    {
        var entityKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var updateDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var key = IdempotencyKeyFactory.ForEntitySaveEvent("test.trigger", entityKey, 42, updateDate);
        key.ShouldBe($"test.trigger:11111111-1111-1111-1111-111111111111:v42:u{updateDate.Ticks}");
    }

    [Fact]
    public void ForEntitySaveBatch_EmptyBatch_ReturnsNull()
        => IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", []).ShouldBeNull();

    [Fact]
    public void ForEntitySaveBatch_SameItems_ProducesSameKey()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 5, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);
        var key2 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);

        key1.ShouldBe(key2);
    }

    [Fact]
    public void ForEntitySaveBatch_SameVersionsDifferentUpdateDates_ProduceDifferentKeys()
    {
        var a = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 1, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(a, 1, t1)]);
        var key2 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(a, 1, t2)]);

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void ForEntitySaveBatch_OrderInsensitive()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var t1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 4, 20, 10, 0, 5, DateTimeKind.Utc);

        var key1 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(a, 1, t1), (b, 2, t2)]);
        var key2 = IdempotencyKeyFactory.ForEntitySaveBatch("test.batch", [(b, 2, t2), (a, 1, t1)]);

        key1.ShouldBe(key2);
    }
}
