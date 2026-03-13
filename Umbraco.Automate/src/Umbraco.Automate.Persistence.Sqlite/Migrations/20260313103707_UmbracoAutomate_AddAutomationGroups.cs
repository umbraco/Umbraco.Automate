using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddAutomationGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateAutomationGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkspaceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateAutomationGroup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomationGroup_ParentId",
                table: "umbracoAutomateAutomationGroup",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomationGroup_WorkspaceId",
                table: "umbracoAutomateAutomationGroup",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateAutomationGroup");
        }
    }
}
