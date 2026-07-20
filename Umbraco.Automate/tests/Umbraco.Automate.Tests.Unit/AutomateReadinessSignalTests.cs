using Umbraco.Automate.Core;

namespace Umbraco.Automate.Tests.Unit;

public class AutomateReadinessSignalTests
{
    [Fact]
    public async Task WaitUntilReadyAsync_ReturnsTrue_WhenSignalled()
    {
        var signal = new AutomateReadinessSignal();
        signal.Signal();

        var ready = await signal.WaitUntilReadyAsync().WaitAsync(TimeSpan.FromSeconds(5));

        ready.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitUntilReadyAsync_ReturnsFalse_WhenMigrationsFailed()
    {
        // Background services must degrade gracefully rather than let AutomateNotReadyException
        // escape ExecuteAsync — an unhandled exception there stops the whole host.
        var signal = new AutomateReadinessSignal();
        signal.SignalFailed(new InvalidOperationException("Simulated migration failure"));

        var ready = await signal.WaitUntilReadyAsync().WaitAsync(TimeSpan.FromSeconds(5));

        ready.ShouldBeFalse();
    }

    [Fact]
    public void HasFailed_DistinguishesFailureFromPending()
    {
        var pending = new AutomateReadinessSignal();
        pending.HasFailed.ShouldBeFalse();
        pending.IsReady.ShouldBeFalse();

        var ready = new AutomateReadinessSignal();
        ready.Signal();
        ready.HasFailed.ShouldBeFalse();

        var failed = new AutomateReadinessSignal();
        failed.SignalFailed(new InvalidOperationException("boom"));
        failed.HasFailed.ShouldBeTrue();
        failed.IsReady.ShouldBeFalse();
    }

    [Fact]
    public async Task WaitAsync_Throws_WhenMigrationsFailed()
    {
        // The DB-write path (interceptor) must still fail loudly.
        var signal = new AutomateReadinessSignal();
        signal.SignalFailed(new InvalidOperationException("Simulated migration failure"));

        var exception = await Should.ThrowAsync<AutomateNotReadyException>(
            () => signal.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        exception.InnerException.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("Simulated migration failure");
    }
}
