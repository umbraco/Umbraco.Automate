using System.Text;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Handles <see cref="AutomationRunResumedNotification"/> and dispatches to configured channels
/// whose <see cref="NotifyOn"/> includes <see cref="NotifyOn.Resumed"/>. Reuses the shared
/// <see cref="ChannelNotifier"/> for channel resolution + per-channel error isolation.
/// </summary>
internal sealed class RunResumedNotificationDispatcher
    : INotificationAsyncHandler<AutomationRunResumedNotification>
{
    private readonly ChannelNotifier _channelNotifier;
    private readonly IAutomationService _automationService;

    public RunResumedNotificationDispatcher(
        ChannelNotifier channelNotifier,
        IAutomationService automationService)
    {
        _channelNotifier = channelNotifier;
        _automationService = automationService;
    }

    public async Task HandleAsync(AutomationRunResumedNotification notification, CancellationToken cancellationToken)
    {
        var run = notification.Run;
        var automation = await _automationService.GetAutomationAsync(run.AutomationId, cancellationToken);
        if (automation is null)
        {
            return;
        }

        var message = new NotificationMessage
        {
            Subject = $"[Umbraco Automate] {automation.Name} — Resumed",
            HtmlBody = BuildHtmlBody(automation.Name, run.Id),
        };

        await _channelNotifier.DispatchAsync(
            automation,
            message,
            notifyOn => Task.FromResult(notifyOn.HasFlag(NotifyOn.Resumed)),
            cancellationToken);
    }

    private static string BuildHtmlBody(string automationName, Guid runId)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Automation Run Resumed</h2>");
        sb.Append("<p>");
        sb.Append(System.Net.WebUtility.HtmlEncode(automationName));
        sb.Append(" has resumed from suspension (run ");
        sb.Append(runId);
        sb.Append(").</p>");
        return sb.ToString();
    }
}
