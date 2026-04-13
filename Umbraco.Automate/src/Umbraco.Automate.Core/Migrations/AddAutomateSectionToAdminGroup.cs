using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Automate.Core.Migrations;

/// <summary>
/// Migration to add the Automate section to the Admin user group.
/// </summary>
/// <remarks>
/// Uses direct database access instead of <c>IUserGroupService</c> because the service layer
/// performs authorization checks and publishes notifications that may fail during migration context.
/// </remarks>
public class AddAutomateSectionToAdminGroup : AsyncMigrationBase
{
    private const string UserGroup2AppTable = Umbraco.Cms.Core.Constants.DatabaseSchema.Tables.UserGroup2App;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddAutomateSectionToAdminGroup"/> class.
    /// </summary>
    public AddAutomateSectionToAdminGroup(IMigrationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override Task MigrateAsync()
    {
        var quotedTable = SqlSyntax.GetQuotedTableName(UserGroup2AppTable);

        // Check if the Automate section is already assigned to the admin group (userGroupId = 1)
        var exists = Database.ExecuteScalar<int>(
            Sql($"SELECT COUNT(*) FROM {quotedTable} WHERE userGroupId = @0 AND app = @1", 1, Constants.Sections.Automate));

        if (exists > 0)
        {
            Logger.LogDebug("The Umbraco Automate section has been assigned to the Admin group already");
            return Task.CompletedTask;
        }

        Database.Execute(
            Sql($"INSERT INTO {quotedTable} (userGroupId, app) VALUES (@0, @1)", 1, Constants.Sections.Automate));

        Logger.LogInformation("The Umbraco Automate section has been assigned to the Admin group");

        return Task.CompletedTask;
    }
}
