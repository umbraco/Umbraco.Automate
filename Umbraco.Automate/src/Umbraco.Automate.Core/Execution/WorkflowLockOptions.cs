using Microsoft.Extensions.Options;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Configuration options for the EF Core-backed WorkflowCore distributed lock provider.
/// Bound to <c>Umbraco:Automate:WorkflowLock</c> in appsettings.json.
/// </summary>
public sealed class WorkflowLockOptions
{
    /// <summary>
    /// How long an acquired lease is valid before another node may steal it. Bounds how long a
    /// crashed holder can block other nodes from processing the same workflow instance, and how long
    /// a starting node waits for another node's heartbeat before treating that node as gone.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often a held lease's expiry is pushed forward, so a lock doesn't lapse mid-use during
    /// a slow step, and how often a node writes its heartbeat. Must be shorter than
    /// <see cref="LeaseDuration"/>.
    /// </summary>
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Rejects <see cref="WorkflowLockOptions"/> that would let a live node's lease lapse, or its
/// heartbeat look stopped, between two renewals.
/// </summary>
internal sealed class WorkflowLockOptionsValidator : IValidateOptions<WorkflowLockOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkflowLockOptions options)
    {
        var failures = new List<string>();

        if (options.LeaseDuration <= TimeSpan.Zero)
        {
            failures.Add("Umbraco:Automate:WorkflowLock:LeaseDuration must be greater than zero.");
        }

        if (options.RenewalInterval <= TimeSpan.Zero)
        {
            failures.Add("Umbraco:Automate:WorkflowLock:RenewalInterval must be greater than zero.");
        }

        if (options.RenewalInterval >= options.LeaseDuration)
        {
            failures.Add(
                $"Umbraco:Automate:WorkflowLock:RenewalInterval ({options.RenewalInterval}) must be shorter than " +
                $"LeaseDuration ({options.LeaseDuration}), otherwise leases lapse between renewals.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
