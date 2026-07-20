using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Background service that starts the WorkflowCore <see cref="IWorkflowHost"/>
/// after Automate migrations have completed.
/// </summary>
internal sealed class WorkflowHostLifecycle : BackgroundService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly WorkflowDefinitionRecovery _definitionRecovery;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly ILogger<WorkflowHostLifecycle> _logger;

    public WorkflowHostLifecycle(
        IWorkflowHost workflowHost,
        WorkflowDefinitionRecovery definitionRecovery,
        AutomateReadinessSignal readinessSignal,
        ILogger<WorkflowHostLifecycle> logger)
    {
        _workflowHost = workflowHost;
        _definitionRecovery = definitionRecovery;
        _readinessSignal = readinessSignal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await _readinessSignal.WaitUntilReadyAsync(stoppingToken))
        {
            _logger.LogError(
                "Automate startup migrations failed; the WorkflowCore host will not start. " +
                "Resolve the migration failure and restart.");
            return;
        }

        // Re-register workflow definitions for in-flight instances before starting the host,
        // so WorkflowCore's poller can resume them without "not registered" errors.
        await _definitionRecovery.RecoverAsync(stoppingToken);

        _logger.LogInformation("Starting WorkflowCore host");
        _workflowHost.Start();

        // Keep alive until shutdown is requested.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }

        _logger.LogInformation("Stopping WorkflowCore host");
        _workflowHost.Stop();
    }
}
