using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Persistence.Scoping;
using Umbraco.Automate.Persistence;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Test implementation of <see cref="IDbContextFactory{TContext}"/> for integration testing. Also
/// satisfies <see cref="IDetachedDbContextFactory{TDbContext}"/>, which is what the engine stores
/// ask for.
/// </summary>
public class TestDbContextFactory : IDetachedDbContextFactory<UmbracoAutomateDbContext>
{
    private readonly Func<UmbracoAutomateDbContext> _contextFactory;

    public TestDbContextFactory(Func<UmbracoAutomateDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public UmbracoAutomateDbContext CreateDbContext() => _contextFactory();
}
