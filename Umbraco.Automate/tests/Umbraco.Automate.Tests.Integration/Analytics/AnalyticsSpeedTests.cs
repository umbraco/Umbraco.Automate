// Story AN-1: docs/plans/analytics/STORIES.md
// Compiled out until T4. The T4 builder removes the #if/#endif lines and writes the seeding.
#if AUTOMATE_ANALYTICS
using System.Diagnostics;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// The speed gate. Opt-in: runs only when <c>AUTOMATE_PERF_SQLSERVER</c> holds a SQL Server
/// connection string (LocalDB is fine), so CI never runs it. The fixture creates the database,
/// applies the SQL Server migrations, and bulk-loads 1,000,000 runs and about 4,000,000 step runs
/// over the last 180 days with <c>SqlBulkCopy</c>. Each spec warms up once, then times the second
/// call for a 90-day range plus the 90 days before, all workspaces.
/// </summary>
[Trait("Category", "Performance")]
public class AnalyticsSpeedTests : IClassFixture<SeededSqlServerAnalyticsDatabase>
{
    private static readonly TimeSpan Target = TimeSpan.FromSeconds(2);
    private readonly SeededSqlServerAnalyticsDatabase _db;

    public AnalyticsSpeedTests(SeededSqlServerAnalyticsDatabase db) => _db = db;

    [Fact(Skip = "Pending")]
    public async Task TheSummaryQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunSummaryQueriesAsync);

        took.ShouldBeLessThan(Target);
    }

    [Fact(Skip = "Pending")]
    public async Task TheAutomationQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunAutomationQueriesAsync);

        took.ShouldBeLessThan(Target);
    }

    [Fact(Skip = "Pending")]
    public async Task TheActionQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunActionQueriesAsync);

        took.ShouldBeLessThan(Target);
    }
}
#endif
