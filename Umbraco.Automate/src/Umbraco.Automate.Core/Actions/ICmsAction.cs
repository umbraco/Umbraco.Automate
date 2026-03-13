namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Marker interface for actions that modify CMS entities.
/// When implemented, the <see cref="Middleware.AuditTrailMiddleware"/> writes
/// a structured audit entry after the action completes successfully.
/// </summary>
public interface ICmsAction;
