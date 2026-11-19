namespace Umbraco.Automate.Tests.Common;

/// <summary>
/// Shared deadlines for the polling waits that integration tests use to observe a run, step run,
/// or workflow reach a status. The values are deliberately generous because CI agents can be slow
/// and under load, and because the waits themselves are polled: a passing test returns as soon as
/// the condition is met, so a large ceiling costs nothing on the happy path and only bites when a
/// test has genuinely failed.
/// </summary>
public static class TestTimeouts
{
    /// <summary>
    /// The default deadline for waiting on a run, step run, or workflow to reach a status.
    /// Generous on purpose so a slow CI agent does not trip a deadline that a fast one would
    /// clear easily. The wait still returns immediately once the condition is met.
    /// </summary>
    public static readonly TimeSpan WorkflowWait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The longer deadline used by the run-cancellation tests, which need extra headroom to get
    /// a run into active execution before terminating it and then to observe the workflow leave
    /// its running state. Still polled, so a passing test finishes as soon as it can.
    /// </summary>
    public static readonly TimeSpan Cancellation = TimeSpan.FromSeconds(60);

    /// <summary>
    /// WorkflowCore's poll interval for fixtures that publish events (approval decisions). In
    /// WorkflowCore 3.9.0 an event published while its subscription is being created can be
    /// dropped from the queue ("Event locked") and is only picked up again by the poller, so with
    /// the default 10 seconds a test stalls for 10 seconds or more under load. Use it with
    /// <see cref="Fixtures.NoGreyList"/>, or a dropped event can wait out the five-minute grey-list
    /// instead of the next poll. Fixed upstream in
    /// WorkflowCore 3.22.0 (danielgerlag/workflow-core#1455); remove this once Automate upgrades.
    /// </summary>
    public static readonly TimeSpan EventPollInterval = TimeSpan.FromMilliseconds(500);
}
