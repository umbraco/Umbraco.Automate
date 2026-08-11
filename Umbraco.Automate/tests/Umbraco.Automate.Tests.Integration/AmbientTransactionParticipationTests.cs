using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StackExchange.Profiling;
using StackExchange.Profiling.Data;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Infrastructure.Persistence.FaultHandling;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Reproduces and guards the fix for the SQLite deadlock hit when Automate shares the Umbraco CMS
/// database (<c>Umbraco:Automate:UseNamedConnectionString: umbracoDbDSN</c>) and a caller — Umbraco
/// Deploy's restore, most visibly — writes Automate entities from inside an ambient Umbraco scope.
/// </summary>
/// <remarks>
/// Deploy wraps a whole restore in one ambient Umbraco scope and takes the distributed worker write
/// lock eagerly, which on SQLite is the single per-file write lock. Automate's repositories open their
/// own connection via <see cref="IDbContextFactory{TContext}"/>, so pointed at the same file they can
/// never acquire that lock while the restore that is waiting on them still holds it. These tests use a
/// real file-backed SQLite database because the deadlock is a property of file-level locking that an
/// in-memory shared-cache database does not exhibit.
/// </remarks>
public sealed class AmbientTransactionParticipationTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public AmbientTransactionParticipationTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"automate_ambient_{Guid.NewGuid():N}.sqlite");

        // Pooling=False so the file handle is released on close and the temp file can be deleted.
        // Default Timeout=1 keeps the "cannot get the write lock" case fast: without it every
        // contended write would busy-wait for the 30s default before reporting the lock.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
            DefaultTimeout = 1,
        }.ToString();

        using var context = CreateDetachedContext();
        context.Database.EnsureCreated();

        // Umbraco runs SQLite in WAL mode; match it so the locking behaviour under test is the
        // same one a real site sees.
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    [Fact]
    public async Task DetachedFactory_CannotWrite_WhileAmbientTransactionHoldsTheWriteLock()
    {
        using var ambient = BeginAmbientWriteTransaction();

        var repository = CreateRepository(new DetachedDbContextFactory(CreateDetachedContext));

        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => repository.SaveAsync(new AutomationBuilder().WithAlias("detached").Build()));

        // "SQLite Error 5: 'database is locked'" (or error 6, 'table is locked', depending on how the
        // connection string is tuned) — the failure reported in Umbraco.Deploy.Automate#9.
        var sqliteException = exception.InnerException.ShouldBeOfType<SqliteException>();
        sqliteException.SqliteErrorCode.ShouldBeOneOf(
            SqliteErrorCodes.Busy,
            SqliteErrorCodes.Locked);
    }

    [Fact]
    public async Task AmbientFactory_Writes_WhileAmbientTransactionHoldsTheWriteLock()
    {
        using var ambient = BeginAmbientWriteTransaction();

        var repository = CreateRepository(CreateAmbientFactory(ambient.Transaction));

        var saved = await repository.SaveAsync(new AutomationBuilder().WithAlias("ambient").Build());

        saved.Alias.ShouldBe("ambient");
    }

    [Fact]
    public async Task AmbientFactory_WriteIsPartOfTheAmbientTransaction()
    {
        using var ambient = BeginAmbientWriteTransaction();

        var repository = CreateRepository(CreateAmbientFactory(ambient.Transaction));
        await repository.SaveAsync(new AutomationBuilder().WithAlias("enlisted").Build());

        // Uncommitted: a reader on its own connection must not see the row yet.
        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(0);

        ambient.Transaction.Commit();

        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AmbientFactory_RollsBackWithTheAmbientTransaction()
    {
        using (var ambient = BeginAmbientWriteTransaction())
        {
            var repository = CreateRepository(CreateAmbientFactory(ambient.Transaction));
            await repository.SaveAsync(new AutomationBuilder().WithAlias("rolled-back").Build());

            ambient.Transaction.Rollback();
        }

        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AmbientFactory_FallsBackToTheDetachedFactory_WhenThereIsNoAmbientTransaction()
    {
        var repository = CreateRepository(CreateAmbientFactory(ambientTransaction: null));

        await repository.SaveAsync(new AutomationBuilder().WithAlias("no-ambient-scope").Build());

        // No ambient transaction to enlist in, so the write committed on its own connection.
        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(1);
    }

    /// <summary>
    /// The shape a real ambient scope actually hands over. NPoco applies Umbraco's registered
    /// <c>IProviderSpecificInterceptor</c>s to every connection it opens
    /// (<c>UmbracoDatabaseFactory.CreateSqlContext</c>), and for SQLite both of them wrap
    /// unconditionally, MiniProfiler first and then the retry policy. So
    /// <c>scope.Database.Transaction</c> is a <c>ProfiledDbTransaction</c> whose connection is a
    /// <c>ProfiledDbConnection</c>, never the bare <see cref="SqliteConnection"/> the other tests here
    /// use — and EF Core has to accept that.
    /// </summary>
    [Fact]
    public async Task AmbientFactory_Writes_WhenTheAmbientConnectionIsWrappedTheWayUmbracoWrapsIt()
    {
        using AmbientWriteTransaction ambient = BeginAmbientWriteTransaction(wrapLikeUmbraco: true);

        ambient.Transaction.ShouldBeOfType<ProfiledDbTransaction>();
        ambient.Transaction.Connection.ShouldBeOfType<ProfiledDbConnection>();

        var repository = CreateRepository(CreateAmbientFactory(ambient.Transaction));
        await repository.SaveAsync(new AutomationBuilder().WithAlias("wrapped").Build());

        // Uncommitted, so the write really did land on the ambient transaction rather than on a
        // second connection that quietly committed.
        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(0);

        ambient.Transaction.Commit();

        (await CountAutomationsOnANewConnectionAsync()).ShouldBe(1);
    }

    /// <summary>
    /// Stands in for Umbraco Deploy's ambient scope: an open connection whose transaction has
    /// already written, and therefore holds SQLite's single per-file write lock.
    /// </summary>
    /// <param name="wrapLikeUmbraco">
    /// When <c>true</c>, wraps the connection the way NPoco's interceptors do in a real site. See
    /// <see cref="AmbientFactory_Writes_WhenTheAmbientConnectionIsWrappedTheWayUmbracoWrapsIt"/>.
    /// </param>
    private AmbientWriteTransaction BeginAmbientWriteTransaction(bool wrapLikeUmbraco = false)
    {
        var sqliteConnection = new SqliteConnection(_connectionString);
        sqliteConnection.Open();

        // Registration order in Umbraco.Cms.Persistence.Sqlite.UmbracoBuilderExtensions: MiniProfiler
        // then retry policy, so the retry wrapper ends up outermost.
        DbConnection connection = wrapLikeUmbraco
            ? new RetryDbConnection(
                new ProfiledDbConnection(sqliteConnection, MiniProfiler.Current),
                conRetryPolicy: null,
                cmdRetryPolicy: null)
            : sqliteConnection;

        DbTransaction transaction = connection.BeginTransaction();

        // SQLite defers the write lock until the first write in the transaction, so issue one.
        // Deploy's equivalent is EagerWriteLock's UPDATE of umbracoLock.
        using DbCommand command = sqliteConnection.CreateCommand();
        command.Transaction = Unwrap(transaction);
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS ambientWriteLockProbe (id INTEGER PRIMARY KEY); " +
            "INSERT INTO ambientWriteLockProbe (id) VALUES (1);";
        command.ExecuteNonQuery();

        return new AmbientWriteTransaction(connection, transaction);
    }

    // The priming write goes through the raw connection, so it needs the raw transaction.
    private static DbTransaction Unwrap(DbTransaction transaction)
        => transaction is ProfiledDbTransaction profiled ? profiled.WrappedTransaction : transaction;

    private UmbracoAutomateDbContext CreateDetachedContext()
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(options, _connectionString, SqliteProviderName);

        return new UmbracoAutomateDbContext(options.Options);
    }

    private UmbracoAutomateDbContext CreateEnlistedContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(options, connection, SqliteProviderName);

        return new UmbracoAutomateDbContext(options.Options);
    }

    private AmbientDbContextFactory<UmbracoAutomateDbContext> CreateAmbientFactory(
        DbTransaction? ambientTransaction)
        => new(
            new DetachedDbContextFactory(CreateDetachedContext),
            new StubAmbientConnection(ambientTransaction),
            CreateEnlistedContext);

    private async Task<int> CountAutomationsOnANewConnectionAsync()
    {
        await using var context = CreateDetachedContext();

        return await context.Automations.CountAsync();
    }

    private static EFCoreAutomationRepository CreateRepository(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
        => new(dbContextFactory, CreateAutomationFactory());

    private static AutomationFactory CreateAutomationFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    private const string SqliteProviderName = Umbraco.Cms.Core.Constants.ProviderNames.SQLLite;

    private static class SqliteErrorCodes
    {
        internal const int Busy = 5;
        internal const int Locked = 6;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        // WAL mode leaves a -wal and a -shm alongside the database file; delete all three or the temp
        // directory collects two orphans per test class run.
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var path = _databasePath + suffix;

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class AmbientWriteTransaction : IDisposable
    {
        private readonly DbConnection _connection;

        internal AmbientWriteTransaction(DbConnection connection, DbTransaction transaction)
        {
            _connection = connection;
            Transaction = transaction;
        }

        internal DbTransaction Transaction { get; }

        public void Dispose()
        {
            Transaction.Dispose();
            _connection.Dispose();
        }
    }

    private sealed class DetachedDbContextFactory : IDbContextFactory<UmbracoAutomateDbContext>
    {
        private readonly Func<UmbracoAutomateDbContext> _create;

        internal DetachedDbContextFactory(Func<UmbracoAutomateDbContext> create) => _create = create;

        public UmbracoAutomateDbContext CreateDbContext() => _create();
    }

    private sealed class StubAmbientConnection : IAmbientAutomateConnection
    {
        internal StubAmbientConnection(DbTransaction? transaction) => Transaction = transaction;

        public DbTransaction? Transaction { get; }
    }
}
