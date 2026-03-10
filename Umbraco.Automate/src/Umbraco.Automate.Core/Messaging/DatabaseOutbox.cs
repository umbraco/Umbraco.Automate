using System.Text.Json;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// <see cref="IOutbox"/> implementation that writes messages to the database via <see cref="IOutboxStore"/>.
/// </summary>
internal sealed class DatabaseOutbox : IOutbox
{
    private readonly IOutboxStore _store;

    public DatabaseOutbox(IOutboxStore store)
    {
        _store = store;
    }

    public async Task PublishAsync(string topic, object message, CancellationToken cancellationToken)
    {
        var outboxMessage = new OutboxMessage
        {
            Topic = topic,
            Body = JsonSerializer.Serialize(message, JsonOptions.Default),
            CreatedUtc = DateTime.UtcNow,
            Status = MessageStatus.Pending,
        };

        await _store.InsertAsync(outboxMessage, cancellationToken);
    }
}
