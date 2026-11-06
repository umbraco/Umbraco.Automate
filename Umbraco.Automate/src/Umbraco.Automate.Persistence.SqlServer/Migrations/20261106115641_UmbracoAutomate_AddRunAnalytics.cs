using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.SqlServer.Migrations;

/// <inheritdoc />
public partial class UmbracoAutomate_AddRunAnalytics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "DurationMs",
            table: "umbracoAutomateRun",
            type: "bigint",
            nullable: true);

        // Backfill runs that finished before the column existed. Runs recovered after a restart
        // (Failed with the recovery's error text) are skipped: their elapsed time includes downtime.
        // Wrapped in EXEC so a generated one-batch script compiles after the column is added.
        migrationBuilder.Sql(
            "EXEC(N'UPDATE umbracoAutomateRun SET DurationMs = DATEDIFF_BIG(millisecond, StartedUtc, CompletedUtc) " +
            "WHERE StartedUtc IS NOT NULL AND CompletedUtc IS NOT NULL " +
            "AND (Status <> 3 OR Error IS NULL OR Error NOT LIKE N''Recovered after application restart%'')')");

        migrationBuilder.CreateIndex(
            name: "IX_umbracoAutomateStepRun_StartedUtc",
            table: "umbracoAutomateStepRun",
            column: "StartedUtc")
            .Annotation("SqlServer:Include", new[] { "RunId", "ActionAlias", "Status", "RetryCount", "DurationTicks", "ErrorCategory" });

        migrationBuilder.CreateIndex(
            name: "IX_umbracoAutomateRun_StartedUtc",
            table: "umbracoAutomateRun",
            column: "StartedUtc")
            .Annotation("SqlServer:Include", new[] { "AutomationId", "Status", "DurationMs" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_umbracoAutomateStepRun_StartedUtc",
            table: "umbracoAutomateStepRun");

        migrationBuilder.DropIndex(
            name: "IX_umbracoAutomateRun_StartedUtc",
            table: "umbracoAutomateRun");

        migrationBuilder.DropColumn(
            name: "DurationMs",
            table: "umbracoAutomateRun");
    }
}
