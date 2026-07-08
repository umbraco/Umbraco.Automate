using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
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
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowExecutionPointer",
                columns: table => new
                {
                    PersistenceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PointerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StepId = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    SleepUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PredecessorId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventPublished = table.Column<bool>(type: "bit", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PersistenceData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContextItem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtensionAttributes = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
