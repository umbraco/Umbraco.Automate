using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user is saved (covers both new users and edits —
/// Umbraco raises <see cref="UserSavedNotification"/> for both, with
/// <see cref="UserSavedTriggerOutput.IsNew"/> distinguishing them).
/// Produces one <see cref="TriggerEvent"/> per saved user.
/// </summary>
[Trigger("umbracoAutomate.userSaved", "User Saved",
    Description = "Fires when a back-office user is saved (created or updated).",
    Group = "Users",
    Icon = "icon-user",
    RequiredSections = [UmbracoConstants.Applications.Users])]
public sealed class UserSavedTrigger
    : NotificationTriggerBase<UserSavedTriggerSettings, UserSavedTriggerOutput, UserSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSavedTrigger"/> class.
    /// </summary>
    public UserSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserSavedNotification notification)
    {
        foreach (var user in notification.SavedEntities)
        {
            yield return new TriggerEvent<UserSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // IUser has no CMS VersionId — UpdateDate advances per save and is the
                // only stable distinguisher between legitimate sequential saves of the
                // same user.
                IdempotencyKey = IdempotencyKeyFactory.ForVersionlessEntitySaveEvent(Alias, user.Key, user.UpdateDate),
                Output = new UserSavedTriggerOutput
                {
                    UserKey = user.Key,
                    UserName = user.Name,
                    Username = user.Username,
                    Email = user.Email,
                    UserGroupKeys = user.Groups?.Select(g => g.Key).ToArray() ?? Array.Empty<Guid>(),
                    IsNew = user.CreateDate == user.UpdateDate,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(UserSavedTriggerOutput output, UserSavedTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
