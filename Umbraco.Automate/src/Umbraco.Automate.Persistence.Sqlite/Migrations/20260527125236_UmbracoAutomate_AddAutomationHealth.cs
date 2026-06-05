using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddAutomationHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateAutomationHealth",
                columns: table => new
                {
                    AutomationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Health = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningIssuedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DisabledUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateAutomationHealth", x => x.AutomationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomationHealth_Health",
                table: "umbracoAutomateAutomationHealth",
                column: "Health");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateAutomationHealth");
        }
    }
}
