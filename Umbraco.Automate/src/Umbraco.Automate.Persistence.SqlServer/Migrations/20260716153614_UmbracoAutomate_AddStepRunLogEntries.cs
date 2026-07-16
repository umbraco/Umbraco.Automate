using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddStepRunLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogEntries",
                table: "umbracoAutomateStepRun",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogEntries",
                table: "umbracoAutomateStepRun");
        }
    }
}
