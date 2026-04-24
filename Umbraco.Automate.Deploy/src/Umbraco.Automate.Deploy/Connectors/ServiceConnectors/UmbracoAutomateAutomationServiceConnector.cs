using System.Runtime.CompilerServices;
using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Service connector for Automations, responsible for synchronizing
/// automation entities during deploy operations. Resolves Workspace and
/// Connection dependencies.
/// </summary>
[UdiDefinition(UmbracoAutomateDeployConstants.UdiEntityType.Automation, UdiType.GuidUdi)]
public class UmbracoAutomateAutomationServiceConnector(
    IAutomationService automationService,
    IWorkspaceService workspaceService,
    ActionCollection actionCollection,
    TriggerCollection triggerCollection,
    ISensitiveSettingsStripper sensitiveStripper,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateAutomationArtifact, Automation>(settingsAccessor)
{
    /// <inheritdoc />
    protected override int[] ProcessPasses => [6];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors =>
    [
        Constants.DeploySelector.This,
        Constants.DeploySelector.ThisAndDescendants,
        Constants.DeploySelector.DescendantsOfThis,
    ];

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

        // Workspace dependency — the automation cannot deploy without its workspace.
        var workspaceUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, entity.WorkspaceId);
        dependencies.Add(new UmbracoAutomateArtifactDependency(workspaceUdi, ArtifactDependencyMode.Match));

        // Group dependency — when the automation lives in a folder, that folder must exist
        // in the target environment before the automation can land in it.
        if (entity.GroupId.HasValue)
        {
            var groupUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, entity.GroupId.Value);
            dependencies.Add(new UmbracoAutomateArtifactDependency(groupUdi, ArtifactDependencyMode.Match));
        }

        // Connection dependencies — each step that references a connection needs that
        // connection to exist in the target environment, otherwise the step would land
        // with a dangling ConnectionId.
        foreach (var connectionId in entity.Steps
            .Select(s => s.ConnectionId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct())
        {
            var connectionUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connectionId);
            dependencies.Add(new UmbracoAutomateArtifactDependency(connectionUdi, ArtifactDependencyMode.Match));
        }

        // Strip sensitive trigger/step settings before serializing — the artifact will
        // be written to disk and committed to source control by Deploy consumers.
        var strippedTrigger = sensitiveStripper.StripTrigger(entity.Trigger);
        var strippedSteps = sensitiveStripper.StripSteps(entity.Steps);

        var artifact = new AutomateAutomationArtifact(udi, dependencies)
        {
            Alias = entity.Alias,
            Name = entity.Name,
            Description = entity.Description,
            WorkspaceUdi = workspaceUdi,
            GroupId = entity.GroupId,
            Trigger = strippedTrigger != null ? JsonSerializer.SerializeToElement(strippedTrigger) : null,
            Steps = strippedSteps.Count > 0 ? JsonSerializer.SerializeToElement(strippedSteps) : null,
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
            case 6:
                await Pass6Async(state, cancellationToken);
                break;
        }
    }

    private async Task Pass6Async(
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

        // Trigger and step actions are contributed via DI (ITrigger/IAction). If the package
        // that contributes one isn't installed on the target, the automation would land with
        // references to unknown aliases and fail silently at runtime. Fail the deploy now
        // with a message pointing at the missing package.
        if (trigger is not null && triggerCollection.GetByAlias(trigger.TriggerAlias) is null)
        {
            throw new InvalidOperationException(
                $"Target site does not contain a trigger with alias '{trigger.TriggerAlias}' (automation '{artifact.Name}'). " +
                "Ensure the package providing this trigger is installed on the target.");
        }

        foreach (var step in steps)
        {
            if (actionCollection.GetByAlias(step.ActionAlias) is null)
            {
                throw new InvalidOperationException(
                    $"Target site does not contain an action with alias '{step.ActionAlias}' (automation '{artifact.Name}', step '{step.Name}'). " +
                    "Ensure the package providing this action is installed on the target.");
            }
        }

        if (state.Entity != null)
        {
            // Update existing automation — preserve the target environment's lifecycle state
            // (published/draft, enabled/disabled) so redeploys don't knock a live automation
            // back to draft. Only the content (alias, steps, trigger, etc.) is replaced.
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

            state.Entity = await automationService.UpdateAutomationAsync(automation, cancellationToken: cancellationToken);
        }
        else
        {
            // Create new automation as disabled draft for safety — an operator must explicitly
            // enable/publish after the first deploy. Preserve the artifact UDI as the entity ID
            // so redeployment of the same artifact stays idempotent.
            var automation = new Automation
            {
                Id = state.Artifact.Udi.Guid,
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
