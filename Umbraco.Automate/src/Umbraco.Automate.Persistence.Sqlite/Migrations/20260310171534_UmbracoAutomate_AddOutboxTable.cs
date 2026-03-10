using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddOutboxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateTransportMessage");

            migrationBuilder.CreateTable(
                name: "umbracoAutomateOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    NextRetryUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimedByInstance = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateOutbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateOutbox_Status_CreatedUtc",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateOutbox_Topic_Status_NextRetryUtc",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Topic", "Status", "NextRetryUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateOutbox");

            migrationBuilder.CreateTable(
                name: "umbracoAutomateTransportMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimedByGroup = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Headers = table.Column<string>(type: "TEXT", nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateTransportMessage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateTransportMessage_CreatedUtc",
                table: "umbracoAutomateTransportMessage",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateTransportMessage_Topic_ClaimedByGroup",
                table: "umbracoAutomateTransportMessage",
                columns: new[] { "Topic", "ClaimedByGroup" });
        }
    }
}
