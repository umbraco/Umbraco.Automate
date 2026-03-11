using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddWorkspacesAndAutomationWorkspaceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                table: "umbracoAutomateAutomation",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ServiceAccountKey = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspace", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspaceConnection",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspaceConnection", x => new { x.WorkspaceId, x.ConnectionId });
                    table.ForeignKey(
                        name: "FK_umbracoAutomateWorkspaceConnection_umbracoAutomateWorkspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "umbracoAutomateWorkspace",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspaceUserGroup",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspaceUserGroup", x => new { x.WorkspaceId, x.UserGroupId });
                    table.ForeignKey(
                        name: "FK_umbracoAutomateWorkspaceUserGroup_umbracoAutomateWorkspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "umbracoAutomateWorkspace",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomation_WorkspaceId",
                table: "umbracoAutomateAutomation",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkspace_Alias",
                table: "umbracoAutomateWorkspace",
                column: "Alias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspaceConnection");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspaceUserGroup");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspace");

            migrationBuilder.DropIndex(
                name: "IX_umbracoAutomateAutomation_WorkspaceId",
                table: "umbracoAutomateAutomation");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                table: "umbracoAutomateAutomation");
        }
    }
}
