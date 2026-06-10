using Umbraco.Automate.Core.Execution;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Checks that this node is eligible to consume queued automation work. In the default
/// <c>SchedulerOnly</c> execution mode only a <c>Single</c> or elected <c>SchedulingPublisher</c>
/// node processes the outbox, so an un-elected node (most commonly one whose <c>ServerRole</c>
/// never leaves <c>Unknown</c>) silently leaves trigger messages pending forever. Severity is
/// driven by consequence: ineligible with work already queued is an error; ineligible with
/// nothing queued yet is a warning.
/// </summary>
[HealthCheck(
    "C7D8E9F0-1A2B-4C3D-9E4F-5A6B7C8D9E0F",
    "Automation Execution Eligibility",
    Description = "Checks that this node is eligible to process queued automation work.",
    Group = "Umbraco Automate")]
public sealed class ExecutionEligibilityHealthCheck : HealthCheck
{
    private readonly IExecutionNodeEligibility _eligibility;
    private readonly IAutomateHealthService _healthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionEligibilityHealthCheck"/> class.
    /// </summary>
    public ExecutionEligibilityHealthCheck(
        IExecutionNodeEligibility eligibility,
        IAutomateHealthService healthService)
    {
        _eligibility = eligibility;
        _healthService = healthService;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
    {
        if (_eligibility.CanExecuteWorkflows(out var reason))
        {
            return [new HealthCheckStatus("This node is eligible to process automation work.")
            {
                ResultType = StatusResultType.Success,
            }];
        }

        var stats = await _healthService.GetQueueStatsAsync(CancellationToken.None);
        var stranded = stats.Pending + stats.Processing;

        // Pending work with nobody eligible to claim it is a silent dead-end → Error.
        // No work queued yet is still a misconfiguration, but not yet harmful → Warning.
        var resultType = stranded > 0 ? StatusResultType.Error : StatusResultType.Warning;
        var prefix = stranded > 0
            ? $"{stranded} automation message(s) are queued but cannot be processed on this node. "
            : "This node is not currently eligible to process automation work. ";

        return [new HealthCheckStatus(prefix + reason) { ResultType = resultType }];
    }
}
