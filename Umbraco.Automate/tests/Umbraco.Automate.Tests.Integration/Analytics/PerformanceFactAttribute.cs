namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// A fact that is skipped unless <c>AUTOMATE_PERF_SQLSERVER</c> holds a SQL Server connection string,
/// so the speed tests never run in CI.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(SeededSqlServerAnalyticsDatabase.ConnectionString))
        {
            Skip = $"Set {SeededSqlServerAnalyticsDatabase.ConnectionStringVariable} to a SQL Server connection string to run the speed tests.";
        }
    }
}
