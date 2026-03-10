using DotNetCore.CAP.Transport;

namespace Umbraco.Automate.Core.Dispatch.Transport;

/// <summary>
/// Factory that creates <see cref="InProcessConsumerClient"/> instances backed by the shared
/// <see cref="InProcessMessageBus"/>.
/// </summary>
internal sealed class InProcessConsumerClientFactory : IConsumerClientFactory
{
    private readonly InProcessMessageBus _bus;

    public InProcessConsumerClientFactory(InProcessMessageBus bus) => _bus = bus;

    public Task<IConsumerClient> CreateAsync(string groupName, byte groupConcurrent)
    {
        IConsumerClient client = new InProcessConsumerClient(_bus, groupName);
        return Task.FromResult(client);
    }
}
