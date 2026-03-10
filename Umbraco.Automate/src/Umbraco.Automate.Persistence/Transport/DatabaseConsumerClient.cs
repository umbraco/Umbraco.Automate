using System.Text.Json;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// Database-backed <see cref="IConsumerClient"/> that polls a shared table for messages.
/// Uses optimistic claiming to ensure each message is processed by only one consumer group.
/// </summary>
internal sealed class DatabaseConsumerClient : IConsumerClient, IAsyncDisposable
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly IRuntimeState _runtimeState;
    private readonly string _groupName;
    private readonly ILogger<DatabaseConsumerClient> _logger;
    private HashSet<string> _subscribedTopics = [];

    public DatabaseConsumerClient(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        IRuntimeState runtimeState,
        string groupName,
        ILogger<DatabaseConsumerClient> logger)
    {
        _scopeProvider = scopeProvider;
        _runtimeState = runtimeState;
        _groupName = groupName;
        _logger = logger;
    }

    public BrokerAddress BrokerAddress { get; } = new("Database", "localhost");

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
        // Wait for Umbraco to finish booting (migrations must complete first).
        while (_runtimeState.Level != RuntimeLevel.Run && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        var topics = _subscribedTopics.ToList();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await ClaimNextMessageAsync(topics, cancellationToken);

                if (claimed is { } message)
                {
                    if (OnMessageCallback is not null)
                    {
                        await OnMessageCallback(message, null);
                    }
                }
                else
                {
                    // No messages available — wait before polling again.
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling transport messages, retrying");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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

    /// <summary>
    /// Claims the next unclaimed message for this consumer group.
    /// Uses optimistic concurrency — the first consumer to set ClaimedByGroup wins.
    /// </summary>
    private async Task<TransportMessage?> ClaimNextMessageAsync(
        List<string> topics, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            // Find the oldest unclaimed message for any of our subscribed topics.
            var entity = await db.TransportMessages
                .Where(m => topics.Contains(m.Topic) && m.ClaimedByGroup == null)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                return (TransportMessage?)null;
            }

            // Claim it for our group.
            entity.ClaimedByGroup = _groupName;
            entity.ClaimedUtc = DateTime.UtcNow;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another consumer claimed it first — that's fine.
                return null;
            }

            // Reconstruct the CAP TransportMessage.
            var headers = JsonSerializer.Deserialize<Dictionary<string, string?>>(entity.Headers)
                          ?? new Dictionary<string, string?>();

            headers[Headers.Group] = _groupName;

            var body = entity.Body is not null
                ? new ReadOnlyMemory<byte>(System.Text.Encoding.UTF8.GetBytes(entity.Body))
                : ReadOnlyMemory<byte>.Empty;

            return new TransportMessage(headers, body);
        });

        scope.Complete();
        return result;
    }
}
