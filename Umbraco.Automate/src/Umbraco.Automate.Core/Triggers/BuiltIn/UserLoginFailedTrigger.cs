using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office login attempt fails (wrong credentials, locked account, etc.).
/// Useful for security automations such as alerting on repeated failures.
/// </summary>
[Trigger("umbracoAutomate.userLoginFailed", "User Login Failed",
    Description = "Fires when a back-office login attempt fails.",
    Group = "Users",
    Icon = "icon-lock")]
public sealed class UserLoginFailedTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserLoginFailedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserLoginFailedTrigger"/> class.
    /// </summary>
    public UserLoginFailedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLoginFailedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification);
}
