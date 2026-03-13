using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddScheduledTriggerState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateScheduledTriggerState",
                columns: table => new
                {
                    AutomationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastFiredUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateScheduledTriggerState", x => x.AutomationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateScheduledTriggerState");
        }
    }
}
