namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Storage abstraction for <see cref="WorkflowLockProvider"/>'s lease rows. Mirrors the
/// <c>IOutbox</c>/<c>IOutboxStore</c> split: this interface lives in Core so <c>AddWorkflow</c>'s
/// setup callback (which must run in Core, at the single call site WorkflowCore reads its
/// <c>IDistributedLockProvider</c> factory from) can depend on it without Core taking a reference
/// on the Persistence project. The EF Core implementation lives in Persistence.
/// </summary>
internal interface IWorkflowLockStore
{
    /// <summary>
    /// Attempts to acquire or steal the lease for <paramref name="lockId"/>: succeeds if no lease
    /// row exists yet, or the existing one has expired.
    /// </summary>
    Task<bool> TryAcquireAsync(
        string lockId, Guid ownerToken, DateTime nowUtc, DateTime expiresUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the lease for <paramref name="lockId"/> as free, but only if still held by
    /// <paramref name="ownerToken"/> (a no-op if it has already been stolen by another node).
    /// </summary>
    Task ReleaseAsync(string lockId, Guid ownerToken, CancellationToken cancellationToken);

    /// <summary>
    /// Pushes the expiry forward to <paramref name="expiresUtc"/> for every id in
    /// <paramref name="lockIds"/> still held by <paramref name="ownerToken"/>, so an in-use lease
    /// doesn't lapse mid-step.
    /// </summary>
    Task RenewAsync(
        IReadOnlyCollection<string> lockIds, Guid ownerToken, DateTime expiresUtc, CancellationToken cancellationToken);
}
