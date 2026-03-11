using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateConnection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Settings = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateConnection", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateConnection_Alias",
                table: "umbracoAutomateConnection",
                column: "Alias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateConnection");
        }
    }
}
