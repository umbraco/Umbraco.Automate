using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// Test fixture providing an in-memory SQLite database for OpenIddict repository tests.
/// </summary>
public sealed class OpenIddictTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public OpenIddictTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public OpenIddictDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new OpenIddictDbContext(options);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

/// <summary>
/// Test scope provider for <see cref="OpenIddictDbContext"/>.
/// </summary>
public sealed class TestOpenIddictScopeProvider : IEFCoreScopeProvider<OpenIddictDbContext>
{
    private readonly Func<OpenIddictDbContext> _contextFactory;

    public TestOpenIddictScopeProvider(Func<OpenIddictDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IEfCoreScope<OpenIddictDbContext> CreateScope(
        RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
        bool? scopeFileSystems = null)
        => new TestOpenIddictScope(_contextFactory());

    public IEfCoreScope<OpenIddictDbContext> CreateDetachedScope(
        RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
        bool? scopeFileSystems = null)
        => CreateScope(repositoryCacheMode, scopeFileSystems);

    public void AttachScope(IEfCoreScope<OpenIddictDbContext> other) { }

    public IEfCoreScope<OpenIddictDbContext> DetachScope()
        => throw new NotSupportedException();

    public IScopeContext? AmbientScopeContext => null;
}

/// <summary>
/// Test scope for <see cref="OpenIddictDbContext"/>.
/// </summary>
public sealed class TestOpenIddictScope : IEfCoreScope<OpenIddictDbContext>
{
    private readonly OpenIddictDbContext _context;
    private bool _completed;
    private bool _disposed;

    public TestOpenIddictScope(OpenIddictDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteWithContextAsync<T>(Func<OpenIddictDbContext, Task<T>> method)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await method(_context);
    }

    public async Task ExecuteWithContextAsync<T>(Func<OpenIddictDbContext, Task> method)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await method(_context);
    }

    public IScopeContext? ScopeContext { get; set; }
    public IScopedNotificationPublisher Notifications => throw new NotSupportedException();
    public Guid InstanceId { get; } = Guid.NewGuid();
    public int CreatedThreadId => Environment.CurrentManagedThreadId;
    public int Depth => 0;
    public ILockingMechanism Locks => throw new NotSupportedException();
    public RepositoryCacheMode RepositoryCacheMode => RepositoryCacheMode.Default;
    public IsolatedCaches IsolatedCaches => throw new NotSupportedException();

    public bool Complete() { _completed = true; return true; }
    public void ReadLock(params int[] lockIds) { }
    public void WriteLock(params int[] lockIds) { }
    public void WriteLock(TimeSpan timeout, int lockId) { }
    public void ReadLock(TimeSpan timeout, int lockId) { }
    public void EagerWriteLock(params int[] lockIds) { }
    public void EagerWriteLock(TimeSpan timeout, int lockId) { }
    public void EagerReadLock(TimeSpan timeout, int lockId) { }
    public void EagerReadLock(params int[] lockIds) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_completed) _context.SaveChanges();
        _context.Dispose();
    }
}

/// <summary>
/// Test protector that passes values through unmodified (no encryption).
/// </summary>
public sealed class PassthroughFieldProtector : ISensitiveFieldProtector
{
    public string? Protect(string? value) => value;
    public string? Unprotect(string? value) => value;
    public bool IsProtected(string? value) => false;
}
