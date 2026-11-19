using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
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
                    NodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Beat = table.Column<long>(type: "bigint", nullable: false),
                    HeartbeatUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
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
