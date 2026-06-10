using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user is deleted.
/// Produces one <see cref="TriggerEvent"/> per deleted user.
/// </summary>
[Trigger("umbracoAutomate.userDeleted", "User Deleted",
    Description = "Fires when a back-office user is deleted.",
    Group = "Users",
    Icon = "icon-delete",
    RequiredSections = [UmbracoConstants.Applications.Users])]
public sealed class UserDeletedTrigger
    : NotificationTriggerBase<UserDeletedTriggerSettings, UserDeletedTriggerOutput, UserDeletedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedTrigger"/> class.
    /// </summary>
    public UserDeletedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserDeletedNotification notification)
    {
        foreach (var user in notification.DeletedEntities)
        {
            yield return new TriggerEvent<UserDeletedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // IUser has no VersionId, but delete is terminal — the entity key alone
                // makes a duplicate notification dedupe to the same key.
                IdempotencyKey = IdempotencyKeyFactory.ForVersionlessEntityEvent(Alias, user.Key),
                Output = new UserDeletedTriggerOutput
                {
                    UserKey = user.Key,
                    UserName = user.Name,
                    Username = user.Username,
                    Email = user.Email,
                    UserGroupKeys = user.Groups?.Select(g => g.Key).ToArray() ?? Array.Empty<Guid>(),
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(UserDeletedTriggerOutput output, UserDeletedTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
