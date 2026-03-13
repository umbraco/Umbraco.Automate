using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Hosted service that starts and stops the WorkflowCore <see cref="IWorkflowHost"/>
/// after Umbraco has finished booting.
/// </summary>
/// <remarks>
/// WorkflowCore registers <see cref="IWorkflowHost"/> as a singleton but does not register it
/// as an <see cref="IHostedService"/>. This service bridges the gap by starting the host
/// (which starts its internal poll threads for workflow and event queue consumption)
/// once the Umbraco runtime reaches <see cref="RuntimeLevel.Run"/>.
/// </remarks>
internal sealed class WorkflowHostLifecycle : BackgroundService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<WorkflowHostLifecycle> _logger;

    public WorkflowHostLifecycle(
        IWorkflowHost workflowHost,
        IRuntimeState runtimeState,
        ILogger<WorkflowHostLifecycle> logger)
    {
        _workflowHost = workflowHost;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for Umbraco to finish booting (migrations must complete first).
        while (_runtimeState.Level != RuntimeLevel.Run && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested) return;

        // Suppress execution context flow so WorkflowCore's background threads
        // do not inherit Umbraco's ambient scope context. Without this, the threads
        // share and corrupt each other's scopes.
        _logger.LogInformation("Starting WorkflowCore host");
        using (ExecutionContext.SuppressFlow())
        {
            await _workflowHost.StartAsync(stoppingToken);
        }

        _logger.LogInformation("WorkflowCore host started");

        // Keep alive until shutdown.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        _logger.LogInformation("Stopping WorkflowCore host");
        await _workflowHost.StopAsync(CancellationToken.None);
    }
}
