using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.HealthChecks;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.HealthChecks;

/// <summary>
/// Reports published automations whose workspace service account no longer satisfies the
/// section requirements of the trigger or one of the configured actions. Captures drift
/// after a service account was downgraded post-publish: those runs are skipped at dispatch
/// or fail at runtime, but the operator otherwise has no list of which automations need
/// republishing.
/// </summary>
[HealthCheck(
    "B8D4A9C2-6E7F-4A1B-9C8D-2F3E4A5B6C7D",
    "Automation Service-Account Permissions",
    Description = "Checks that every published automation's workspace service account still has the backoffice section access required by its trigger and step actions.",
    Group = "Umbraco Automate")]
public sealed class AutomationPermissionDriftHealthCheck : HealthCheck
{
    private readonly IAutomationService _automationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IWorkspaceServiceAccountResolver _serviceAccountResolver;
    private readonly TriggerCollection _triggers;
    private readonly ActionCollection _actions;
    private readonly ISectionAccessChecker _sectionAccessChecker;
    private readonly ILogger<AutomationPermissionDriftHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutomationPermissionDriftHealthCheck"/> class.
    /// </summary>
    public AutomationPermissionDriftHealthCheck(
        IAutomationService automationService,
        IWorkspaceService workspaceService,
        IWorkspaceServiceAccountResolver serviceAccountResolver,
        TriggerCollection triggers,
        ActionCollection actions,
        ISectionAccessChecker sectionAccessChecker,
        ILogger<AutomationPermissionDriftHealthCheck> logger)
    {
        _automationService = automationService;
        _workspaceService = workspaceService;
        _serviceAccountResolver = serviceAccountResolver;
        _triggers = triggers;
        _actions = actions;
        _sectionAccessChecker = sectionAccessChecker;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<HealthCheckStatus>> GetStatusAsync()
    {
        IEnumerable<Automation> published;
        try
        {
            var all = await _automationService.GetAllAutomationsAsync(CancellationToken.None);
            published = all.Where(a => a.Status == AutomationStatus.Published);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate automations for permission drift check.");
            return [new HealthCheckStatus($"Could not evaluate automations: {ex.Message}")
            {
                ResultType = StatusResultType.Error,
            }];
        }

        // Cache the (workspace, service account) pair per workspaceId so N automations
        // sharing one workspace only pay for one workspace + one user lookup.
        var workspaceCache = new Dictionary<Guid, (Workspace? Workspace, IUser? ServiceAccount)>();
        var violations = new List<string>();

        foreach (var automation in published)
        {
            if (!workspaceCache.TryGetValue(automation.WorkspaceId, out var resolved))
            {
                var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId);
                var user = workspace is null || workspace.ServiceAccountKey == Guid.Empty
                    ? null
                    : await _serviceAccountResolver.GetServiceAccountAsync(workspace.Id);
                resolved = (workspace, user);
                workspaceCache[automation.WorkspaceId] = resolved;
            }

            var (workspaceResolved, serviceAccount) = resolved;
            if (workspaceResolved is null || workspaceResolved.ServiceAccountKey == Guid.Empty)
            {
                continue;
            }

            if (serviceAccount is null)
            {
                violations.Add($"'{automation.Name}' (workspace '{workspaceResolved.Name}') — service account '{workspaceResolved.ServiceAccountKey}' was not found.");
                continue;
            }

            if (automation.Trigger is not null
                && _triggers.GetByAlias(automation.Trigger.TriggerAlias) is { } trigger
                && !_sectionAccessChecker.CanAccess(serviceAccount, trigger))
            {
                violations.Add($"'{automation.Name}' (workspace '{workspaceResolved.Name}') — trigger '{trigger.Name}' requires sections {FormatSections(trigger.RequiredSections)}.");
            }

            foreach (var step in automation.Steps)
            {
                if (_actions.GetByAlias(step.ActionAlias) is { } action
                    && !_sectionAccessChecker.CanAccess(serviceAccount, action))
                {
                    violations.Add($"'{automation.Name}' (workspace '{workspaceResolved.Name}') — step '{step.Name}' uses action '{action.Name}' which requires sections {FormatSections(action.RequiredSections)}.");
                }
            }
        }

        if (violations.Count == 0)
        {
            return [new HealthCheckStatus("All published automations have service accounts with the required section access.")
            {
                ResultType = StatusResultType.Success,
            }];
        }

        var message = new StringBuilder("The following automations have service accounts that no longer satisfy their section requirements. Republish each automation or grant the service account the missing sections:\n");
        foreach (var line in violations)
        {
            message.Append("- ").AppendLine(line);
        }

        return [new HealthCheckStatus(message.ToString())
        {
            ResultType = StatusResultType.Warning,
        }];
    }

    private static string FormatSections(IReadOnlyList<string> sections)
        => sections.Count == 0 ? "(none)" : "(" + string.Join(", ", sections) + ")";
}
