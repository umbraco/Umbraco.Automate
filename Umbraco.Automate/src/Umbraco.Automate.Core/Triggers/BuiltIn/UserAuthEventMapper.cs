using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Shared per-trigger mapping for user auth notifications. Each auth trigger raises one
/// event per notification with the same shape, so the projection is centralised here to
/// keep the trigger classes themselves to a single concern.
/// </summary>
internal static class UserAuthEventMapper
{
    public static IEnumerable<TriggerEvent> Map(string triggerAlias, UserNotification notification)
    {
        yield return new TriggerEvent<UserAuthTriggerOutput>
        {
            TriggerAlias = triggerAlias,
            InitiatorType = TriggerInitiatorType.System,
            IdempotencyKey = IdempotencyKeyFactory.ForUserAuthEvent(triggerAlias, notification.AffectedUserId, notification.DateTimeUtc),
            Output = new UserAuthTriggerOutput
            {
                AffectedUserId = notification.AffectedUserId,
                PerformingUserId = notification.PerformingUserId,
                IpAddress = notification.IpAddress,
                DateTimeUtc = notification.DateTimeUtc,
            },
        };
    }
}
