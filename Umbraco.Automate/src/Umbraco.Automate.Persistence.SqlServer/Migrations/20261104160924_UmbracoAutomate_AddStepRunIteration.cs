using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddStepRunIteration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IterationIndex",
                table: "umbracoAutomateStepRun",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IterationTotal",
                table: "umbracoAutomateStepRun",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IterationIndex",
                table: "umbracoAutomateStepRun");

            migrationBuilder.DropColumn(
                name: "IterationTotal",
                table: "umbracoAutomateStepRun");
        }
    }
}
