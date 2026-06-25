using Umbraco.Automate.Persistence;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Test implementation of <see cref="IEFCoreScopeProvider{TDbContext}"/> for integration testing.
/// </summary>
public class TestEfCoreScopeProvider : IEFCoreScopeProvider<UmbracoAutomateDbContext>
{
    private readonly Func<UmbracoAutomateDbContext> _contextFactory;

    public TestEfCoreScopeProvider(Func<UmbracoAutomateDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IEFCoreScope<UmbracoAutomateDbContext> CreateScope(
        RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
        bool? scopeFileSystems = null)
    {
        return new TestEfCoreScope(_contextFactory());
    }

    public IEFCoreScope<UmbracoAutomateDbContext> CreateDetachedScope(
        RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
        bool? scopeFileSystems = null)
    {
        return CreateScope(repositoryCacheMode, scopeFileSystems);
    }

    public void AttachScope(IEFCoreScope<UmbracoAutomateDbContext> other)
    {
        // No-op for tests
    }

    public IEFCoreScope<UmbracoAutomateDbContext> DetachScope()
    {
        throw new NotSupportedException("DetachScope is not supported in test scope provider.");
    }

    public IScopeContext? AmbientScopeContext => null;
}

/// <summary>
/// Test implementation of <see cref="IEFCoreScope{TDbContext}"/> for integration testing.
/// </summary>
public class TestEfCoreScope : IEFCoreScope<UmbracoAutomateDbContext>
{
    private readonly UmbracoAutomateDbContext _context;
    private bool _completed;
    private bool _disposed;

    public TestEfCoreScope(UmbracoAutomateDbContext context)
    {
        _context = context;
    }

    public async Task<T> ExecuteWithContextAsync<T>(Func<UmbracoAutomateDbContext, Task<T>> method)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestEfCoreScope));
        }

        return await method(_context);
    }

    public async Task ExecuteWithContextAsync<T>(Func<UmbracoAutomateDbContext, Task> method)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestEfCoreScope));
        }

        await method(_context);
    }

    public IScopeContext? ScopeContext { get; set; }

    public IScopedNotificationPublisher Notifications =>
        throw new NotSupportedException("Notifications are not supported in test scope.");

    public Guid InstanceId { get; } = Guid.NewGuid();

    public int CreatedThreadId => Environment.CurrentManagedThreadId;

    public int Depth => 0;

    public ILockingMechanism Locks =>
        throw new NotSupportedException("Locking is not supported in test scope.");

    public RepositoryCacheMode RepositoryCacheMode => RepositoryCacheMode.Default;

    public IsolatedCaches IsolatedCaches =>
        throw new NotSupportedException("IsolatedCaches is not supported in test scope.");

    public bool Complete()
    {
        _completed = true;
        return true;
    }

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
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_completed)
        {
            _context.SaveChanges();
        }

        _context.Dispose();
    }
}
