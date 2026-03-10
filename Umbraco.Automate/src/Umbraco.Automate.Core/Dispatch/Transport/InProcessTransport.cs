using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Transport;

namespace Umbraco.Automate.Core.Dispatch.Transport;

/// <summary>
/// In-process <see cref="ITransport"/> that writes messages to a shared <see cref="InProcessMessageBus"/>.
/// </summary>
internal sealed class InProcessTransport : ITransport
{
    private readonly InProcessMessageBus _bus;

    public InProcessTransport(InProcessMessageBus bus) => _bus = bus;

    public BrokerAddress BrokerAddress { get; } = new("InProcess", "localhost");

    public async Task<OperateResult> SendAsync(TransportMessage message)
    {
        await _bus.Writer.WriteAsync(message);
        return OperateResult.Success;
    }
}
