using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Automate.Persistence.Sqlite.Migrations;

/// <inheritdoc />
public partial class UmbracoAutomate_AddRunAnalytics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "DurationMs",
            table: "umbracoAutomateRun",
            type: "INTEGER",
            nullable: true);

        // Backfill runs that finished before the column existed. Runs recovered after a restart
        // (Failed with the recovery's error text) are skipped: their elapsed time includes downtime.
        migrationBuilder.Sql(
            "UPDATE umbracoAutomateRun SET DurationMs = CAST(ROUND((julianday(CompletedUtc) - julianday(StartedUtc)) * 86400000) AS INTEGER) " +
            "WHERE StartedUtc IS NOT NULL AND CompletedUtc IS NOT NULL " +
            "AND (Status <> 3 OR Error IS NULL OR Error NOT LIKE 'Recovered after application restart%')");

        migrationBuilder.CreateIndex(
            name: "IX_umbracoAutomateStepRun_StartedUtc",
            table: "umbracoAutomateStepRun",
            column: "StartedUtc");

        migrationBuilder.CreateIndex(
            name: "IX_umbracoAutomateRun_StartedUtc",
            table: "umbracoAutomateRun",
            column: "StartedUtc");
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
