using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Test implementation of <see cref="IDbContextFactory{TContext}"/> for integration testing.
/// </summary>
public class TestDbContextFactory : IDbContextFactory<UmbracoAutomateDbContext>
{
    private readonly Func<UmbracoAutomateDbContext> _contextFactory;

    public TestDbContextFactory(Func<UmbracoAutomateDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public UmbracoAutomateDbContext CreateDbContext() => _contextFactory();
}
