using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Background service that periodically cleans up old automation run records
/// based on the configured cleanup policy.
/// </summary>
internal sealed class RunCleanupBackgroundJob : RecurringHostedServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<RunCleanupPolicy> _options;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly RunCleanupTracker _tracker;
    private readonly ILogger<RunCleanupBackgroundJob> _logger;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(15);

    public RunCleanupBackgroundJob(
        IServiceProvider serviceProvider,
        IOptionsMonitor<RunCleanupPolicy> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        RunCleanupTracker tracker,
        ILogger<RunCleanupBackgroundJob> logger)
        : base(logger, CleanupInterval, StartupDelay)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _mainDom = mainDom;
        _tracker = tracker;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(object? state)
    {
        var policy = _options.CurrentValue;

        if (!policy.Enabled)
        {
            _logger.LogDebug("Run cleanup is disabled, skipping");
            return;
        }

        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("Run cleanup will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("Run cleanup will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("Run cleanup will not run if not MainDom");
            return;
        }

        if (policy is { RetentionDays: <= 0, MaxRunsPerAutomation: <= 0 })
        {
            _logger.LogDebug("No run cleanup policy configured, skipping");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAutomationRunRepository>();

        try
        {
            var deletedByAge = 0;
            var deletedByCount = 0;

            if (policy.RetentionDays > 0)
            {
                var threshold = DateTime.UtcNow.AddDays(-policy.RetentionDays);
                deletedByAge = await repository.DeleteRunsOlderThanAsync(threshold, CancellationToken.None);
            }

            if (policy.MaxRunsPerAutomation > 0)
            {
                deletedByCount = await repository.DeleteExcessRunsAsync(policy.MaxRunsPerAutomation, CancellationToken.None);
            }

            var totalDeleted = deletedByAge + deletedByCount;

            if (totalDeleted > 0)
            {
                _logger.LogInformation(
                    "Run cleanup completed. Deleted {TotalDeleted} runs ({DeletedByAge} by age, {DeletedByCount} by count)",
                    totalDeleted, deletedByAge, deletedByCount);
            }
            else
            {
                _logger.LogDebug("Run cleanup completed. No old runs to delete");
            }

            _tracker.RecordSuccess();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old automation runs");
            throw;
        }
    }
}
