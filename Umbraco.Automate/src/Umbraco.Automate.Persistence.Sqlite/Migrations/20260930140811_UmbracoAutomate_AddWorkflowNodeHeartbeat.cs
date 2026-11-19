using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddWorkflowNodeHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowNodeHeartbeat",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Beat = table.Column<long>(type: "INTEGER", nullable: false),
                    HeartbeatUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkflowNodeHeartbeat", x => x.NodeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkflowNodeHeartbeat");
        }
    }
}
