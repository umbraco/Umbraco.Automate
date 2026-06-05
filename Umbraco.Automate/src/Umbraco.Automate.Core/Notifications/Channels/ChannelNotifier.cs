using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Shared dispatch of a <see cref="NotificationMessage"/> to an automation's configured
/// notification channels. Encapsulates the per-channel mechanics — enabled check, <see cref="NotifyOn"/>
/// filtering, channel resolution, settings resolution, send, and per-channel error isolation — so
/// multiple notification handlers reuse the same behaviour.
/// </summary>
internal sealed class ChannelNotifier
{
    private readonly NotificationChannelCollection _channels;
    private readonly ILogger<ChannelNotifier> _logger;

    public ChannelNotifier(NotificationChannelCollection channels, ILogger<ChannelNotifier> logger)
    {
        _channels = channels;
        _logger = logger;
    }

    /// <summary>
    /// Sends <paramref name="message"/> to each enabled channel on <paramref name="automation"/>
    /// whose <see cref="NotifyOn"/> satisfies <paramref name="shouldNotify"/>. A failing channel is
    /// logged and skipped; it does not stop the others.
    /// </summary>
    public async Task DispatchAsync(
        Automation automation,
        NotificationMessage message,
        Func<NotifyOn, Task<bool>> shouldNotify,
        CancellationToken cancellationToken)
    {
        var settings = automation.NotificationSettings;
        if (settings is null || settings.Channels.Count == 0)
        {
            return;
        }

        foreach (var channelConfig in settings.Channels)
        {
            if (!channelConfig.IsEnabled || channelConfig.NotifyOn == NotifyOn.Never)
            {
                continue;
            }

            if (!await shouldNotify(channelConfig.NotifyOn))
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
                    "Failed to send notification via channel '{ChannelAlias}' for automation {AutomationId}",
                    channelConfig.ChannelAlias, automation.Id);
            }
        }
    }
}
