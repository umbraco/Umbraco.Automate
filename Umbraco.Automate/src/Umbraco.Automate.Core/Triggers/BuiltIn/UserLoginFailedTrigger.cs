using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office login attempt fails (wrong credentials, locked account, etc.).
/// Useful for security automations such as alerting on repeated failures.
/// </summary>
[Trigger("umbracoAutomate.userLoginFailed", "User Login Failed",
    Description = "Fires when a back-office login attempt fails.",
    Group = "Users",
    Icon = "icon-lock",
    RequiredSections = [UmbracoConstants.Applications.Users])]
public sealed class UserLoginFailedTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserLoginFailedNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<UserLoginFailedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLoginFailedTrigger"/> class.
    /// </summary>
    public UserLoginFailedTrigger(
        TriggerInfrastructure infrastructure,
        IUserService userService,
        ILogger<UserLoginFailedTrigger> logger)
        : base(infrastructure)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLoginFailedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification,
            UserGroupResolver.Resolve(_userService, notification.AffectedUserId, _logger));

    /// <inheritdoc />
    protected override bool CanHandle(UserAuthTriggerOutput output, UserAuthTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
