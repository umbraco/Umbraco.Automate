using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Automate.Core.Migrations;

/// <summary>
/// Migration to add the Automate section to the Admin user group.
/// </summary>
public class AddAutomateSectionToAdminGroup : AsyncMigrationBase
{
    private readonly IUserGroupService _userGroupService;
    private readonly IScopeProvider _scopeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAutomateSectionToAdminGroup"/> class.
    /// </summary>
    public AddAutomateSectionToAdminGroup(
        IMigrationContext context,
        IScopeProvider scopeProvider,
        IUserGroupService userGroupService)
        : base(context)
    {
        _scopeProvider = scopeProvider;
        _userGroupService = userGroupService;
    }

    /// <inheritdoc />
    protected override async Task MigrateAsync()
    {
        using IScope scope = _scopeProvider.CreateScope();

        IUserGroup? adminUserGroup = await _userGroupService.GetAsync(
            Cms.Core.Constants.Security.AdminGroupAlias);

        if (adminUserGroup is not null)
        {
            if (adminUserGroup.AllowedSections.Contains(Constants.Sections.Automate))
            {
                Logger.LogDebug("The Umbraco Automate section has been assigned to the Admin group already");
            }
            else
            {
                adminUserGroup.AddAllowedSection(Constants.Sections.Automate);
                await _userGroupService.UpdateAsync(adminUserGroup, Cms.Core.Constants.Security.SuperUserKey);
            }
        }

        scope.Complete();
    }
}
