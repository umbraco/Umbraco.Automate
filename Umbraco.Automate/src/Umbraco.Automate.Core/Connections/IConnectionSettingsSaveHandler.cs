namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Inspects, and may rewrite, a connection's settings before <see cref="IConnectionService"/> persists it.
/// Register implementations in DI; every registered handler runs on create, update and rollback, before
/// the <c>ConnectionSavingNotification</c> is published.
/// </summary>
/// <remarks>
/// Use this when a connection type's settings need server-side checks that depend on the previously
/// persisted value — for example, exchanging a short-lived value submitted by the editor for the value
/// that is actually stored. Throw <see cref="ConnectionSettingsValidationException"/> to reject the save.
/// </remarks>
internal interface IConnectionSettingsSaveHandler
{
    /// <summary>
    /// Validates and, where needed, rewrites <see cref="ConnectionSettingsSaveContext.Connection"/>'s settings.
    /// </summary>
    /// <param name="context">The connection being saved and what is currently persisted for it.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="ConnectionSettingsValidationException">The settings must not be saved.</exception>
    Task PrepareSettingsAsync(ConnectionSettingsSaveContext context, CancellationToken cancellationToken);
}
