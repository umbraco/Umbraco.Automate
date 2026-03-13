using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Handles <see cref="AutomationRunCompletedNotification"/> and dispatches to configured
/// notification channels when runs fail or suspend.
/// </summary>
internal sealed class RunCompletedNotificationDispatcher
    : INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    private readonly NotificationChannelCollection _channels;
    private readonly IAutomationService _automationService;
    private readonly ILogger<RunCompletedNotificationDispatcher> _logger;

    public RunCompletedNotificationDispatcher(
        NotificationChannelCollection channels,
        IAutomationService automationService,
        ILogger<RunCompletedNotificationDispatcher> logger)
    {
        _channels = channels;
        _automationService = automationService;
        _logger = logger;
    }

    public async Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken cancellationToken)
    {
        var run = notification.Target;

        if (!ShouldNotify(run.Status))
        {
            return;
        }

        var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
        if (automation is null)
        {
            return;
        }

        var settings = automation.NotificationSettings;
        if (settings is null || settings.Channels.Count == 0)
        {
            return;
        }

        if (!MatchesNotifyOn(run.Status, settings.NotifyOn))
        {
            return;
        }

        var payload = new RunFailureNotification
        {
            AutomationId = automation.Id,
            AutomationName = automation.Name,
            RunId = run.Id,
            Status = run.Status,
            Error = run.Error,
            WorkspaceId = run.WorkspaceId,
            CompletedUtc = run.CompletedUtc,
        };

        foreach (var channelConfig in settings.Channels)
        {
            if (!channelConfig.IsEnabled)
            {
                continue;
            }

            var channel = _channels.GetByAlias(channelConfig.ChannelAlias);
            if (channel is null)
            {
                _logger.LogWarning(
                    "Notification channel '{ChannelAlias}' not found for automation {AutomationId}",
                    channelConfig.ChannelAlias, automation.Id);
                continue;
            }

            try
            {
                var resolvedSettings = channelConfig.Settings.Count > 0
                    ? channel.ResolveSettings(channelConfig.Settings)
                    : null;

                await channel.NotifyAsync(payload, resolvedSettings, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send notification via channel '{ChannelAlias}' for run {RunId}",
                    channelConfig.ChannelAlias, run.Id);
            }
        }
    }

    private static bool ShouldNotify(AutomationRunStatus status)
        => status is AutomationRunStatus.Failed or AutomationRunStatus.Suspended;

    private static bool MatchesNotifyOn(AutomationRunStatus status, NotifyOn notifyOn)
        => status switch
        {
            AutomationRunStatus.Failed => notifyOn.HasFlag(NotifyOn.Failed),
            AutomationRunStatus.Suspended => notifyOn.HasFlag(NotifyOn.Suspended),
            _ => false,
        };
}
