using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_NormalizeWorkflowExecutionPointers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "umbracoAutomateWorkflowInstance",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowExecutionPointer",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkflowInstanceId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PointerId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    SleepUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PredecessorId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EventPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Children = table.Column<string>(type: "TEXT", nullable: true),
                    Scope = table.Column<string>(type: "TEXT", nullable: true),
                    PersistenceData = table.Column<string>(type: "TEXT", nullable: true),
                    ContextItem = table.Column<string>(type: "TEXT", nullable: true),
                    EventData = table.Column<string>(type: "TEXT", nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", nullable: true),
                    ExtensionAttributes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkflowExecutionPointer", x => x.PersistenceId);
                    table.ForeignKey(
                        name: "FK_umbracoAutomateWorkflowExecutionPointer_umbracoAutomateWorkflowInstance_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "umbracoAutomateWorkflowInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowExecutionPointer_WorkflowInstanceId",
                table: "umbracoAutomateWorkflowExecutionPointer",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowExecutionPointer_WorkflowInstanceId_Active",
                table: "umbracoAutomateWorkflowExecutionPointer",
                columns: new[] { "WorkflowInstanceId", "Active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkflowExecutionPointer");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "umbracoAutomateWorkflowInstance");
        }
    }
}
