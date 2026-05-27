using System.Net;
using System.Text;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Dispatches <see cref="AutomationHealthChangedNotification"/> to the automation's configured
/// channels when it transitions into Degraded (<see cref="NotifyOn.Warning"/>) or Disabled
/// (<see cref="NotifyOn.Disabled"/>). Reuses <see cref="ChannelNotifier"/> for the send mechanics.
/// </summary>
internal sealed class CircuitBreakerHealthNotificationDispatcher
    : INotificationAsyncHandler<AutomationHealthChangedNotification>
{
    private readonly ChannelNotifier _channelNotifier;
    private readonly IAutomationService _automationService;

    public CircuitBreakerHealthNotificationDispatcher(
        ChannelNotifier channelNotifier,
        IAutomationService automationService)
    {
        _channelNotifier = channelNotifier;
        _automationService = automationService;
    }

    public async Task HandleAsync(AutomationHealthChangedNotification notification, CancellationToken cancellationToken)
    {
        NotifyOn requiredFlag;
        switch (notification.State.Health)
        {
            case AutomationHealth.Disabled:
                requiredFlag = NotifyOn.Disabled;
                break;
            case AutomationHealth.Degraded:
                requiredFlag = NotifyOn.Warning;
                break;
            default:
                return; // Healthy (reset / recovery) is not dispatched to channels.
        }

        var automation = await _automationService.GetAutomationAsync(notification.State.AutomationId, cancellationToken);
        if (automation is null)
        {
            return;
        }

        var message = new NotificationMessage
        {
            Subject = $"[Umbraco Automate] {automation.Name} — {notification.State.Health}",
            HtmlBody = BuildHtmlBody(automation.Name, notification),
        };

        await _channelNotifier.DispatchAsync(
            automation,
            message,
            notifyOn => Task.FromResult(notifyOn.HasFlag(requiredFlag)),
            cancellationToken);
    }

    private static string BuildHtmlBody(string automationName, AutomationHealthChangedNotification notification)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Automation ");
        sb.Append(WebUtility.HtmlEncode(notification.State.Health.ToString()));
        sb.Append("</h2>");
        sb.Append("<p>");
        sb.Append(WebUtility.HtmlEncode(automationName));
        sb.Append("</p>");

        if (!string.IsNullOrWhiteSpace(notification.Reason))
        {
            sb.Append("<p>");
            sb.Append(WebUtility.HtmlEncode(notification.Reason));
            sb.Append("</p>");
        }

        return sb.ToString();
    }
}
