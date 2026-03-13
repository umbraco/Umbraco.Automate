namespace Umbraco.Automate.Core;

/// <summary>
/// Lightweight signal that background services can await before accessing the database.
/// Set by the migration notification handler once EF Core migrations have completed.
/// </summary>
public sealed class AutomateReadinessSignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Signals that the Automate database is ready for use.
    /// </summary>
    public void Signal() => _tcs.TrySetResult();

    /// <summary>
    /// Waits until the database is ready, or until the cancellation token is triggered.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
        => _tcs.Task.WaitAsync(cancellationToken);
}
