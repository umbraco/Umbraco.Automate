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
    /// Builds an idempotency key of the form <c>{alias}:{contentKey}:v{versionId}</c>.
    /// </summary>
    /// <param name="alias">The trigger alias (e.g. <c>umbracoAutomate.contentPublished</c>).</param>
    /// <param name="contentKey">The Umbraco content item key.</param>
    /// <param name="versionId">
    /// The CMS version id that represents the state being signalled — typically
    /// <c>IContent.PublishedVersionId</c> for publish events and <c>IContent.VersionId</c>
    /// for save/unpublish events.
    /// </param>
    public static string ForContentEvent(string alias, Guid contentKey, int versionId)
        => $"{alias}:{contentKey}:v{versionId}";
}
