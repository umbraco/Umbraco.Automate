using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// The OpenIddict counterpart of Umbraco.Automate's own ambient-transaction tests. This DbContext
/// resolves the same Automate connection string, so when that points at the Umbraco CMS database it is
/// exposed to the same SQLite deadlock: a second connection cannot get the single per-file write lock
/// while an ambient Umbraco scope holds it, and that scope cannot commit until the write it is waiting
/// on completes.
/// </summary>
/// <remarks>
/// A real file-backed database is used deliberately — the deadlock is a property of file-level locking
/// that an in-memory shared-cache database does not exhibit.
/// </remarks>
public sealed class AmbientTransactionParticipationTests : IDisposable
{
    private const string SqliteProviderName = Umbraco.Cms.Core.Constants.ProviderNames.SQLLite;

    private readonly string _databasePath;
    private readonly string _connectionString;

    public AmbientTransactionParticipationTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"openiddict_ambient_{Guid.NewGuid():N}.sqlite");

        // Pooling=False so the file handle is released on close and the temp file can be deleted.
        // Default Timeout=1 keeps the "cannot get the write lock" case fast.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false,
            DefaultTimeout = 1,
        }.ToString();

        using OpenIddictDbContext context = CreateDetachedContext();
        context.Database.EnsureCreated();

        // Umbraco runs SQLite in WAL mode; match it.
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    [Fact]
    public async Task DetachedFactory_CannotWrite_WhileAmbientTransactionHoldsTheWriteLock()
    {
        using AmbientWriteTransaction ambient = BeginAmbientWriteTransaction();

        EFCoreOAuthCredentialsRepository repository =
            CreateRepository(new DetachedDbContextFactory(CreateDetachedContext));

        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => repository.SaveAsync(CreateCredentials()));

        var sqliteException = exception.InnerException.ShouldBeOfType<SqliteException>();
        sqliteException.SqliteErrorCode.ShouldBeOneOf(SqliteBusy, SqliteLocked);
    }

    [Fact]
    public async Task AmbientFactory_Writes_WhileAmbientTransactionHoldsTheWriteLock()
    {
        using AmbientWriteTransaction ambient = BeginAmbientWriteTransaction();

        EFCoreOAuthCredentialsRepository repository =
            CreateRepository(CreateAmbientFactory(ambient.Transaction));

        OAuthCredentials credentials = CreateCredentials();

        await repository.SaveAsync(credentials);

        // Uncommitted: a reader on its own connection must not see the row yet.
        (await CountCredentialsOnANewConnectionAsync()).ShouldBe(0);

        ambient.Transaction.Commit();

        (await CountCredentialsOnANewConnectionAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AmbientFactory_FallsBackToTheDetachedFactory_WhenThereIsNoAmbientTransaction()
    {
        EFCoreOAuthCredentialsRepository repository =
            CreateRepository(CreateAmbientFactory(ambientTransaction: null));

        await repository.SaveAsync(CreateCredentials());

        (await CountCredentialsOnANewConnectionAsync()).ShouldBe(1);
    }

    private static OAuthCredentials CreateCredentials() => new()
    {
        Id = Guid.NewGuid(),
        Provider = "Slack",
        AccessToken = "xoxb-test-token",
        DateCreated = DateTime.UtcNow,
        DateModified = DateTime.UtcNow,
    };

    /// <summary>
    /// Stands in for an ambient Umbraco scope: an open connection whose transaction has already
    /// written, and therefore holds SQLite's single per-file write lock.
    /// </summary>
    private AmbientWriteTransaction BeginAmbientWriteTransaction()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        SqliteTransaction transaction = connection.BeginTransaction();

        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS ambientWriteLockProbe (id INTEGER PRIMARY KEY); " +
            "INSERT INTO ambientWriteLockProbe (id) VALUES (1);";
        command.ExecuteNonQuery();

        return new AmbientWriteTransaction(connection, transaction);
    }

    private OpenIddictDbContext CreateDetachedContext()
    {
        var options = new DbContextOptionsBuilder<OpenIddictDbContext>();
        OpenIddictDbContext.ConfigureProvider(options, _connectionString, SqliteProviderName);

        return new OpenIddictDbContext(options.Options);
    }

    private OpenIddictDbContext CreateEnlistedContext(DbConnection connection)
    {
        var options = new DbContextOptionsBuilder<OpenIddictDbContext>();
        OpenIddictDbContext.ConfigureProvider(options, connection, SqliteProviderName);

        return new OpenIddictDbContext(options.Options);
    }

    private AmbientDbContextFactory<OpenIddictDbContext> CreateAmbientFactory(
        DbTransaction? ambientTransaction)
        => new(
            new DetachedDbContextFactory(CreateDetachedContext),
            new StubAmbientConnection(ambientTransaction),
            CreateEnlistedContext);

    private async Task<int> CountCredentialsOnANewConnectionAsync()
    {
        await using OpenIddictDbContext context = CreateDetachedContext();

        return await context.OAuthCredentials.CountAsync();
    }

    private static EFCoreOAuthCredentialsRepository CreateRepository(
        IDbContextFactory<OpenIddictDbContext> dbContextFactory)
        => new(dbContextFactory, new OAuthCredentialsFactory(new PassthroughFieldProtector()));

    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

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

    private sealed class DetachedDbContextFactory : IDbContextFactory<OpenIddictDbContext>
    {
        private readonly Func<OpenIddictDbContext> _create;

        internal DetachedDbContextFactory(Func<OpenIddictDbContext> create) => _create = create;

        public OpenIddictDbContext CreateDbContext() => _create();
    }

    private sealed class StubAmbientConnection : IAmbientAutomateConnection
    {
        internal StubAmbientConnection(DbTransaction? transaction) => Transaction = transaction;

        public DbTransaction? Transaction { get; }
    }
}
