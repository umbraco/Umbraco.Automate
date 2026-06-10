using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user's password is changed.
/// </summary>
[Trigger("umbracoAutomate.userPasswordChanged", "User Password Changed",
    Description = "Fires when a back-office user's password is changed.",
    Group = "Users",
    Icon = "icon-key",
    RequiredSections = [UmbracoConstants.Applications.Users])]
public sealed class UserPasswordChangedTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserPasswordChangedNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<UserPasswordChangedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPasswordChangedTrigger"/> class.
    /// </summary>
    public UserPasswordChangedTrigger(
        TriggerInfrastructure infrastructure,
        IUserService userService,
        ILogger<UserPasswordChangedTrigger> logger)
        : base(infrastructure)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserPasswordChangedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification,
            UserGroupResolver.Resolve(_userService, notification.AffectedUserId, _logger));

    /// <inheritdoc />
    protected override bool CanHandle(UserAuthTriggerOutput output, UserAuthTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
