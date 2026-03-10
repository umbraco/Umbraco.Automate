using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddTransportMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateTransportMessage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClaimedByGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateTransportMessage");
        }
    }
}
