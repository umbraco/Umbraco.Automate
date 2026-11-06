using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Analytics;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// A SQL Server database, named by <c>AUTOMATE_PERF_SQLSERVER</c>, with the Automate migrations
/// applied and 1,000,000 runs plus about 4,000,000 step runs loaded over the last 180 days. The
/// load is skipped when the database already holds it. A database that holds anything other than
/// this seed's rows is refused rather than cleared, so point the variable at a dedicated database.
/// </summary>
public sealed class SeededSqlServerAnalyticsDatabase : IAsyncLifetime
{
    internal const string ConnectionStringVariable = "AUTOMATE_PERF_SQLSERVER";

    internal static readonly string? ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

    private const int RunCount = 1_000_000;
    private const int DayCount = 180;
    private const int AutomationCount = 40;
    private const int WorkspaceCount = 3;
    private const int ChunkSize = 50_000;
    private const string SeedMarker = "perf-seed";
    private const string SeedAliasPrefix = "perf-automation-";
    private const string RunTable = "umbracoAutomateRun";
    private const string StepRunTable = "umbracoAutomateStepRun";
    private const string AutomationTable = "umbracoAutomateAutomation";

    private static readonly string[] ActionAliases =
    [
        "umbAutomate.logMessage",
        "umbAutomate.httpRequest",
        "umbAutomate.publishContent",
        "umbAutomate.delay",
        "umbAutomate.runScript",
        "umbAutomate.createContent",
        "umbAutomate.sendEmail",
        "umbAutomate.updateContent",
        "umbAutomate.approval",
        "umbAutomate.setVariable",
    ];

    private EFCoreAutomateAnalyticsRepository _repository = null!;
    private DateTime _currentFromUtc;
    private DateTime _currentToUtc;
    private DateTime _previousFromUtc;

    /// <summary>Seed time in this run of the fixture, or zero when the database was already seeded.</summary>
    public TimeSpan SeedDuration { get; private set; }

    /// <summary>One workspace's id, for the scoped calls.</summary>
    public IReadOnlySet<Guid> FirstWorkspaceScope { get; } = new HashSet<Guid> { GuidFor(1, 0) };

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            // Every spec is skipped, so there is nothing to build.
            return;
        }

        var factory = new SqlServerDbContextFactory(ConnectionString);
        _repository = new EFCoreAutomateAnalyticsRepository(factory);

        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        if (!await IsSeededAsync())
        {
            var stopwatch = Stopwatch.StartNew();
            await SeedAsync();
            SeedDuration = stopwatch.Elapsed;
            Console.WriteLine($"Seeded {RunCount:N0} runs in {SeedDuration.TotalSeconds:N0} s.");
        }

        // Anchor the ranges on the newest seeded run so a reused database still has its 180 days.
        await using var anchorDb = factory.CreateDbContext();
        var newest = await anchorDb.AutomationRuns.MaxAsync(r => r.StartedUtc);
        _currentToUtc = DateTime.SpecifyKind(newest!.Value.AddSeconds(1), DateTimeKind.Utc);
        _currentFromUtc = _currentToUtc.AddDays(-90);
        _previousFromUtc = _currentToUtc.AddDays(-180);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Calls the query once to warm up, then returns how long the second call took.</summary>
    public async Task<TimeSpan> TimeSecondCallAsync(Func<Task> query)
    {
        await query();

        var stopwatch = Stopwatch.StartNew();
        await query();
        return stopwatch.Elapsed;
    }

    public Task RunSummaryQueriesAsync() => RunSummaryQueriesAsync(null);

    // The current and comparison ranges run in parallel, each repository call on its own DbContext.
    public async Task RunSummaryQueriesAsync(IReadOnlySet<Guid>? workspaceIds)
    {
        async Task RangeAsync(DateTime from, DateTime to)
        {
            await _repository.GetRunSummaryAggregatesAsync(from, to, workspaceIds);
        }

        await Task.WhenAll(
            Ranges().Select(r => RangeAsync(r.From, r.To))
                .Append(_repository.GetSuspendedRunCountAsync(workspaceIds)));
    }

    public Task RunAutomationQueriesAsync() => RunAutomationQueriesAsync(null);

    public Task RunAutomationQueriesAsync(IReadOnlySet<Guid>? workspaceIds)
        => Task.WhenAll(Ranges().Select(r => _repository.GetAutomationAggregatesAsync(r.From, r.To, workspaceIds)));

    public Task RunActionQueriesAsync() => RunActionQueriesAsync(null);

    public Task RunActionQueriesAsync(IReadOnlySet<Guid>? workspaceIds)
        => Task.WhenAll(Ranges().Select(r => _repository.GetActionAggregatesAsync(r.From, r.To, workspaceIds)));

    private (DateTime From, DateTime To)[] Ranges()
        => [(_currentFromUtc, _currentToUtc), (_previousFromUtc, _currentFromUtc)];

    private async Task<bool> IsSeededAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var runs = await CountAsync(connection, RunTable);
        var steps = await CountAsync(connection, StepRunTable);
        return runs == RunCount && steps > RunCount * 3L;
    }

    private static async Task<long> CountAsync(SqlConnection connection, string table)
    {
        await using var command = new SqlCommand($"SELECT COUNT_BIG(*) FROM [{table}]", connection) { CommandTimeout = 0 };
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> HoldsOnlySeedRowsAsync(SqlConnection connection)
    {
        await using var command = new SqlCommand(
            $"SELECT (SELECT COUNT_BIG(*) FROM [{AutomationTable}] WHERE [Alias] NOT LIKE '{SeedAliasPrefix}%')" +
            $" + (SELECT COUNT_BIG(*) FROM [{RunTable}] WHERE [InitiatedBy] IS NULL OR [InitiatedBy] <> '{SeedMarker}')",
            connection)
        { CommandTimeout = 0 };
        return (long)(await command.ExecuteScalarAsync())! == 0;
    }

    private async Task SeedAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Only this harness's own rows (or nothing) may be cleared. Anything else is someone's data.
        if (!await HoldsOnlySeedRowsAsync(connection))
        {
            throw new InvalidOperationException(
                $"The database named by {ConnectionStringVariable} holds automations or runs that this speed test did not seed, " +
                $"so it will not be cleared and reseeded. Point {ConnectionStringVariable} at a dedicated database.");
        }

        // A half-finished earlier load is cleared; the automations are reseeded with it.
        foreach (var table in new[] { StepRunTable, RunTable, AutomationTable })
        {
            await using var delete = new SqlCommand($"DELETE FROM [{table}]", connection) { CommandTimeout = 0 };
            await delete.ExecuteNonQueryAsync();
        }

        var anchor = DateTime.UtcNow;

        await BulkLoadAsync(connection, AutomationTable, AutomationColumns(), GenerateAutomations(anchor));
        await BulkLoadAsync(connection, RunTable, RunColumns(), GenerateRuns(anchor).Select(RunRow));
        await BulkLoadAsync(connection, StepRunTable, StepRunColumns(), GenerateRuns(anchor).SelectMany(GenerateStepRows));
    }

    private static async Task BulkLoadAsync(
        SqlConnection connection,
        string table,
        (string Name, Type Type)[] columns,
        IEnumerable<object?[]> rows)
    {
        using var buffer = new DataTable();
        foreach (var (name, type) in columns)
        {
            buffer.Columns.Add(name, type);
        }

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = $"[{table}]",
            BatchSize = ChunkSize,
            BulkCopyTimeout = 0,
        };
        foreach (var (name, _) in columns)
        {
            bulkCopy.ColumnMappings.Add(name, name);
        }

        // Rows are generated lazily and flushed in chunks, so millions of rows are never held at once.
        foreach (var chunk in rows.Chunk(ChunkSize))
        {
            buffer.Clear();
            foreach (var row in chunk)
            {
                buffer.Rows.Add(row);
            }

            await bulkCopy.WriteToServerAsync(buffer);
        }
    }

    private static (string, Type)[] AutomationColumns() =>
    [
        ("Id", typeof(Guid)), ("Alias", typeof(string)), ("Name", typeof(string)), ("Status", typeof(int)),
        ("WorkspaceId", typeof(Guid)), ("Version", typeof(int)), ("DateCreated", typeof(DateTime)), ("DateModified", typeof(DateTime)),
    ];

    private static IEnumerable<object?[]> GenerateAutomations(DateTime anchor)
    {
        for (var i = 0; i < AutomationCount; i++)
        {
            yield return [GuidFor(2, i), $"{SeedAliasPrefix}{i}", $"Perf automation {i}", (int)AutomationStatus.Published, WorkspaceFor(i), 1, anchor.AddDays(-DayCount), anchor.AddDays(-DayCount)];
        }
    }

    private static (string, Type)[] RunColumns() =>
    [
        ("Id", typeof(Guid)), ("AutomationId", typeof(Guid)), ("AutomationVersion", typeof(int)), ("WorkspaceId", typeof(Guid)),
        ("ServiceAccountKey", typeof(Guid)), ("Status", typeof(int)), ("StartedUtc", typeof(DateTime)), ("CompletedUtc", typeof(DateTime)),
        ("DurationMs", typeof(long)), ("InitiatedBy", typeof(string)), ("Error", typeof(string)),
    ];

    private static object?[] RunRow(SeedRun run) =>
    [
        run.Id, run.AutomationId, 1, run.WorkspaceId, GuidFor(3, 0), (int)run.Status, run.StartedUtc, run.CompletedUtc,
        run.DurationMs, SeedMarker, run.Status == AutomationRunStatus.Failed ? "Seeded failure" : null,
    ];

    private static (string, Type)[] StepRunColumns() =>
    [
        ("Id", typeof(Guid)), ("RunId", typeof(Guid)), ("StepId", typeof(Guid)), ("ActionAlias", typeof(string)), ("Status", typeof(int)),
        ("StartedUtc", typeof(DateTime)), ("CompletedUtc", typeof(DateTime)), ("ErrorCategory", typeof(int)),
        ("RetryCount", typeof(int)), ("DurationTicks", typeof(long)),
    ];

    private static Guid WorkspaceFor(int automationIndex) => GuidFor(1, automationIndex % WorkspaceCount);

    // Deterministic ids: the runs are generated twice (runs, then their step runs) and must agree.
    private static Guid GuidFor(int kind, long index)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, index);
        BitConverter.TryWriteBytes(bytes[8..], kind);
        return new Guid(bytes);
    }

    private static IEnumerable<SeedRun> GenerateRuns(DateTime anchor)
    {
        var random = new Random(20261106);
        var start = anchor.AddDays(-DayCount);

        // Weekdays carry more runs than weekends, with some day-to-day noise.
        var weights = new double[DayCount];
        for (var day = 0; day < DayCount; day++)
        {
            var weekend = start.AddDays(day).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            weights[day] = (weekend ? 0.35 : 1.0) * (0.8 + 0.4 * random.NextDouble());
        }

        var total = weights.Sum();
        long index = 0;
        for (var day = 0; day < DayCount; day++)
        {
            var count = day == DayCount - 1 ? RunCount - index : (long)(RunCount * weights[day] / total);
            var dayStart = start.AddDays(day);
            for (var i = 0; i < count; i++, index++)
            {
                yield return NextRun(random, index, dayStart.AddMilliseconds(random.NextDouble() * TimeSpan.FromDays(1).TotalMilliseconds), anchor);
            }
        }
    }

    private static SeedRun NextRun(Random random, long index, DateTime startedUtc, DateTime anchor)
    {
        // Busy automations get most of the runs.
        var automation = (int)(AutomationCount * Math.Pow(random.NextDouble(), 2));
        var age = anchor - startedUtc;
        var roll = random.NextDouble();

        var status = roll switch
        {
            < 0.0004 when age < TimeSpan.FromDays(3) => AutomationRunStatus.Suspended,
            < 0.0006 when age < TimeSpan.FromDays(1) => AutomationRunStatus.Running,
            < 0.90 => AutomationRunStatus.Completed,
            < 0.96 => AutomationRunStatus.Failed,
            < 0.98 => AutomationRunStatus.Rejected,
            _ => AutomationRunStatus.Cancelled,
        };

        var finished = status is not (AutomationRunStatus.Suspended or AutomationRunStatus.Running);

        // Roughly log-normal, median a little over a second, failures slower.
        var gaussian = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        var durationMs = (long)(50 + Math.Exp(7.0 + 0.9 * gaussian) * (status == AutomationRunStatus.Failed ? 2 : 1));

        return new SeedRun(
            GuidFor(0, index),
            index,
            GuidFor(2, automation),
            WorkspaceFor(automation),
            status,
            startedUtc,
            finished ? startedUtc.AddMilliseconds(durationMs) : null,
            finished ? durationMs : null);
    }

    private static IEnumerable<object?[]> GenerateStepRows(SeedRun run)
    {
        // A per-run generator keeps the step runs stable however the run sequence is consumed.
        var random = new Random(unchecked((int)run.Index));
        var count = 2 + random.Next(5);
        var stepSpanMs = (run.DurationMs ?? 1_000) / count;

        for (var i = 0; i < count; i++)
        {
            var failed = run.Status == AutomationRunStatus.Failed && i == count - 1;
            var roll = random.NextDouble();
            var status = failed ? StepRunStatus.Failed : roll switch
            {
                < 0.03 => StepRunStatus.Skipped,
                < 0.04 => StepRunStatus.Rejected,
                < 0.045 => StepRunStatus.Cancelled,
                < 0.06 => StepRunStatus.Failed,
                _ => StepRunStatus.Completed,
            };

            var started = run.StartedUtc.AddMilliseconds(stepSpanMs * i);
            var timed = status is StepRunStatus.Completed or StepRunStatus.Failed or StepRunStatus.Rejected;
            var ticks = timed ? (long)(stepSpanMs * (0.5 + random.NextDouble()) * TimeSpan.TicksPerMillisecond) : (long?)null;
            int? errorCategory = status == StepRunStatus.Failed && random.NextDouble() < 0.8 ? 1 + random.Next(7) : null;

            yield return
            [
                GuidFor(4, run.Index * 8 + i),
                run.Id,
                GuidFor(5, i),
                ActionAliases[(int)(ActionAliases.Length * Math.Pow(random.NextDouble(), 1.5))],
                (int)status,
                started,
                ticks is null ? null : started.AddTicks(ticks.Value),
                errorCategory,
                random.NextDouble() < 0.05 ? 1 + random.Next(3) : 0,
                ticks,
            ];
        }
    }

    private readonly record struct SeedRun(
        Guid Id,
        long Index,
        Guid AutomationId,
        Guid WorkspaceId,
        AutomationRunStatus Status,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        long? DurationMs);

    private sealed class SqlServerDbContextFactory : IDbContextFactory<UmbracoAutomateDbContext>
    {
        private readonly DbContextOptions<UmbracoAutomateDbContext> _options;

        public SqlServerDbContextFactory(string connectionString)
            => _options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>()
                .UseSqlServer(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.Persistence.SqlServer");
                    x.MigrationsHistoryTable(DatabaseConnectionInfo.MigrationsHistoryTable);
                })
                .Options;

        public UmbracoAutomateDbContext CreateDbContext() => new(_options);
    }
}
