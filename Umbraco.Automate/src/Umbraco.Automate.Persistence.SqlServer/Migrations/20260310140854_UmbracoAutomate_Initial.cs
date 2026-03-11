using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishedVersion = table.Column<int>(type: "int", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Definition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateAutomation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TriggerData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateStepRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionAlias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InputData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCategory = table.Column<int>(type: "int", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: true)
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
