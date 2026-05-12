using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_RemoveAutomationIsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "umbracoAutomateAutomation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "umbracoAutomateAutomation",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
