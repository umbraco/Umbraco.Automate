using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class UmbracoAutomate_AddWorkflowLockAndPointerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "umbracoAutomateWorkflowLock",
                columns: table => new
                {
                    LockId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquiredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoAutomateWorkflowLock", x => x.LockId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoAutomateWorkflowExecutionPointer_WorkflowInstanceId_PointerId",
                table: "umbracoAutomateWorkflowExecutionPointer",
                columns: new[] { "WorkflowInstanceId", "PointerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoAutomateWorkflowLock");

            migrationBuilder.DropIndex(
                name: "IX_umbracoAutomateWorkflowExecutionPointer_WorkflowInstanceId_PointerId",
                table: "umbracoAutomateWorkflowExecutionPointer");
        }
    }
}
