// Story AN-1: docs/plans/analytics/STORIES.md
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public AnalyticsSpeedTests(SeededSqlServerAnalyticsDatabase db, ITestOutputHelper output)
    {
        _db = db;
        _output = output;
    }

    [PerformanceFact]
    public async Task TheSummaryQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunSummaryQueriesAsync);

        _output.WriteLine($"Summary queries (all workspaces): {took.TotalMilliseconds:N0} ms");
        took.ShouldBeLessThan(Target);
    }

    [PerformanceFact]
    public async Task TheAutomationQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunAutomationQueriesAsync);

        _output.WriteLine($"Automation queries (all workspaces): {took.TotalMilliseconds:N0} ms");
        took.ShouldBeLessThan(Target);
    }

    [PerformanceFact]
    public async Task TheActionQueriesAnswerInUnderTwoSeconds()
    {
        TimeSpan took = await _db.TimeSecondCallAsync(_db.RunActionQueriesAsync);

        _output.WriteLine($"Action queries (all workspaces): {took.TotalMilliseconds:N0} ms");
        took.ShouldBeLessThan(Target);
    }

    [PerformanceFact]
    public async Task OneWorkspaceTimingsAreReportedForReference()
    {
        TimeSpan summary = await _db.TimeSecondCallAsync(() => _db.RunSummaryQueriesAsync(_db.FirstWorkspaceScope));
        TimeSpan automations = await _db.TimeSecondCallAsync(() => _db.RunAutomationQueriesAsync(_db.FirstWorkspaceScope));
        TimeSpan actions = await _db.TimeSecondCallAsync(() => _db.RunActionQueriesAsync(_db.FirstWorkspaceScope));

        _output.WriteLine(
            $"One workspace: summary {summary.TotalMilliseconds:N0} ms, automations {automations.TotalMilliseconds:N0} ms, actions {actions.TotalMilliseconds:N0} ms");
    }
}
