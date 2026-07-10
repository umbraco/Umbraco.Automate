namespace Umbraco.Automate.Core;

/// <summary>
/// Lightweight signal that background services can await before accessing the database.
/// Set by the migration notification handler once EF Core migrations have completed.
/// </summary>
public sealed class AutomateReadinessSignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets whether the database is ready.
    /// </summary>
    public bool IsReady => _tcs.Task.IsCompletedSuccessfully;

    /// <summary>
    /// Signals that the Automate database is ready for use.
    /// </summary>
    public void Signal() => _tcs.TrySetResult();

    /// <summary>
    /// Signals that startup migrations failed, so the database will never become ready.
    /// Callers waiting via <see cref="WaitAsync"/> fail fast with <see cref="AutomateNotReadyException"/>
    /// instead of waiting indefinitely for a signal that will never arrive.
    /// </summary>
    public void SignalFailed(Exception migrationFailure)
        => _tcs.TrySetException(new AutomateNotReadyException(migrationFailure));

    /// <summary>
    /// Waits until the database is ready, or until the cancellation token is triggered.
    /// Throws <see cref="AutomateNotReadyException"/> if startup migrations failed.
    /// </summary>
    public Task WaitAsync(CancellationToken cancellationToken = default)
        => _tcs.Task.WaitAsync(cancellationToken);
}
