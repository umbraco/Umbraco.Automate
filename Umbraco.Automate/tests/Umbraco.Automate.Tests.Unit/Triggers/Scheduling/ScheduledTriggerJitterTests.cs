using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Scheduling;

namespace Umbraco.Automate.Tests.Unit.Triggers.Scheduling;

public class ScheduledTriggerJitterTests
{
    private static readonly TimeSpan MaxJitter = TimeSpan.FromMinutes(5);

    [Fact]
    public void ComputeOffset_IsDeterministic_ForSameId()
    {
        var id = Guid.NewGuid();

        var first = ScheduledTriggerJitter.ComputeOffset(id, ScheduleTiming.Flexible, MaxJitter);
        var second = ScheduledTriggerJitter.ComputeOffset(id, ScheduleTiming.Flexible, MaxJitter);

        second.ShouldBe(first);
    }

    [Fact]
    public void ComputeOffset_DiffersAcrossIds()
    {
        // With 200 random ids spread across [0, 5min), we expect a healthy distribution
        // rather than all bunched at zero.
        var offsets = Enumerable.Range(0, 200)
            .Select(_ => ScheduledTriggerJitter.ComputeOffset(Guid.NewGuid(), ScheduleTiming.Flexible, MaxJitter))
            .ToList();

        offsets.Distinct().Count().ShouldBeGreaterThan(150);
    }

    [Fact]
    public void ComputeOffset_IsZero_WhenPrecise()
    {
        var offset = ScheduledTriggerJitter.ComputeOffset(Guid.NewGuid(), ScheduleTiming.Precise, MaxJitter);

        offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeOffset_IsZero_WhenMaxJitterIsZero()
    {
        var offset = ScheduledTriggerJitter.ComputeOffset(Guid.NewGuid(), ScheduleTiming.Flexible, TimeSpan.Zero);

        offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeOffset_IsZero_WhenMaxJitterIsNegative()
    {
        var offset = ScheduledTriggerJitter.ComputeOffset(Guid.NewGuid(), ScheduleTiming.Flexible, TimeSpan.FromSeconds(-1));

        offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeOffset_StaysWithinBounds()
    {
        foreach (var _ in Enumerable.Range(0, 500))
        {
            var offset = ScheduledTriggerJitter.ComputeOffset(Guid.NewGuid(), ScheduleTiming.Flexible, MaxJitter);

            offset.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
            offset.ShouldBeLessThanOrEqualTo(MaxJitter);
        }
    }
}
