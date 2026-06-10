using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Background service that periodically cleans up old entity version records
/// based on the configured cleanup policy.
/// </summary>
internal sealed class VersionCleanupBackgroundJob : RecurringHostedServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<VersionCleanupPolicy> _options;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly ILogger<VersionCleanupBackgroundJob> _logger;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    public VersionCleanupBackgroundJob(
        IServiceProvider serviceProvider,
        IOptionsMonitor<VersionCleanupPolicy> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        ILogger<VersionCleanupBackgroundJob> logger)
        : base(logger, CleanupInterval, StartupDelay)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _mainDom = mainDom;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(object? state)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogDebug("Version cleanup is disabled, skipping");
            return;
        }

        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("Version cleanup will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("Version cleanup will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("Version cleanup will not run if not MainDom");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var versionService = scope.ServiceProvider.GetRequiredService<IEntityVersionService>();

        try
        {
            var result = await versionService.CleanupVersionsAsync(CancellationToken.None);

            if (result.WasSkipped)
            {
                _logger.LogDebug("Version cleanup was skipped: {Reason}", result.SkipReason);
            }
            else if (result.TotalDeleted > 0)
            {
                _logger.LogInformation(
                    "Version cleanup completed. Deleted {TotalDeleted} versions ({DeletedByAge} by age, {DeletedByCount} by count). {RemainingVersions} remaining",
                    result.TotalDeleted, result.DeletedByAge, result.DeletedByCount, result.RemainingVersions);
            }
            else
            {
                _logger.LogDebug(
                    "Version cleanup completed. No old versions to delete. {RemainingVersions} remaining",
                    result.RemainingVersions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old entity versions");
            throw;
        }
    }
}
