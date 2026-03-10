using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddWorkflowCoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateEvent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventData = table.Column<string>(type: "TEXT", nullable: true),
                    EventTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateEventSubscription",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkflowId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StepId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutionPointerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubscribeAsOf = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubscriptionData = table.Column<string>(type: "TEXT", nullable: true),
                    ExternalToken = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExternalWorkerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExternalTokenExpiry = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateEventSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateScheduledCommand",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    ExecuteTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateScheduledCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowInstance",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextExecution = table.Column<long>(type: "INTEGER", nullable: true),
                    CompleteTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkflowInstance", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEvent_EventName_EventKey",
                table: "umbracoAutomateEvent",
                columns: new[] { "EventName", "EventKey" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEvent_IsProcessed_EventTime",
                table: "umbracoAutomateEvent",
                columns: new[] { "IsProcessed", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEventSubscription_EventName_EventKey",
                table: "umbracoAutomateEventSubscription",
                columns: new[] { "EventName", "EventKey" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateScheduledCommand_ExecuteTime",
                table: "umbracoAutomateScheduledCommand",
                column: "ExecuteTime");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowInstance_NextExecution",
                table: "umbracoAutomateWorkflowInstance",
                column: "NextExecution");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowInstance_Status",
                table: "umbracoAutomateWorkflowInstance",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowInstance_Status_NextExecution",
                table: "umbracoAutomateWorkflowInstance",
                columns: new[] { "Status", "NextExecution" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateEvent");

            migrationBuilder.DropTable(
                name: "umbracoAutomateEventSubscription");

            migrationBuilder.DropTable(
                name: "umbracoAutomateScheduledCommand");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkflowInstance");
        }
    }
}
