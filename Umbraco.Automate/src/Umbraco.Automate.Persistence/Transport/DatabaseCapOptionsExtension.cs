using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// CAP options extension that registers the database-backed transport.
/// Uses the shared Umbraco database for cross-node message delivery.
/// </summary>
internal sealed class DatabaseCapOptionsExtension : ICapOptionsExtension
{
    public void AddServices(IServiceCollection services)
    {
        services.AddSingleton(new CapMessageQueueMakerService("Database"));
        services.AddSingleton<ITransport, DatabaseTransport>();
        services.AddSingleton<IConsumerClientFactory, DatabaseConsumerClientFactory>();
    }
}
