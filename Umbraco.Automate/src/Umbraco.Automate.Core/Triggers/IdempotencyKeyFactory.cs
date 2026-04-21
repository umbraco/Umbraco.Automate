using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Builds deterministic idempotency keys for trigger events so the outbox can dedupe
/// true double-fires of the same CMS event (e.g. an Umbraco notification dispatched
/// twice for one publish).
/// </summary>
/// <remarks>
/// The previous implementation bucketed events by a time window, which had two problems:
/// its floor calculation was racy (read <c>DateTime.UtcNow</c> twice, so the computed
/// "window start" varied by a tick per call), and it dropped legitimately-separate
/// events that happened to fall into the same window. Keying on the CMS version id
/// avoids both: two distinct publishes of the same content get distinct version ids
/// (no false dedup), and a duplicate notification for the same publish shares the
/// same version id (true dedup).
/// </remarks>
internal static class IdempotencyKeyFactory
{
    /// <summary>
    /// Builds an idempotency key of the form <c>{alias}:{contentKey}:v{versionId}</c>
    /// for a single-content event whose <paramref name="versionId"/> uniquely identifies
    /// the event (publish, unpublish).
    /// </summary>
    /// <param name="alias">The trigger alias (e.g. <c>umbracoAutomate.contentPublished</c>).</param>
    /// <param name="contentKey">The Umbraco content item key.</param>
    /// <param name="versionId">
    /// The CMS version id that represents the state being signalled — typically
    /// <c>IContent.PublishedVersionId</c>.
    /// </param>
    public static string ForContentEvent(string alias, Guid contentKey, int versionId)
        => $"{alias}:{contentKey}:v{versionId}";

    /// <summary>
    /// Builds an idempotency key of the form <c>{alias}:{contentKey}:v{versionId}:u{ticks}</c>
    /// for a single-content save event. Umbraco does not increment the draft version id
    /// on every save, so <see cref="ForContentEvent"/> would dedupe legitimate sequential
    /// saves of the same draft. Including <see cref="IContent.UpdateDate"/>.Ticks makes
    /// each save distinct while still collapsing a duplicate notification for the same
    /// save (which carries the same UpdateDate).
    /// </summary>
    /// <param name="alias">The trigger alias (e.g. <c>umbracoAutomate.contentSaved</c>).</param>
    /// <param name="contentKey">The Umbraco content item key.</param>
    /// <param name="versionId">The current CMS version id.</param>
    /// <param name="updateDate">The <c>IContent.UpdateDate</c> captured at save time.</param>
    public static string ForContentSaveEvent(string alias, Guid contentKey, int versionId, DateTime updateDate)
        => $"{alias}:{contentKey}:v{versionId}:u{updateDate.Ticks}";

    /// <summary>
    /// Builds an idempotency key for a batch trigger event by hashing the sorted set of
    /// <c>(contentKey, versionId)</c> pairs in the batch. Two duplicate notifications for
    /// the exact same batch produce the same hash and dedupe; any change to the membership
    /// or to any item's version id produces a different hash.
    /// </summary>
    /// <param name="alias">The batch trigger alias (e.g. <c>umbracoAutomate.contentBatchPublished</c>).</param>
    /// <param name="items">The (contentKey, versionId) pairs that make up the batch.</param>
    /// <returns>
    /// A key of the form <c>{alias}:batch:{base64-sha256}</c>, or <c>null</c> if the batch
    /// is empty (the trigger should not have produced an event in that case).
    /// </returns>
    public static string? ForContentBatch(string alias, IReadOnlyCollection<(Guid ContentKey, int VersionId)> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        // Sort so the same batch always hashes the same way regardless of enumeration order.
        var sorted = items
            .OrderBy(x => x.ContentKey)
            .ThenBy(x => x.VersionId)
            .ToList();

        var sb = new StringBuilder(items.Count * 48);
        foreach (var (contentKey, versionId) in sorted)
        {
            sb.Append(contentKey.ToString("N")).Append(':').Append(versionId).Append(';');
        }

        return HashToBatchKey(alias, sb);
    }

    /// <summary>
    /// Builds an idempotency key for a batch save trigger event by hashing the sorted set
    /// of <c>(contentKey, versionId, updateDate)</c> triples. Draft saves don't bump the
    /// version id, so UpdateDate is included to distinguish legitimate sequential saves.
    /// </summary>
    /// <param name="alias">The batch trigger alias (e.g. <c>umbracoAutomate.contentBatchSaved</c>).</param>
    /// <param name="items">The (contentKey, versionId, updateDate) triples that make up the batch.</param>
    /// <returns>
    /// A key of the form <c>{alias}:batch:{base64-sha256}</c>, or <c>null</c> if the batch
    /// is empty.
    /// </returns>
    public static string? ForContentSaveBatch(string alias, IReadOnlyCollection<(Guid ContentKey, int VersionId, DateTime UpdateDate)> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        var sorted = items
            .OrderBy(x => x.ContentKey)
            .ThenBy(x => x.VersionId)
            .ThenBy(x => x.UpdateDate.Ticks)
            .ToList();

        var sb = new StringBuilder(items.Count * 64);
        foreach (var (contentKey, versionId, updateDate) in sorted)
        {
            sb.Append(contentKey.ToString("N"))
                .Append(':').Append(versionId)
                .Append(':').Append(updateDate.Ticks)
                .Append(';');
        }

        return HashToBatchKey(alias, sb);
    }

    private static string HashToBatchKey(string alias, StringBuilder content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()));

        // Base64Url-style: trim padding and replace URL-unsafe chars so the key is portable
        // and short. The full 256-bit hash gives ample collision resistance for our scale.
        var base64 = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"{alias}:batch:{base64}";
    }
}
