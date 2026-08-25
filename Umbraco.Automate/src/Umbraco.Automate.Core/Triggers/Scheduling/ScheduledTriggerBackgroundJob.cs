using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Core.Triggers.Scheduling;

/// <summary>
/// Background service that polls published automations with scheduled triggers
/// and dispatches them when their CRON schedule is due.
/// </summary>
internal sealed class ScheduledTriggerBackgroundJob : RecurringHostedServiceBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<ScheduledTriggerOptions> _options;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly ILogger<ScheduledTriggerBackgroundJob> _logger;

    public ScheduledTriggerBackgroundJob(
        IServiceProvider serviceProvider,
        IOptionsMonitor<ScheduledTriggerOptions> options,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        ILogger<ScheduledTriggerBackgroundJob> logger)
        : base(logger, options.CurrentValue.PollInterval, options.CurrentValue.StartupDelay)
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
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("Scheduled trigger job will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("Scheduled trigger job will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("Scheduled trigger job will not run if not MainDom");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
        var triggerCollection = scope.ServiceProvider.GetRequiredService<TriggerCollection>();
        var stateStore = scope.ServiceProvider.GetRequiredService<IScheduledTriggerStateStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ITriggerDispatcher>();

        var publishedRefs = await automationService.GetPublishedVersionReferencesAsync(CancellationToken.None);
        var now = DateTime.UtcNow;

        foreach (var (automationId, _) in publishedRefs)
        {
            try
            {
                var automation = await automationService.GetAutomationAsync(automationId, CancellationToken.None);
                if (automation?.Trigger is null)
                {
                    continue;
                }

                var trigger = triggerCollection.GetByAlias(automation.Trigger.TriggerAlias);
                if (trigger is not IScheduledTrigger scheduledTrigger)
                {
                    continue;
                }

                // Resolve trigger settings to pass to GetCronExpression.
                object? triggerSettings = null;
                if (trigger.SettingsType is not null && automation.Trigger.Settings.Count > 0)
                {
                    triggerSettings = trigger.ResolveSettings(automation.Trigger.Settings);
                }

                var cronExpression = scheduledTrigger.GetCronExpression(triggerSettings);
                if (string.IsNullOrWhiteSpace(cronExpression))
                {
                    continue;
                }

                CronExpression cron;
                try
                {
                    cron = CronExpression.Parse(cronExpression);
                }
                catch (CronFormatException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Automation {AutomationId} has invalid CRON expression '{CronExpression}', skipping",
                        automationId, cronExpression);
                    continue;
                }

                var timeZone = scheduledTrigger.GetTimeZone(triggerSettings);

                var lastFired = await stateStore.GetLastFiredAsync(automationId, CancellationToken.None);
                // For first-fire (lastFired is null), the lookback must span the jitter window
                // plus a poll's worth of slack so a tick we observed but skipped (because the
                // jittered dueAt was in the future) is still in scope on the next poll.
                var lookback = _options.CurrentValue.MaxJitter + _options.CurrentValue.PollInterval;
                var baseTime = lastFired ?? now - lookback;
                var nextOccurrence = cron.GetNextOccurrence(baseTime, timeZone, inclusive: false);

                if (nextOccurrence is null)
                {
                    continue;
                }

                var timing = ResolveTiming(triggerSettings);
                var offset = ScheduledTriggerJitter.ComputeOffset(automationId, timing, _options.CurrentValue.MaxJitter);

                // Don't let jitter push us past the next cron occurrence.
                if (offset > TimeSpan.Zero)
                {
                    var nextAfter = cron.GetNextOccurrence(nextOccurrence.Value, timeZone, inclusive: false);
                    if (nextAfter is not null)
                    {
                        var maxOffset = nextAfter.Value - nextOccurrence.Value - TimeSpan.FromSeconds(1);
                        if (offset > maxOffset)
                        {
                            offset = maxOffset > TimeSpan.Zero ? maxOffset : TimeSpan.Zero;
                        }
                    }
                }

                var dueAt = nextOccurrence.Value + offset;
                if (dueAt > now)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Dispatching scheduled trigger for automation {AutomationId} (CRON: {CronExpression}, TimeZone: {TimeZoneId}, Timing: {Timing}, Offset: {Offset})",
                    automationId, cronExpression, timeZone.Id, timing, offset);

                var triggerEvent = new TriggerEvent<ScheduledTriggerOutput>
                {
                    TriggerAlias = automation.Trigger.TriggerAlias,
                    InitiatorType = TriggerInitiatorType.Scheduled,
                    // This loop already resolved the schedule for one specific automation, so the
                    // event is addressed to it. Without a target, TriggerEventHandler fans the
                    // event out to every published automation sharing the trigger alias — each of
                    // N scheduled automations due on the same tick would then run N times, and an
                    // automation would also run on another automation's tick, since the fan-out
                    // never re-checks the recipient's own CRON.
                    TargetAutomationId = automationId,
                    IdempotencyKey = $"scheduled:{automationId}:{nextOccurrence:O}",
                    Output = new ScheduledTriggerOutput
                    {
                        FiredAtUtc = dueAt,
                        CronExpression = cronExpression,
                    },
                };

                await dispatcher.DispatchAsync(triggerEvent, CancellationToken.None);
                await stateStore.SetLastFiredAsync(automationId, now, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate scheduled trigger for automation {AutomationId}", automationId);
            }
        }
    }

    private static ScheduleTiming ResolveTiming(object? triggerSettings)
    {
        if (triggerSettings is ScheduledTriggerSettings sts
            && Enum.TryParse<ScheduleTiming>(sts.Timing, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return ScheduleTiming.Flexible;
    }
}
