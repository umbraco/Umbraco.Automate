using System.Reflection;
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
        ControlFlowCollection controlFlows)
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

    public async Task<Automation> CreateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (automation.Id == Guid.Empty)
        {
            automation.Id = Guid.NewGuid();
        }

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
        automation.Status = AutomationStatus.Published;
        automation.IsEnabled = true;

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

        var eventMessages = _eventMessagesFactory.Get();

        var unpublishingNotification = new AutomationUnpublishingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(unpublishingNotification))
        {
            throw new OperationCanceledException("Automation unpublish was cancelled by a notification handler.");
        }

        automation.Status = AutomationStatus.Inactive;
        automation.IsEnabled = false;

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

        var exportTrigger = StripTriggerSensitiveFields(automation.Trigger);

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
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateFormatVersion(exportModel, errors);

        if (errors.Count > 0)
        {
            return new AutomationImportResult { Success = false, Errors = errors };
        }

        await ValidateWorkspaceExistsAsync(workspaceId, errors, cancellationToken);
        ValidateProviders(exportModel, errors);
        var resolvedConnections = await ResolveImportConnectionsAsync(exportModel, errors, cancellationToken);
        await ValidateWorkspaceAllowsConnectionsAsync(workspaceId, resolvedConnections, errors, cancellationToken);
        await CheckImportAliasConflictAsync(exportModel.Automation.Alias, errors, cancellationToken);
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
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateImportAsync(exportModel, workspaceId, cancellationToken);
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

        var created = await CreateAutomationAsync(automation, userId, cancellationToken);

        return new AutomationImportResult
        {
            Success = true,
            AutomationId = created.Id,
            AutomationAlias = created.Alias,
            Warnings = validationResult.Warnings,
        };
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

        var strippedSettings = StripSensitiveSettings(step.Settings, GetStepTypeSchema(step.ActionAlias));

        return new ExportStepModel
        {
            Id = step.Id,
            ActionAlias = step.ActionAlias,
            Name = step.Name,
            ConnectionAlias = connectionAlias,
            Settings = strippedSettings,
            InputMappings = step.InputMappings,
            Position = step.Position,
            ErrorBehavior = step.ErrorBehavior,
            RetryInterval = step.RetryInterval,
            MaxRetries = step.MaxRetries,
        };
    }

    private TriggerConfiguration? StripTriggerSensitiveFields(TriggerConfiguration? trigger)
    {
        if (trigger is null || trigger.Settings.Count == 0)
        {
            return trigger;
        }

        var schema = _triggers.GetByAlias(trigger.TriggerAlias)?.GetSettingsSchema();
        var strippedSettings = StripSensitiveSettings(trigger.Settings, schema);

        if (strippedSettings == trigger.Settings)
        {
            return trigger;
        }

        return new TriggerConfiguration
        {
            TriggerAlias = trigger.TriggerAlias,
            Settings = strippedSettings,
        };
    }

    private static Dictionary<string, object?> StripSensitiveSettings(
        Dictionary<string, object?> settings,
        EditableModelSchema? schema)
    {
        if (schema is null)
        {
            return settings;
        }

        var sensitiveFields = schema.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.PropertyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sensitiveFields.Count == 0)
        {
            return settings;
        }

        return settings
            .Where(kvp => !sensitiveFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

    private async Task CheckImportAliasConflictAsync(string alias, List<string> errors, CancellationToken cancellationToken)
    {
        var existing = await _automationRepository.GetByAliasAsync(alias, cancellationToken);
        if (existing is not null)
        {
            errors.Add($"An automation with alias '{alias}' already exists (ID: {existing.Id}).");
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
}
