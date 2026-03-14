using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Handles <see cref="AutomationRunCompletedNotification"/> and dispatches to configured
/// notification channels based on run status and <see cref="NotifyOn"/> settings.
/// </summary>
internal sealed class RunCompletedNotificationDispatcher
    : INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    private readonly NotificationChannelCollection _channels;
    private readonly IAutomationService _automationService;
    private readonly IAutomationRunService _runService;
    private readonly ILogger<RunCompletedNotificationDispatcher> _logger;

    public RunCompletedNotificationDispatcher(
        NotificationChannelCollection channels,
        IAutomationService automationService,
        IAutomationRunService runService,
        ILogger<RunCompletedNotificationDispatcher> logger)
    {
        _channels = channels;
        _automationService = automationService;
        _runService = runService;
        _logger = logger;
    }

    public async Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken cancellationToken)
    {
        var run = notification.Run;

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

        if (!await ShouldNotifyAsync(run, settings.NotifyOn, cancellationToken))
        {
            return;
        }

        var runData = new RunNotification
        {
            AutomationId = automation.Id,
            AutomationName = automation.Name,
            RunId = run.Id,
            Status = run.Status,
            Error = run.Error,
            WorkspaceId = run.WorkspaceId,
            CompletedUtc = run.CompletedUtc,
        };

        var message = new NotificationMessage
        {
            Subject = BuildSubject(automation.Name, run.Status),
            HtmlBody = BuildHtmlBody(runData),
            Data = runData,
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

                await channel.NotifyAsync(message, resolvedSettings, cancellationToken);
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

    private async Task<bool> ShouldNotifyAsync(
        AutomationRun run,
        NotifyOn notifyOn,
        CancellationToken cancellationToken)
    {
        if (notifyOn == NotifyOn.Never)
        {
            return false;
        }

        return run.Status switch
        {
            AutomationRunStatus.Failed => notifyOn.HasFlag(NotifyOn.Failed),
            AutomationRunStatus.Suspended => notifyOn.HasFlag(NotifyOn.Suspended),
            AutomationRunStatus.Completed => await ShouldNotifyCompletedAsync(run, notifyOn, cancellationToken),
            _ => false,
        };
    }

    private async Task<bool> ShouldNotifyCompletedAsync(
        AutomationRun run,
        NotifyOn notifyOn,
        CancellationToken cancellationToken)
    {
        if (notifyOn.HasFlag(NotifyOn.Completed))
        {
            return true;
        }

        if (!notifyOn.HasFlag(NotifyOn.Recovered))
        {
            return false;
        }

        // Check if the previous terminal run was a failure or suspension.
        var previousStatus = await _runService.GetPreviousTerminalRunStatusAsync(
            run.AutomationId, run.Id, cancellationToken);

        return previousStatus is AutomationRunStatus.Failed or AutomationRunStatus.Suspended;
    }

    private static string BuildSubject(string automationName, AutomationRunStatus status)
        => $"[Umbraco Automate] {automationName} \u2014 {status}";

    private static string BuildHtmlBody(RunNotification notification)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Automation Run ");
        sb.Append(notification.Status);
        sb.Append("</h2>");
        sb.Append("<table style=\"border-collapse:collapse;\">");
        AppendRow(sb, "Automation", notification.AutomationName);
        AppendRow(sb, "Run ID", notification.RunId.ToString());
        AppendRow(sb, "Status", notification.Status.ToString());

        if (!string.IsNullOrWhiteSpace(notification.Error))
        {
            AppendRow(sb, "Error", notification.Error);
        }

        if (notification.CompletedUtc.HasValue)
        {
            AppendRow(sb, "Completed (UTC)", notification.CompletedUtc.Value.ToString("u"));
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append("<tr><td style=\"padding:4px 12px 4px 0;font-weight:bold;vertical-align:top;\">");
        sb.Append(System.Net.WebUtility.HtmlEncode(label));
        sb.Append("</td><td style=\"padding:4px 0;\">");
        sb.Append(System.Net.WebUtility.HtmlEncode(value));
        sb.Append("</td></tr>");
    }
}
