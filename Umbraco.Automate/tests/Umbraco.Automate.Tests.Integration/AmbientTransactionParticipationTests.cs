using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Scoping;
using Umbraco.Automate.Testing.Builders;

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
    /// Stands in for Umbraco Deploy's ambient scope: an open connection whose transaction has
    /// already written, and therefore holds SQLite's single per-file write lock.
    /// </summary>
    private AmbientWriteTransaction BeginAmbientWriteTransaction()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        SqliteTransaction transaction = connection.BeginTransaction();

        // SQLite defers the write lock until the first write in the transaction, so issue one.
        // Deploy's equivalent is EagerWriteLock's UPDATE of umbracoLock.
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS ambientWriteLockProbe (id INTEGER PRIMARY KEY); " +
            "INSERT INTO ambientWriteLockProbe (id) VALUES (1);";
        command.ExecuteNonQuery();

        return new AmbientWriteTransaction(connection, transaction);
    }

    private UmbracoAutomateDbContext CreateDetachedContext()
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(options, _connectionString, SqliteProviderName);

        return new UmbracoAutomateDbContext(options.Options);
    }

    private AmbientAutomateDbContextFactory CreateAmbientFactory(DbTransaction? ambientTransaction)
        => new(
            new DetachedDbContextFactory(CreateDetachedContext),
            new StubAmbientConnection(ambientTransaction),
            SqliteProviderName,
            interceptors: []);

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

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class AmbientWriteTransaction : IDisposable
    {
        private readonly SqliteConnection _connection;

        internal AmbientWriteTransaction(SqliteConnection connection, SqliteTransaction transaction)
        {
            _connection = connection;
            Transaction = transaction;
        }

        internal SqliteTransaction Transaction { get; }

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
