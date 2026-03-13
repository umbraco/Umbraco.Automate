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
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "umbracoAutomateAutomationGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateAutomationGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateConnection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateConnection", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "umbracoAutomateEvent",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateEventSubscription",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StepId = table.Column<int>(type: "int", nullable: false),
                    ExecutionPointerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubscribeAsOf = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubscriptionData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalWorkerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateEventSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateOutbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Topic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "umbracoAutomateRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AutomationVersion = table.Column<int>(type: "int", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceAccountKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                name: "umbracoAutomateScheduledCommand",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommandName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecuteTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateScheduledCommand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateScheduledTriggerState",
                columns: table => new
                {
                    AutomationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastFiredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateScheduledTriggerState", x => x.AutomationId);
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

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowInstance",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextExecution = table.Column<long>(type: "bigint", nullable: true),
                    CompleteTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkflowInstance", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ServiceAccountKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspace", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspaceConnection",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspaceConnection", x => new { x.WorkspaceId, x.ConnectionId });
                    table.ForeignKey(
                        name: "FK_umbracoAutomateWorkspaceConnection_umbracoAutomateWorkspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "umbracoAutomateWorkspace",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkspaceUserGroup",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkspaceUserGroup", x => new { x.WorkspaceId, x.UserGroupId });
                    table.ForeignKey(
                        name: "FK_umbracoAutomateWorkspaceUserGroup_umbracoAutomateWorkspace_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "umbracoAutomateWorkspace",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_umbracoAutomateAutomation_WorkspaceId",
                table: "umbracoAutomateAutomation",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomationGroup_ParentId",
                table: "umbracoAutomateAutomationGroup",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateAutomationGroup_WorkspaceId",
                table: "umbracoAutomateAutomationGroup",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateConnection_Alias",
                table: "umbracoAutomateConnection",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEntityVersion_EntityId_EntityType",
                table: "umbracoAutomateEntityVersion",
                columns: new[] { "EntityId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateEntityVersion_EntityId_EntityType_Version",
                table: "umbracoAutomateEntityVersion",
                columns: new[] { "EntityId", "EntityType", "Version" },
                unique: true);

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
                name: "IX_umbracoAutomateOutbox_Status_CreatedUtc",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateOutbox_Topic_IdempotencyKey",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Topic", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateOutbox_Topic_Status_NextRetryUtc",
                table: "umbracoAutomateOutbox",
                columns: new[] { "Topic", "Status", "NextRetryUtc" });

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
                name: "IX_umbracoAutomateScheduledCommand_ExecuteTime",
                table: "umbracoAutomateScheduledCommand",
                column: "ExecuteTime");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateStepRun_RunId",
                table: "umbracoAutomateStepRun",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateStepRun_RunId_StepId",
                table: "umbracoAutomateStepRun",
                columns: new[] { "RunId", "StepId" });

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

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkspace_Alias",
                table: "umbracoAutomateWorkspace",
                column: "Alias",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateAutomation");

            migrationBuilder.DropTable(
                name: "umbracoAutomateAutomationGroup");

            migrationBuilder.DropTable(
                name: "umbracoAutomateConnection");

            migrationBuilder.DropTable(
                name: "umbracoAutomateEntityVersion");

            migrationBuilder.DropTable(
                name: "umbracoAutomateEvent");

            migrationBuilder.DropTable(
                name: "umbracoAutomateEventSubscription");

            migrationBuilder.DropTable(
                name: "umbracoAutomateOutbox");

            migrationBuilder.DropTable(
                name: "umbracoAutomateRun");

            migrationBuilder.DropTable(
                name: "umbracoAutomateScheduledCommand");

            migrationBuilder.DropTable(
                name: "umbracoAutomateScheduledTriggerState");

            migrationBuilder.DropTable(
                name: "umbracoAutomateStepRun");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkflowInstance");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspaceConnection");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspaceUserGroup");

            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkspace");
        }
    }
}
