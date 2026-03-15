using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Service for managing automation lifecycle (CRUD, publish, enable/disable).
/// </summary>
public interface IAutomationService
{
    /// <summary>
    /// Gets an automation by its unique ID.
    /// </summary>
    Task<Automation?> GetAutomationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an automation by its alias.
    /// </summary>
    Task<Automation?> GetAutomationByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all automations.
    /// </summary>
    Task<IEnumerable<Automation>> GetAllAutomationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of automations, optionally scoped to specific workspace IDs.
    /// </summary>
    /// <param name="filter">Optional name/alias filter.</param>
    /// <param name="workspaceIds">When provided, only returns automations in these workspaces. Pass <c>null</c> to return all.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Maximum number of items to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="groupId">
    /// When not null, filters by group.
    /// Pass <see cref="Guid.Empty"/> to get root-level automations (those with no group).
    /// </param>
    Task<(IEnumerable<Automation> Items, int Total)> GetAutomationsPagedAsync(
        string? filter = null,
        IReadOnlySet<Guid>? workspaceIds = null,
        Guid? groupId = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new automation.
    /// </summary>
    Task<Automation> CreateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing automation.
    /// </summary>
    Task<Automation> UpdateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an automation, making its current draft the active version.
    /// </summary>
    Task<Automation> PublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublishes an automation, setting it to inactive.
    /// </summary>
    Task<Automation> UnpublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an automation and all its runs.
    /// </summary>
    Task<bool> DeleteAutomationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the version history for an automation.
    /// </summary>
    /// <param name="automationId">The automation ID.</param>
    /// <param name="skip">Number of versions to skip.</param>
    /// <param name="take">Maximum number of versions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated version history (ordered by version descending) and the total count.</returns>
    Task<(IEnumerable<EntityVersion> Items, int Total)> GetAutomationVersionHistoryAsync(
        Guid automationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version snapshot of an automation.
    /// </summary>
    /// <param name="automationId">The automation ID.</param>
    /// <param name="version">The version number to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The automation at that version, or null if not found.</returns>
    Task<Automation?> GetAutomationVersionSnapshotAsync(
        Guid automationId,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the published version references for all automations that have a published version.
    /// Returns lightweight (Id, PublishedVersion) pairs without loading full entities.
    /// </summary>
    Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back an automation to a previous version, creating a new draft version with that state.
    /// </summary>
    /// <param name="automationId">The automation ID.</param>
    /// <param name="targetVersion">The version to rollback to.</param>
    /// <param name="userId">The user performing the rollback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated automation at the new version.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the automation or target version is not found.</exception>
    Task<Automation> RollbackAutomationAsync(
        Guid automationId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports an automation as a portable model, stripping sensitive fields and replacing
    /// environment-specific IDs with aliases.
    /// </summary>
    /// <param name="automationId">The automation to export.</param>
    /// <param name="options">Options controlling which optional sections to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The export model, or null if the automation was not found.</returns>
    Task<AutomationExportModel?> ExportAutomationAsync(
        Guid automationId,
        AutomationExportOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an export model against the target environment without creating anything.
    /// </summary>
    /// <param name="exportModel">The export model to validate.</param>
    /// <param name="workspaceId">The target workspace to import into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AutomationImportResult> ValidateImportAsync(
        AutomationExportModel exportModel,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports an automation from an export model into a target workspace.
    /// The automation is created as Draft and disabled.
    /// </summary>
    /// <param name="exportModel">The export model to import.</param>
    /// <param name="workspaceId">The target workspace to import into.</param>
    /// <param name="userId">The user performing the import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AutomationImportResult> ImportAutomationAsync(
        AutomationExportModel exportModel,
        Guid workspaceId,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
