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
}
