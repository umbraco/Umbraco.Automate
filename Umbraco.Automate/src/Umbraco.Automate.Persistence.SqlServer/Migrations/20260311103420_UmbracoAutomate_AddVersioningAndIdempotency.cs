using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddVersioningAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "umbracoAutomateOutbox",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "umbracoAutomateEntityVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateEntityVersion", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateOutbox_Topic_IdempotencyKey",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Topic", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEntityVersion_EntityId_EntityType",
                table: "umbracoAutomateEntityVersion",
                columns: new[] { "EntityId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEntityVersion_EntityId_EntityType_Version",
                table: "umbracoAutomateEntityVersion",
                columns: new[] { "EntityId", "EntityType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateEntityVersion");

            migrationBuilder.DropIndex(
                name: "IX_umbracoAutomateOutbox_Topic_IdempotencyKey",
                table: "umbracoAutomateOutbox");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "umbracoAutomateOutbox");
        }
    }
}
