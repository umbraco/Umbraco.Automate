using System.Reflection;
using System.Text.RegularExpressions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Default implementation of <see cref="IAutomationService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class AutomationService : IAutomationService
{
    /// <summary>
    /// The current format version for exported automations.
    /// </summary>
    internal const string CurrentExportFormatVersion = "1.0";

    private static readonly string ProductVersion = typeof(AutomationService)
        .Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";

    private const string EntityTypeName = "Automation";

    private readonly IAutomationRepository _automationRepository;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IEntityVersionService _versionService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IConnectionService _connectionService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly ControlFlowCollection _controlFlows;
    private readonly ISensitiveSettingsStripper _sensitiveStripper;

    public AutomationService(
        IAutomationRepository automationRepository,
        IAutomationRunRepository runRepository,
        IEntityVersionService versionService,
        IWorkspaceService workspaceService,
        IConnectionService connectionService,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory,
        ActionCollection actions,
        TriggerCollection triggers,
        ControlFlowCollection controlFlows,
        ISensitiveSettingsStripper sensitiveStripper)
    {
        _automationRepository = automationRepository;
        _runRepository = runRepository;
        _versionService = versionService;
        _workspaceService = workspaceService;
        _connectionService = connectionService;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
        _actions = actions;
        _triggers = triggers;
        _controlFlows = controlFlows;
        _sensitiveStripper = sensitiveStripper;
    }

    public Task<Automation?> GetAutomationAsync(Guid id, CancellationToken cancellationToken = default)
        => _automationRepository.GetAsync(id, cancellationToken);

    public Task<Automation?> GetAutomationByAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _automationRepository.GetByAliasAsync(alias, cancellationToken);

    public Task<IEnumerable<Automation>> GetAllAutomationsAsync(CancellationToken cancellationToken = default)
        => _automationRepository.GetAllAsync(cancellationToken);

    public Task<(IEnumerable<Automation> Items, int Total)> GetAutomationsPagedAsync(
        string? filter = null,
        IReadOnlySet<Guid>? workspaceIds = null,
        Guid? groupId = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _automationRepository.GetPagedAsync(filter, workspaceIds, groupId, skip, take, cancellationToken);

    public Task<bool> ExistsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => _automationRepository.ExistsByWorkspaceAsync(workspaceId, cancellationToken);

    public async Task<Automation> CreateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (automation.Id == Guid.Empty)
        {
            automation.Id = Guid.NewGuid();
        }

        EnsureStepAliases(automation);

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new AutomationSavingNotification(automation, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Automation creation was cancelled by a notification handler.");
        }

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, "Created", cancellationToken);

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Automation> UpdateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        EnsureStepAliases(automation);

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new AutomationSavingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(savingNotification))
        {
            throw new OperationCanceledException("Automation update was cancelled by a notification handler.");
        }

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, cancellationToken: cancellationToken);

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Automation> PublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{id}' not found.");

        // Publish-time validation.
        await ValidateForPublishAsync(automation, cancellationToken);

        var eventMessages = _eventMessagesFactory.Get();

        var publishingNotification = new AutomationPublishingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(publishingNotification))
        {
            throw new OperationCanceledException("Automation publish was cancelled by a notification handler.");
        }

        automation.PublishedVersion = automation.Version;

        // Only enable on first publish (Draft → Published). Re-publishing an already-published
        // automation preserves the user's enabled/disabled choice.
        if (automation.Status != AutomationStatus.Published)
        {
            automation.IsEnabled = true;
        }

        automation.Status = AutomationStatus.Published;

        var saved = await _automationRepository.SaveMetadataAsync(automation, userId, cancellationToken);

        scope.Notifications.Publish(new AutomationPublishedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    private async Task ValidateForPublishAsync(Automation automation, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // 1. Trigger must be configured.
        if (automation.Trigger is null)
        {
            errors.Add("A trigger must be configured before publishing.");
        }

        // 2. Workspace must exist and have a valid service account.
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            errors.Add($"Workspace '{automation.WorkspaceId}' not found.");
        }
        else
        {
            if (workspace.ServiceAccountKey == Guid.Empty)
            {
                errors.Add("The workspace's service account is not configured.");
            }

            // 3. All step connections must be allowed by the workspace.
            var stepConnectionIds = automation.Steps
                .Where(s => s.ConnectionId.HasValue)
                .Select(s => s.ConnectionId!.Value)
                .Distinct()
                .ToList();

            if (stepConnectionIds.Count > 0)
            {
                var allowedSet = workspace.AllowedConnections.ToHashSet();
                var disallowed = stepConnectionIds.Where(id => !allowedSet.Contains(id)).ToList();

                foreach (var connectionId in disallowed)
                {
                    errors.Add($"Connection '{connectionId}' is not allowed in workspace '{workspace.Name}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new AutomationValidationException(
                $"Cannot publish automation '{automation.Name}'.",
                errors);
        }
    }

    public async Task<Automation> UnpublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{id}' not found.");

        if (automation.Status != AutomationStatus.Published)
        {
            throw new AutomationValidationException(
                $"Cannot unpublish automation '{automation.Name}'.",
                [$"Only published automations can be unpublished (current status: {automation.Status})."]);
        }

        var eventMessages = _eventMessagesFactory.Get();

        var unpublishingNotification = new AutomationUnpublishingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(unpublishingNotification))
        {
            throw new OperationCanceledException("Automation unpublish was cancelled by a notification handler.");
        }

        automation.Status = AutomationStatus.Inactive;

        var saved = await _automationRepository.SaveMetadataAsync(automation, userId, cancellationToken);

        scope.Notifications.Publish(new AutomationUnpublishedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<bool> DeleteAutomationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken);
        if (automation is null)
        {
            return false;
        }

        var eventMessages = _eventMessagesFactory.Get();

        var deletingNotification = new AutomationDeletingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(deletingNotification))
        {
            throw new OperationCanceledException("Automation deletion was cancelled by a notification handler.");
        }

        await _runRepository.DeleteByAutomationAsync(id, cancellationToken);
        await _versionService.DeleteVersionsAsync(id, EntityTypeName, cancellationToken);
        var deleted = await _automationRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new AutomationDeletedNotification(automation, eventMessages));
        }

        scope.Complete();
        return deleted;
    }

    public Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default)
        => _automationRepository.GetPublishedVersionReferencesAsync(cancellationToken);

    public Task<(IEnumerable<EntityVersion> Items, int Total)> GetAutomationVersionHistoryAsync(
        Guid automationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => _versionService.GetVersionHistoryAsync(automationId, EntityTypeName, skip, take, cancellationToken);

    public Task<Automation?> GetAutomationVersionSnapshotAsync(
        Guid automationId,
        int version,
        CancellationToken cancellationToken = default)
        => _versionService.GetVersionSnapshotAsync<Automation>(automationId, version, cancellationToken);

    public async Task<Automation> RollbackAutomationAsync(
        Guid automationId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _versionService.GetVersionSnapshotAsync<Automation>(
            automationId, targetVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Version {targetVersion} not found for automation '{automationId}'.");

        // Rollback is a draft-only operation: restore content fields from the target version
        // into a new draft. Lifecycle state (PublishedVersion, Status, IsEnabled) is preserved
        // from the current entity — the user must explicitly re-publish if desired.
        var current = await _automationRepository.GetAsync(automationId, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{automationId}' not found.");

        current.Name = snapshot.Name;
        current.Alias = snapshot.Alias;
        current.Description = snapshot.Description;
        current.Trigger = snapshot.Trigger;
        current.Steps = snapshot.Steps;
        current.Connections = snapshot.Connections;
        current.CanvasState = snapshot.CanvasState;

        return await UpdateAutomationAsync(current, userId, cancellationToken);
    }

    #region Import / Export

    public async Task<AutomationExportModel?> ExportAutomationAsync(
        Guid automationId,
        AutomationExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AutomationExportOptions();

        var automation = await _automationRepository.GetAsync(automationId, cancellationToken);
        if (automation is null)
        {
            return null;
        }

        var connectionReferences = new List<ConnectionReferenceModel>();
        var exportSteps = new List<ExportStepModel>();

        foreach (var step in automation.Steps)
        {
            var exportStep = await BuildExportStepAsync(step, connectionReferences, cancellationToken);
            exportSteps.Add(exportStep);
        }

        var exportTrigger = _sensitiveStripper.StripTrigger(automation.Trigger);

        return new AutomationExportModel
        {
            FormatVersion = CurrentExportFormatVersion,
            ExportedAt = DateTime.UtcNow,
            ExportedFrom = new ExportSourceModel
            {
                Product = "Umbraco.Automate",
                Version = ProductVersion,
            },
            Automation = new AutomationExportDefinition
            {
                Id = automation.Id,
                Alias = automation.Alias,
                Name = automation.Name,
                Description = automation.Description,
                Trigger = exportTrigger,
                Steps = exportSteps,
                Connections = automation.Connections,
                CanvasState = automation.CanvasState,
                NotificationSettings = options.IncludeNotifications ? automation.NotificationSettings : null,
            },
            ConnectionReferences = connectionReferences,
        };
    }

    public async Task<AutomationImportResult> ValidateImportAsync(
        AutomationExportModel exportModel,
        Guid workspaceId,
        Guid? existingAutomationId = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateFormatVersion(exportModel, errors);
        ValidateExportHasId(exportModel, errors);

        if (errors.Count > 0)
        {
            return new AutomationImportResult { Success = false, Errors = errors };
        }

        await ValidateWorkspaceExistsAsync(workspaceId, errors, cancellationToken);
        ValidateProviders(exportModel, errors);
        var resolvedConnections = await ResolveImportConnectionsAsync(exportModel, errors, cancellationToken);
        await ValidateWorkspaceAllowsConnectionsAsync(workspaceId, resolvedConnections, errors, cancellationToken);
        await CheckImportIdentityAsync(exportModel, existingAutomationId, errors, cancellationToken);
        CollectSensitiveFieldWarnings(exportModel, warnings);

        return new AutomationImportResult
        {
            Success = errors.Count == 0,
            AutomationAlias = exportModel.Automation.Alias,
            Errors = errors,
            Warnings = warnings,
        };
    }

    public async Task<AutomationImportResult> ImportAutomationAsync(
        AutomationExportModel exportModel,
        Guid workspaceId,
        Guid? existingAutomationId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateImportAsync(exportModel, workspaceId, existingAutomationId, cancellationToken);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        var connectionAliasToId = await BuildConnectionAliasMapAsync(exportModel, cancellationToken);
        var def = exportModel.Automation;

        var steps = def.Steps.Select(s => new StepConfiguration
        {
            Id = s.Id,
            ActionAlias = s.ActionAlias,
            Name = s.Name,
            Alias = s.Alias,
            ConnectionId = s.ConnectionAlias is not null && connectionAliasToId.TryGetValue(s.ConnectionAlias, out var connId)
                ? connId
                : null,
            Settings = s.Settings,
            InputMappings = s.InputMappings,
            Position = s.Position,
            ErrorBehavior = s.ErrorBehavior,
            RetryInterval = s.RetryInterval,
            MaxRetries = s.MaxRetries,
        }).ToList();

        if (existingAutomationId is null)
        {
            var created = await CreateImportedAutomationAsync(def, steps, workspaceId, userId, cancellationToken);
            return new AutomationImportResult
            {
                Success = true,
                AutomationId = created.Id,
                AutomationAlias = created.Alias,
                Warnings = validationResult.Warnings,
            };
        }

        var updated = await OverwriteImportedAutomationAsync(existingAutomationId.Value, def, steps, userId, cancellationToken);
        return new AutomationImportResult
        {
            Success = true,
            AutomationId = updated.Id,
            AutomationAlias = updated.Alias,
            Warnings = validationResult.Warnings,
        };
    }

    private async Task<Automation> CreateImportedAutomationAsync(
        AutomationExportDefinition def,
        List<StepConfiguration> steps,
        Guid workspaceId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var automation = new Automation
        {
            Alias = def.Alias,
            Name = def.Name,
            Description = def.Description,
            IsEnabled = false,
            Status = AutomationStatus.Draft,
            WorkspaceId = workspaceId,
            Trigger = def.Trigger,
            Steps = steps,
            Connections = def.Connections.ToList(),
            CanvasState = def.CanvasState,
            NotificationSettings = def.NotificationSettings,
        };
        automation.Id = def.Id;

        return await CreateAutomationAsync(automation, userId, cancellationToken);
    }

    private async Task<Automation> OverwriteImportedAutomationAsync(
        Guid existingAutomationId,
        AutomationExportDefinition def,
        List<StepConfiguration> steps,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var existing = await _automationRepository.GetAsync(existingAutomationId, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{existingAutomationId}' not found.");

        existing.Alias = def.Alias;
        existing.Name = def.Name;
        existing.Description = def.Description;
        existing.Trigger = def.Trigger;
        existing.Steps = steps;
        existing.Connections = def.Connections.ToList();
        existing.CanvasState = def.CanvasState;
        existing.NotificationSettings = def.NotificationSettings;

        return await UpdateAutomationAsync(existing, userId, cancellationToken);
    }

    private async Task<ExportStepModel> BuildExportStepAsync(
        StepConfiguration step,
        List<ConnectionReferenceModel> connectionReferences,
        CancellationToken cancellationToken)
    {
        string? connectionAlias = null;

        if (step.ConnectionId.HasValue)
        {
            var connection = await _connectionService.GetConnectionAsync(step.ConnectionId.Value, cancellationToken);
            if (connection is not null)
            {
                connectionAlias = connection.Alias;

                if (!connectionReferences.Any(r => r.Alias == connection.Alias))
                {
                    connectionReferences.Add(new ConnectionReferenceModel
                    {
                        Alias = connection.Alias,
                        Type = connection.Type,
                        Name = connection.Name,
                    });
                }
            }
        }

        var strippedSettings = _sensitiveStripper.StripStepSettings(step.ActionAlias, step.Settings);

        return new ExportStepModel
        {
            Id = step.Id,
            ActionAlias = step.ActionAlias,
            Name = step.Name,
            Alias = step.Alias,
            ConnectionAlias = connectionAlias,
            Settings = strippedSettings,
            InputMappings = step.InputMappings,
            Position = step.Position,
            ErrorBehavior = step.ErrorBehavior,
            RetryInterval = step.RetryInterval,
            MaxRetries = step.MaxRetries,
        };
    }

    private EditableModelSchema? GetStepTypeSchema(string actionAlias)
    {
        var stepType = (IStepType?)_actions.GetByAlias(actionAlias) ?? _controlFlows.GetByAlias(actionAlias);
        return stepType?.GetSettingsSchema();
    }

    private static void ValidateFormatVersion(AutomationExportModel exportModel, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(exportModel.FormatVersion))
        {
            errors.Add("The export file is missing a format version.");
            return;
        }

        if (exportModel.FormatVersion != CurrentExportFormatVersion)
        {
            errors.Add($"Unsupported format version '{exportModel.FormatVersion}'. Expected '{CurrentExportFormatVersion}'.");
        }
    }

    private async Task ValidateWorkspaceExistsAsync(Guid workspaceId, List<string> errors, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            errors.Add($"Target workspace '{workspaceId}' was not found.");
        }
    }

    private void ValidateProviders(AutomationExportModel exportModel, List<string> errors)
    {
        var def = exportModel.Automation;

        if (def.Trigger is not null)
        {
            var trigger = _triggers.GetByAlias(def.Trigger.TriggerAlias);
            if (trigger is null)
            {
                errors.Add($"Trigger type '{def.Trigger.TriggerAlias}' is not registered in this environment.");
            }
        }

        foreach (var step in def.Steps)
        {
            IStepType? stepType = _actions.GetByAlias(step.ActionAlias) ?? (IStepType?)_controlFlows.GetByAlias(step.ActionAlias);
            if (stepType is null)
            {
                errors.Add($"Step type '{step.ActionAlias}' (step '{step.Name}') is not registered in this environment.");
            }
        }
    }

    private async Task<Dictionary<string, Guid>> ResolveImportConnectionsAsync(
        AutomationExportModel exportModel,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var connRef in exportModel.ConnectionReferences)
        {
            var connection = await _connectionService.GetConnectionByAliasAsync(connRef.Alias, cancellationToken);
            if (connection is null)
            {
                errors.Add($"Connection '{connRef.Alias}' (type: {connRef.Type}) was not found in this environment.");
            }
            else
            {
                resolved[connRef.Alias] = connection.Id;
            }
        }

        return resolved;
    }

    private async Task ValidateWorkspaceAllowsConnectionsAsync(
        Guid workspaceId,
        Dictionary<string, Guid> resolvedConnections,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (resolvedConnections.Count == 0)
        {
            return;
        }

        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            return; // Already reported by ValidateWorkspaceExistsAsync.
        }

        foreach (var (alias, connectionId) in resolvedConnections)
        {
            if (!workspace.AllowedConnections.Contains(connectionId))
            {
                errors.Add($"Connection '{alias}' is not allowed in workspace '{workspace.Name}'. Add it to the workspace's allowed connections first.");
            }
        }
    }

    private static void ValidateExportHasId(AutomationExportModel exportModel, List<string> errors)
    {
        if (exportModel.Automation.Id == Guid.Empty)
        {
            errors.Add("The export file is missing an automation ID. Re-export from a newer version of Umbraco.Automate.");
        }
    }

    private async Task CheckImportIdentityAsync(
        AutomationExportModel exportModel,
        Guid? existingAutomationId,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var fileId = exportModel.Automation.Id;
        var alias = exportModel.Automation.Alias;

        if (existingAutomationId is null)
        {
            // Create path: both id and alias must not already exist.
            var byId = await _automationRepository.GetAsync(fileId, cancellationToken);
            if (byId is not null)
            {
                errors.Add($"An automation with ID '{fileId}' already exists. Use the overwrite endpoint to update it.");
            }

            var byAlias = await _automationRepository.GetByAliasAsync(alias, cancellationToken);
            if (byAlias is not null && byAlias.Id != fileId)
            {
                errors.Add($"An automation with alias '{alias}' already exists (ID: {byAlias.Id}).");
            }

            return;
        }

        // Overwrite path: the file's id must match the target id.
        if (fileId != existingAutomationId.Value)
        {
            errors.Add($"The export file's automation ID '{fileId}' does not match the target automation ID '{existingAutomationId.Value}'.");
            return;
        }

        var target = await _automationRepository.GetAsync(existingAutomationId.Value, cancellationToken);
        if (target is null)
        {
            errors.Add($"Target automation '{existingAutomationId.Value}' was not found.");
            return;
        }

        // Alias uniqueness still applies — but not against the automation being overwritten.
        var byAliasForUpdate = await _automationRepository.GetByAliasAsync(alias, cancellationToken);
        if (byAliasForUpdate is not null && byAliasForUpdate.Id != existingAutomationId.Value)
        {
            errors.Add($"An automation with alias '{alias}' already exists (ID: {byAliasForUpdate.Id}).");
        }
    }

    private void CollectSensitiveFieldWarnings(AutomationExportModel exportModel, List<string> warnings)
    {
        var def = exportModel.Automation;

        if (def.Trigger is not null)
        {
            var schema = _triggers.GetByAlias(def.Trigger.TriggerAlias)?.GetSettingsSchema();
            AddSensitiveFieldWarning(schema, "trigger", def.Trigger.TriggerAlias, warnings);
        }

        foreach (var step in def.Steps)
        {
            var schema = GetStepTypeSchema(step.ActionAlias);
            AddSensitiveFieldWarning(schema, "step", step.Name, warnings);
        }
    }

    private static void AddSensitiveFieldWarning(
        EditableModelSchema? schema,
        string entityType,
        string entityName,
        List<string> warnings)
    {
        if (schema is null)
        {
            return;
        }

        var sensitiveFields = schema.Fields.Where(f => f.IsSensitive).Select(f => f.Label).ToList();
        if (sensitiveFields.Count > 0)
        {
            warnings.Add($"The {entityType} '{entityName}' has sensitive fields that need to be configured after import: {string.Join(", ", sensitiveFields)}.");
        }
    }

    private async Task<Dictionary<string, Guid>> BuildConnectionAliasMapAsync(
        AutomationExportModel exportModel,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var connRef in exportModel.ConnectionReferences)
        {
            var connection = await _connectionService.GetConnectionByAliasAsync(connRef.Alias, cancellationToken);
            if (connection is not null)
            {
                map[connRef.Alias] = connection.Id;
            }
        }

        return map;
    }

    #endregion

    #region Step aliases

    private static readonly Regex StepAliasRegex = new("^[a-zA-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedBindingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "trigger",
        "steps",
        "loop",
        "previous",
    };

    /// <summary>
    /// Validates explicit step aliases and auto-generates aliases for steps that lack one.
    /// Mutates <paramref name="automation"/> in-place.
    /// </summary>
    private static void EnsureStepAliases(Automation automation)
    {
        if (automation.Steps.Count == 0)
        {
            return;
        }

        var usedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: validate explicitly set aliases.
        var errors = new List<string>();

        foreach (var step in automation.Steps)
        {
            if (string.IsNullOrEmpty(step.Alias))
            {
                continue;
            }

            ValidateStepAlias(step, errors);

            if (!usedAliases.Add(step.Alias))
            {
                errors.Add($"Duplicate step alias '{step.Alias}' on step '{step.Name}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new AutomationValidationException("Step alias validation failed.", errors);
        }

        // Second pass: auto-generate missing aliases.
        foreach (var step in automation.Steps)
        {
            if (!string.IsNullOrEmpty(step.Alias))
            {
                continue;
            }

            step.Alias = GenerateUniqueAlias(step.ActionAlias, usedAliases);
            usedAliases.Add(step.Alias);
        }
    }

    private static void ValidateStepAlias(StepConfiguration step, List<string> errors)
    {
        if (!StepAliasRegex.IsMatch(step.Alias!))
        {
            errors.Add($"Step '{step.Name}' has invalid alias '{step.Alias}'. Aliases must start with a letter and contain only letters and digits.");
        }

        if (ReservedBindingKeys.Contains(step.Alias!))
        {
            errors.Add($"Step '{step.Name}' uses reserved alias '{step.Alias}'.");
        }

        if (Guid.TryParse(step.Alias, out _))
        {
            errors.Add($"Step '{step.Name}' alias '{step.Alias}' cannot be a GUID.");
        }
    }

    /// <summary>
    /// Generates a unique alias from an action alias (e.g. "umbracoAutomate.httpRequest" → "httpRequest").
    /// Appends an incrementing number if the base name is already taken.
    /// </summary>
    internal static string GenerateUniqueAlias(string actionAlias, ISet<string> usedAliases)
    {
        var baseName = actionAlias;
        var lastDot = actionAlias.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < actionAlias.Length - 1)
        {
            baseName = actionAlias[(lastDot + 1)..];
        }

        if (!usedAliases.Contains(baseName) && !ReservedBindingKeys.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}{i}";
            if (!usedAliases.Contains(candidate) && !ReservedBindingKeys.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to generate unique alias for action '{actionAlias}'.");
    }

    #endregion
}
