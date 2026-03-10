using DotNetCore.CAP;
using DotNetCore.CAP.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Umbraco.Automate.Core.Dispatch.Transport;

/// <summary>
/// <see cref="ICapOptionsExtension"/> that registers an in-process transport for CAP.
/// Uses <see cref="System.Threading.Channels.Channel{T}"/> under the hood — suitable for
/// single-process deployments (Umbraco Automate v1).
/// </summary>
internal sealed class InProcessCapOptionsExtension : ICapOptionsExtension
{
    public void AddServices(IServiceCollection services)
    {
        // Marker service — CAP's Bootstrapper.CheckRequirement() resolves this to verify
        // that a transport provider has been configured.
        services.AddSingleton(new CapMessageQueueMakerService("InProcess"));

        services.AddSingleton<InProcessMessageBus>();
        services.AddSingleton<ITransport, InProcessTransport>();
        services.AddSingleton<IConsumerClientFactory, InProcessConsumerClientFactory>();
    }
}
