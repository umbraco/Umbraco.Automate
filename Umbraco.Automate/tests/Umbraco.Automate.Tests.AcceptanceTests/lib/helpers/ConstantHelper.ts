export class ConstantHelper {
  /* Backoffice section tab label. Matched case-insensitively by getByRole, so this is the
   * localised label from lang/en.ts (#uaSections_automate), not the section alias. */
  public static readonly sections = {
    automate: 'Automation'
  };

  /* Section alias and pathname, from src/section/constants.ts in the client. */
  public static readonly section = {
    alias: 'Ua.Section.Automate',
    pathname: 'automation'
  };

  public static readonly api = {
    basePath: '/umbraco/automate/management/api/v1/'
  };

  /* A workspace with this service account key is accepted by the server and is what the client
   * itself scaffolds a new workspace with, but it does not resolve to a user — so automations
   * in that workspace have no execution identity and cannot run. */
  public static readonly emptyGuid = '00000000-0000-0000-0000-000000000000';

  /* Automate's own custom elements. These are the stable selectors: Automate components do
   * not carry the CMS `data-mark` attribute, so getByTestId does not reach them. */
  public static readonly elements = {
    dashboard: 'ua-automate-dashboard',
    automationTree: 'umb-tree',
    nodePickerModal: 'ua-node-picker-modal',
    nodeSettingsModal: 'ua-node-settings-modal',
    bindingPickerModal: 'ua-binding-picker-modal',
    runsTable: 'ua-runs-table',
    runDetailModal: 'ua-run-detail-modal',
    approvalDashboard: 'ua-approval-dashboard',
    approvalDecisionModal: 'ua-approval-decision-modal',
    edgeFilterModal: 'ua-edge-filter-modal',
    connectionTypePickerModal: 'ua-connection-type-picker-modal',
    oauthEditor: 'umb-automate-property-editor-ui-oauth'
  };

  /* Trigger and step type aliases, as the catalogue API reports them. */
  public static readonly triggers = {
    manual: 'umbracoAutomate.manual'
  };

  public static readonly actions = {
    delay: 'umbracoAutomate.delay',
    logMessage: 'umbracoAutomate.logMessage',
    requestApproval: 'umbracoAutomate.requestApproval',
    runScript: 'umbracoAutomate.runScript',
    setVariable: 'umbracoAutomate.setVariable',
    slackSendMessage: 'slack.sendMessage',
    if: 'umbracoAutomate.if',
    switch: 'umbracoAutomate.switch',
    while: 'umbracoAutomate.while'
  };

  /* Source handle ids on branching and container nodes. Mirror model-to-flow.ts in the client. */
  public static readonly handles = {
    ifTrue: 'true',
    ifFalse: 'false',
    approved: 'approved',
    rejected: 'rejected',
    body: 'body',
    done: 'done'
  };

  /* Extension aliases. The CMS renders workspace and entity actions with
   * `data-mark="workspace-action:<alias>"` / `"entity-action:<alias>"`, which getByTestId reaches —
   * a stable handle on Automate's own actions that does not depend on their localised label. */
  public static readonly extensions = {
    runNowEntityAction: 'UmbracoAutomate.EntityAction.Automation.RunNow',
    deleteAutomationEntityAction: 'UmbracoAutomate.EntityAction.Automation.Delete',
    testConnectionWorkspaceAction: 'UmbracoAutomate.WorkspaceAction.Connection.Test'
  };

  /* Entity types, from src/automation/entity.ts and src/workspace-management/entity.ts. */
  public static readonly entityTypes = {
    automation: 'ua:automation',
    automationRoot: 'ua:automation-root',
    automationGroup: 'ua:automation-group',
    /* The parent to create an automation under when it belongs to a workspace. Passing
     * `automationRoot` instead leaves the new automation's workspaceId as the empty Guid. */
    workspace: 'ua:workspace',
    workspaceRoot: 'ua:workspace-root',
    workspaceMgmt: 'ua:workspace-mgmt',
    workspaceMgmtRoot: 'ua:workspace-mgmt-root'
  };
}
