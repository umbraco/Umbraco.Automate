using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Verifies that the WorkflowCore host is running and the outbox dispatcher is processing messages.
/// </summary>
[HealthCheck(
    "7A2F8D1E-4B3C-4E5A-9F6D-8C7B2A1E3D4F",
    "Automation Engine Status",
    Description = "Checks that the automation engine and message queue are running.",
    Group = "Umbraco Automate")]
public sealed class EngineStatusHealthCheck : HealthCheck
{
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly IAutomateHealthService _healthService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineStatusHealthCheck"/> class.
    /// </summary>
    public EngineStatusHealthCheck(AutomateReadinessSignal readinessSignal, IAutomateHealthService healthService)
    {
        _readinessSignal = readinessSignal;
        _healthService = healthService;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
    {
        if (!_readinessSignal.IsReady)
        {
            return [new HealthCheckStatus("The automation engine has not started. Migrations may still be running.")
            {
                ResultType = StatusResultType.Error,
            }];
        }

        try
        {
            await _healthService.GetQueueStatsAsync(CancellationToken.None);
            return [new HealthCheckStatus("The automation engine is running and the message queue is reachable.")
            {
                ResultType = StatusResultType.Success,
            }];
        }
        catch (Exception ex)
        {
            return [new HealthCheckStatus($"The message queue is unreachable: {ex.Message}")
            {
                ResultType = StatusResultType.Error,
            }];
        }
    }
}
