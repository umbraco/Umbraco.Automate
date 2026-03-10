using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateAutomation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Alias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    DraftVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Definition = table.Column<string>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DateModified = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateAutomation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutomationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutomationVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggerData = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateStepRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionAlias = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InputData = table.Column<string>(type: "TEXT", nullable: true),
                    OutputData = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCategory = table.Column<int>(type: "INTEGER", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    DurationTicks = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateStepRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomation_Alias",
                table: "umbracoAutomateAutomation",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomation_GroupId",
                table: "umbracoAutomateAutomation",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomation_Status",
                table: "umbracoAutomateAutomation",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateRun_AutomationId",
                table: "umbracoAutomateRun",
                column: "AutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateRun_AutomationId_StartedUtc",
                table: "umbracoAutomateRun",
                columns: new[] { "AutomationId", "StartedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateRun_Status",
                table: "umbracoAutomateRun",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateStepRun_RunId",
                table: "umbracoAutomateStepRun",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateStepRun_RunId_StepId",
                table: "umbracoAutomateStepRun",
                columns: new[] { "RunId", "StepId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateAutomation");

            migrationBuilder.DropTable(
                name: "umbracoAutomateRun");

            migrationBuilder.DropTable(
                name: "umbracoAutomateStepRun");
        }
    }
}
