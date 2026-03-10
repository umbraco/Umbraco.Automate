using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Transport;

namespace Umbraco.Automate.Core.Dispatch.Transport;

/// <summary>
/// In-process <see cref="IConsumerClient"/> that reads messages from the shared <see cref="InProcessMessageBus"/>.
/// </summary>
internal sealed class InProcessConsumerClient : IConsumerClient, IAsyncDisposable
{
    private readonly InProcessMessageBus _bus;
    private readonly string _groupName;
    private HashSet<string> _subscribedTopics = [];

    public InProcessConsumerClient(InProcessMessageBus bus, string groupName)
    {
        _bus = bus;
        _groupName = groupName;
    }

    public BrokerAddress BrokerAddress { get; } = new("InProcess", "localhost");

    public Func<TransportMessage, object?, Task>? OnMessageCallback { get; set; }

    public Action<LogMessageEventArgs>? OnLogCallback { get; set; }

    public Task<ICollection<string>> FetchTopicsAsync(IEnumerable<string> topicNames)
    {
        ICollection<string> topics = topicNames.ToList();
        return Task.FromResult(topics);
    }

    public Task SubscribeAsync(IEnumerable<string> topics)
    {
        _subscribedTopics = [.. topics];
        return Task.CompletedTask;
    }

    public async Task ListeningAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = await _bus.Reader.ReadAsync(cancellationToken);

                // Only deliver messages matching subscribed topics.
                var topic = message.GetName();
                if (topic is null || !_subscribedTopics.Contains(topic))
                {
                    continue;
                }

                // Stamp the group header so CAP routes to the right consumer.
                message.Headers[Headers.Group] = _groupName;

                if (OnMessageCallback is not null)
                {
                    await OnMessageCallback(message, null);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public Task CommitAsync(object? sender) => Task.CompletedTask;

    public Task RejectAsync(object? sender) => Task.CompletedTask;

    public void Dispose()
    {
        // Nothing to dispose.
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
