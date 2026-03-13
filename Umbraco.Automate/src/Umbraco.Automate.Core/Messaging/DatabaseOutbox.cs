using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// <see cref="IOutbox"/> implementation that writes messages to the database via <see cref="IOutboxStore"/>.
/// </summary>
internal sealed class DatabaseOutbox : IOutbox
{
    private readonly IOutboxStore _store;
    private readonly IOptions<OutboxOptions> _options;
    private readonly AutomateReadinessSignal _readinessSignal;

    public DatabaseOutbox(IOutboxStore store, IOptions<OutboxOptions> options, AutomateReadinessSignal readinessSignal)
    {
        _store = store;
        _options = options;
        _readinessSignal = readinessSignal;
    }

    public async Task PublishAsync(string topic, object message, CancellationToken cancellationToken, string? idempotencyKey = null)
    {
        // Silently drop messages published before the database is ready (e.g. content
        // published during Umbraco's own migration seeding, before Automate tables exist).
        if (!_readinessSignal.IsReady)
        {
            return;
        }

        var maxPending = _options.Value.MaxPendingMessages;
        if (maxPending > 0)
        {
            var stats = await _store.GetStatsAsync(cancellationToken);
            var currentDepth = stats.Pending + stats.Processing;

            if (currentDepth >= maxPending)
            {
                throw new OutboxBackpressureException(currentDepth, maxPending);
            }
        }

        var outboxMessage = new OutboxMessage
        {
            Topic = topic,
            Body = JsonSerializer.Serialize(message, JsonOptions.Default),
            CreatedUtc = DateTime.UtcNow,
            Status = MessageStatus.Pending,
            IdempotencyKey = idempotencyKey,
        };

        await _store.InsertAsync(outboxMessage, cancellationToken);
    }
}
