using DotNetCore.CAP.Transport;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// Factory that creates <see cref="DatabaseConsumerClient"/> instances.
/// </summary>
internal sealed class DatabaseConsumerClientFactory : IConsumerClientFactory
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<DatabaseConsumerClient> _logger;

    public DatabaseConsumerClientFactory(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        IRuntimeState runtimeState,
        ILogger<DatabaseConsumerClient> logger)
    {
        _scopeProvider = scopeProvider;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public Task<IConsumerClient> CreateAsync(string groupName, byte groupConcurrent)
    {
        IConsumerClient client = new DatabaseConsumerClient(_scopeProvider, _runtimeState, groupName, _logger);
        return Task.FromResult(client);
    }
}
