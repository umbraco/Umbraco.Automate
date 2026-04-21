using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Background service that periodically removes completed outbox messages older than
/// <see cref="OutboxOptions.CompletedRetention"/>.
/// </summary>
internal sealed class OutboxCleanupBackgroundJob : RecurringHostedServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<OutboxOptions> _options;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly ILogger<OutboxCleanupBackgroundJob> _logger;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    public OutboxCleanupBackgroundJob(
        IServiceProvider serviceProvider,
        IOptionsMonitor<OutboxOptions> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        ILogger<OutboxCleanupBackgroundJob> logger)
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
        var retention = _options.CurrentValue.CompletedRetention;

        if (retention <= TimeSpan.Zero)
        {
            _logger.LogDebug("Outbox cleanup is disabled (CompletedRetention <= 0), skipping");
            return;
        }

        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("Outbox cleanup will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("Outbox cleanup will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("Outbox cleanup will not run if not MainDom");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        try
        {
            await store.CleanupCompletedAsync(retention, CancellationToken.None);
            _logger.LogDebug("Outbox cleanup completed (retention: {Retention})", retention);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup completed outbox messages");
            throw;
        }
    }
}
