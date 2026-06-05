using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Core.Triggers.Scheduling;

/// <summary>
/// Computes a deterministic per-automation offset within <c>[0, maxJitter]</c>.
/// Same automation id + parameters always yields the same offset so a restart
/// in the jitter window doesn't double-dispatch.
/// </summary>
internal static class ScheduledTriggerJitter
{
    public static TimeSpan ComputeOffset(Guid automationId, ScheduleTiming timing, TimeSpan maxJitter)
    {
        if (timing == ScheduleTiming.Precise || maxJitter <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        Span<byte> bytes = stackalloc byte[16];
        automationId.TryWriteBytes(bytes);
        var hash = BitConverter.ToUInt64(bytes[..8]);
        var fraction = hash / (double)ulong.MaxValue;
        return TimeSpan.FromTicks((long)(maxJitter.Ticks * fraction));
    }
}
