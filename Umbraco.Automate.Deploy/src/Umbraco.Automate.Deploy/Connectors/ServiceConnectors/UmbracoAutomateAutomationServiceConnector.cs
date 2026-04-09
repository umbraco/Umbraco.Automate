using System.Runtime.CompilerServices;
using System.Text.Json;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Service connector for Automations, responsible for synchronizing
/// automation entities during deploy operations. Resolves Workspace dependencies.
/// </summary>
[UdiDefinition(UmbracoAutomateDeployConstants.UdiEntityType.Automation, UdiType.GuidUdi)]
public class UmbracoAutomateAutomationServiceConnector(
    IAutomationService automationService,
    IWorkspaceService workspaceService,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateAutomationArtifact, Automation>(settingsAccessor)
{
    /// <inheritdoc />
    protected override int[] ProcessPasses => [4];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors => ["this", "this-and-descendants", "descendants"];

    /// <inheritdoc />
    protected override string OpenUdiName => "All Umbraco Automate Automations";

    /// <inheritdoc />
    public override string UdiEntityType => UmbracoAutomateDeployConstants.UdiEntityType.Automation;

    /// <inheritdoc />
    public override async Task<Automation?> GetEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => await automationService.GetAutomationAsync(id, cancellationToken);

    /// <inheritdoc />
    public override async IAsyncEnumerable<Automation> GetEntitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var automations = await automationService.GetAllAutomationsAsync(cancellationToken);
        foreach (var automation in automations)
        {
            yield return automation;
        }
    }

    /// <inheritdoc />
    public override string GetEntityName(Automation entity) => entity.Name;

    /// <inheritdoc />
    public override Task<AutomateAutomationArtifact?> GetArtifactAsync(
        GuidUdi udi,
        Automation? entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult<AutomateAutomationArtifact?>(null);
        }

        var dependencies = new ArtifactDependencyCollection();

        // Add Workspace dependency
        var workspaceUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, entity.WorkspaceId);
        dependencies.Add(new UmbracoAutomateArtifactDependency(workspaceUdi, ArtifactDependencyMode.Match));

        var artifact = new AutomateAutomationArtifact(udi, dependencies)
        {
            Alias = entity.Alias,
            Name = entity.Name,
            Description = entity.Description,
            IsEnabled = entity.IsEnabled,
            Status = (int)entity.Status,
            PublishedVersion = entity.PublishedVersion,
            WorkspaceUdi = workspaceUdi,
            GroupId = entity.GroupId,
            Trigger = entity.Trigger != null ? JsonSerializer.SerializeToElement(entity.Trigger) : null,
            Steps = entity.Steps.Count > 0 ? JsonSerializer.SerializeToElement(entity.Steps) : null,
            Connections = entity.Connections.Count > 0 ? JsonSerializer.SerializeToElement(entity.Connections) : null,
            NotificationSettings = entity.NotificationSettings != null ? JsonSerializer.SerializeToElement(entity.NotificationSettings) : null,
            CanvasState = entity.CanvasState,
        };

        return Task.FromResult<AutomateAutomationArtifact?>(artifact);
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(
        ArtifactDeployState<AutomateAutomationArtifact, Automation> state,
        IDeployContext context,
        int pass,
        CancellationToken cancellationToken = default)
    {
        state.NextPass = GetNextPass(pass);

        switch (pass)
        {
            case 4:
                await Pass4Async(state, cancellationToken);
                break;
        }
    }

    private async Task Pass4Async(
        ArtifactDeployState<AutomateAutomationArtifact, Automation> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        // Resolve Workspace UDI to ID
        artifact.WorkspaceUdi.EnsureType(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);

        var workspace = await workspaceService.GetWorkspaceAsync(artifact.WorkspaceUdi.Guid, cancellationToken);
        if (workspace == null)
        {
            throw new InvalidOperationException(
                $"Workspace with ID {artifact.WorkspaceUdi.Guid} not found. Ensure the workspace is deployed before the automation.");
        }

        // Deserialize complex properties
        TriggerConfiguration? trigger = null;
        if (artifact.Trigger.HasValue)
        {
            trigger = artifact.Trigger.Value.Deserialize<TriggerConfiguration>();
        }

        IList<StepConfiguration> steps = [];
        if (artifact.Steps.HasValue)
        {
            steps = artifact.Steps.Value.Deserialize<IList<StepConfiguration>>() ?? [];
        }

        IList<StepConnection> connections = [];
        if (artifact.Connections.HasValue)
        {
            connections = artifact.Connections.Value.Deserialize<IList<StepConnection>>() ?? [];
        }

        AutomationNotificationSettings? notificationSettings = null;
        if (artifact.NotificationSettings.HasValue)
        {
            notificationSettings = artifact.NotificationSettings.Value.Deserialize<AutomationNotificationSettings>();
        }

        if (state.Entity != null)
        {
            // Update existing automation
            var automation = state.Entity;
            automation.Alias = artifact.Alias!;
            automation.Name = artifact.Name;
            automation.Description = artifact.Description;
            automation.WorkspaceId = workspace.Id;
            automation.GroupId = artifact.GroupId;
            automation.Trigger = trigger;
            automation.Steps = steps;
            automation.Connections = connections;
            automation.NotificationSettings = notificationSettings;
            automation.CanvasState = artifact.CanvasState;

            // Safety: import as disabled draft to prevent accidental trigger execution
            automation.IsEnabled = false;
            automation.Status = AutomationStatus.Draft;

            state.Entity = await automationService.UpdateAutomationAsync(automation, cancellationToken: cancellationToken);
        }
        else
        {
            // Create new automation as disabled draft
            var automation = new Automation
            {
                Alias = artifact.Alias!,
                Name = artifact.Name,
                Description = artifact.Description,
                IsEnabled = false,
                Status = AutomationStatus.Draft,
                WorkspaceId = workspace.Id,
                GroupId = artifact.GroupId,
                Trigger = trigger,
                Steps = steps,
                Connections = connections,
                NotificationSettings = notificationSettings,
                CanvasState = artifact.CanvasState,
            };

            state.Entity = await automationService.CreateAutomationAsync(automation, cancellationToken: cancellationToken);
        }
    }
}
