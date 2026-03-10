using System.Threading.Channels;
using DotNetCore.CAP.Messages;

namespace Umbraco.Automate.Core.Dispatch.Transport;

/// <summary>
/// Shared in-process message bus using <see cref="Channel{T}"/>.
/// Singleton — all transport and consumer instances share the same channels.
/// </summary>
internal sealed class InProcessMessageBus
{
    private readonly Channel<TransportMessage> _channel =
        Channel.CreateUnbounded<TransportMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

    public ChannelWriter<TransportMessage> Writer => _channel.Writer;
    public ChannelReader<TransportMessage> Reader => _channel.Reader;
}
