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
}
