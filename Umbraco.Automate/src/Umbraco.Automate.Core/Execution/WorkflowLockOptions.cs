namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Configuration options for the EF Core-backed WorkflowCore distributed lock provider.
/// Bound to <c>Umbraco:Automate:WorkflowLock</c> in appsettings.json.
/// </summary>
public sealed class WorkflowLockOptions
{
    /// <summary>
    /// How long an acquired lease is valid before another node may steal it. Bounds how long a
    /// crashed holder can block other nodes from processing the same workflow instance.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often a held lease's expiry is pushed forward, so a lock doesn't lapse mid-use during
    /// a slow step. Should be comfortably shorter than <see cref="LeaseDuration"/>.
    /// </summary>
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
}
