using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.OpenIddict.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomateOpenIddict_AddUserAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserAccessToken",
                table: "umbracoAutomateOpenIddictCredentials",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserAccessToken",
                table: "umbracoAutomateOpenIddictCredentials");
        }
    }
}
