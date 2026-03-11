using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutboxStore"/> using optimistic concurrency for claiming.
/// </summary>
internal sealed class EFCoreOutboxStore : IOutboxStore
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<EFCoreOutboxStore> _logger;

    public EFCoreOutboxStore(
        IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider,
        IOptions<OutboxOptions> options,
        ILogger<EFCoreOutboxStore> logger)
    {
        _scopeProvider = scopeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task InsertAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            // Idempotency: if a message with the same topic + key already exists, skip.
            if (!string.IsNullOrEmpty(message.IdempotencyKey))
            {
                var exists = await db.OutboxMessages.AnyAsync(
                    m => m.Topic == message.Topic && m.IdempotencyKey == message.IdempotencyKey,
                    cancellationToken);

                if (exists)
                {
                    _logger.LogDebug(
                        "Duplicate outbox message for topic {Topic} with idempotency key {IdempotencyKey}, skipping",
                        message.Topic, message.IdempotencyKey);
                    return;
                }
            }

            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Topic = message.Topic,
                Body = message.Body,
                IdempotencyKey = message.IdempotencyKey,
                CreatedUtc = message.CreatedUtc,
                Status = (int)MessageStatus.Pending,
                RetryCount = 0,
            });
            await db.SaveChangesAsync(cancellationToken);
        });
        scope.Complete();
    }

    public async Task<OutboxMessage?> ClaimNextAsync(
        IReadOnlyList<string> topics, string instanceId, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        var staleThreshold = DateTime.UtcNow - _options.Value.StaleClaimThreshold;

        var result = await scope.ExecuteWithContextAsync<OutboxMessage?>(async db =>
        {
            // Find the oldest message that is either:
            // - Pending (new message)
            // - Failed with NextRetryUtc in the past (retry)
            // - Stale claim (claimed too long ago, likely crashed instance)
            var now = DateTime.UtcNow;

            var entity = await db.OutboxMessages
                .Where(m => topics.Contains(m.Topic) &&
                    (
                        // New pending messages
                        (m.Status == (int)MessageStatus.Pending && m.ClaimedByInstance == null) ||
                        // Failed messages ready for retry
                        (m.Status == (int)MessageStatus.Failed && m.ClaimedByInstance == null && m.NextRetryUtc != null && m.NextRetryUtc <= now) ||
                        // Stale claims (crashed instance)
                        (m.Status == (int)MessageStatus.Processing && m.ClaimedUtc != null && m.ClaimedUtc < staleThreshold)
                    ))
                .OrderBy(m => m.CreatedUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                return null;
            }

            // Claim it
            entity.ClaimedByInstance = instanceId;
            entity.ClaimedUtc = now;
            entity.Status = (int)MessageStatus.Processing;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another instance claimed it first — that's fine.
                return null;
            }

            return entity.ToOutboxMessage();
        });

        scope.Complete();
        return result;
    }

    public async Task MarkCompletedAsync(long messageId, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            var entity = await db.OutboxMessages.FindAsync([messageId], cancellationToken);
            if (entity is not null)
            {
                entity.Status = (int)MessageStatus.Completed;
                await db.SaveChangesAsync(cancellationToken);
            }
        });
        scope.Complete();
    }

    public async Task MarkFailedAsync(long messageId, string error, DateTime? nextRetryUtc, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            var entity = await db.OutboxMessages.FindAsync([messageId], cancellationToken);
            if (entity is not null)
            {
                entity.Status = (int)MessageStatus.Failed;
                entity.Error = error;
                entity.NextRetryUtc = nextRetryUtc;
                entity.RetryCount++;
                entity.ClaimedByInstance = null;
                entity.ClaimedUtc = null;
                await db.SaveChangesAsync(cancellationToken);
            }
        });
        scope.Complete();
    }

    public async Task MarkDeadLetteredAsync(long messageId, string error, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            var entity = await db.OutboxMessages.FindAsync([messageId], cancellationToken);
            if (entity is not null)
            {
                entity.Status = (int)MessageStatus.DeadLettered;
                entity.Error = error;
                entity.ClaimedByInstance = null;
                entity.ClaimedUtc = null;
                await db.SaveChangesAsync(cancellationToken);
            }
        });
        scope.Complete();
    }

    public async Task CleanupCompletedAsync(TimeSpan olderThan, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();
        var threshold = DateTime.UtcNow - olderThan;
        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            await db.OutboxMessages
                .Where(m => m.Status == (int)MessageStatus.Completed && m.CreatedUtc < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        });
        scope.Complete();
    }

    public async Task<OutboxStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var stats = await scope.ExecuteWithContextAsync(async db =>
        {
            var counts = await db.OutboxMessages
                .GroupBy(m => m.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return new OutboxStats
            {
                Pending = counts.FirstOrDefault(c => c.Status == (int)MessageStatus.Pending)?.Count ?? 0,
                Processing = counts.FirstOrDefault(c => c.Status == (int)MessageStatus.Processing)?.Count ?? 0,
                Failed = counts.FirstOrDefault(c => c.Status == (int)MessageStatus.Failed)?.Count ?? 0,
                DeadLettered = counts.FirstOrDefault(c => c.Status == (int)MessageStatus.DeadLettered)?.Count ?? 0,
            };
        });

        scope.Complete();
        return stats;
    }
}
