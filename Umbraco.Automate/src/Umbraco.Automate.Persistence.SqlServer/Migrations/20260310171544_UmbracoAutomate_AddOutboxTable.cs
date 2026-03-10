using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextRetryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimedByInstance = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimedByGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
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
