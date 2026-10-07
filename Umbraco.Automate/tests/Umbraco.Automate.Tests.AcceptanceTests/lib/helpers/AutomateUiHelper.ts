import { Locator, Page, expect } from '@playwright/test';
import { ConstantHelper } from './ConstantHelper';

/**
 * The Page Object Model for the Automate backoffice.
 *
 * Route every Automate UI interaction through this class. Do not inline raw locators or
 * `page.*` calls in a spec for anything this covers — when a flow is missing, add a method
 * here instead, so any hardening (force clicks, retry loops) stays in one place.
 *
 * **Navigate by URL, not by clicking the sidebar.** The sidebar's create buttons sit under the
 * resizable `umb-split-panel` divider, which intermittently intercepts pointer events, so a
 * normal click silently no-ops and a later wait hangs. The route patterns below mirror
 * `paths.ts` in the client and are stable.
 */
export class AutomateUiHelper {
  page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  /* --- Routes -------------------------------------------------------------------------- */

  private sectionPath(): string {
    return `/umbraco/section/${ConstantHelper.section.pathname}`;
  }

  automationCreateUrl(parentEntityType: string, parentUnique: string): string {
    return `${this.sectionPath()}/workspace/ua:automation/create/${parentEntityType}/${parentUnique}`;
  }

  automationEditUrl(id: string): string {
    return `${this.sectionPath()}/workspace/ua:automation/edit/${id}`;
  }

  automationRunsUrl(id: string): string {
    return `${this.automationEditUrl(id)}/view/runs`;
  }

  automationNotificationsUrl(id: string): string {
    return `${this.automationEditUrl(id)}/view/notifications`;
  }

  /* The Approvals dashboard, from src/approval/dashboard/manifests.ts (pathname `approvals`). */
  approvalDashboardUrl(): string {
    return `${this.sectionPath()}/dashboard/approvals`;
  }

  connectionCreateUrl(connectionType: string): string {
    return `${this.sectionPath()}/workspace/ua:connection/create/${connectionType}`;
  }

  connectionEditUrl(id: string): string {
    return `${this.sectionPath()}/workspace/ua:connection/edit/${id}`;
  }

  sectionUrl(): string {
    return this.sectionPath();
  }

  connectionRootUrl(): string {
    return `${this.sectionPath()}/workspace/ua:connection-root/edit/null`;
  }

  workspaceCreateUrl(): string {
    return `${this.sectionPath()}/workspace/ua:workspace-mgmt/create`;
  }

  workspaceEditUrl(id: string): string {
    return `${this.sectionPath()}/workspace/ua:workspace-mgmt/edit/${id}`;
  }

  workspaceRootUrl(): string {
    return `${this.sectionPath()}/workspace/ua:workspace-mgmt-root/edit/null`;
  }

  /* --- Locators ------------------------------------------------------------------------ */

  get dashboard(): Locator {
    return this.page.locator(ConstantHelper.elements.dashboard);
  }

  get sectionSidebar(): Locator {
    return this.page.locator('umb-section-sidebar');
  }

  get workspaceEditor(): Locator {
    return this.page.locator('umb-workspace-editor');
  }

  /* The back arrow in the workspace (management) editor header. */
  get workspaceEditorBackButton(): Locator {
    return this.page.locator('ua-workspace-mgmt-workspace-editor #header > uui-button');
  }

  get nameInput(): Locator {
    return this.page.getByRole('textbox', { name: 'Name', exact: true });
  }

  /* The automation editor's name field itself (not its inner textbox). UUI reflects `pristine`
   * on it and clears it once validation has checked the field, which is when it shows invalid. */
  get automationNameField(): Locator {
    return this.page.locator('ua-automation-workspace-editor uui-input#name');
  }

  get aliasInput(): Locator {
    return this.page.getByRole('textbox', { name: 'Alias', exact: true });
  }

  get saveButton(): Locator {
    return this.page.getByRole('button', { name: 'Save', exact: true });
  }

  get saveAndPublishButton(): Locator {
    return this.page.getByRole('button', { name: 'Save and publish', exact: true });
  }

  get actionsButton(): Locator {
    return this.page.getByRole('button', { name: 'Actions', exact: true });
  }

  get collectionFilter(): Locator {
    return this.page.getByRole('textbox', { name: 'general_filter' });
  }

  /* --- Navigation ---------------------------------------------------------------------- */

  async goToUrl(url: string) {
    await this.page.goto(url);
    await this.page.waitForLoadState('domcontentloaded');
  }

  async waitForDashboard(timeout: number = 30000) {
    await this.dashboard.waitFor({ state: 'visible', timeout });
  }

  async isDashboardVisible() {
    return await this.dashboard.isVisible();
  }

  async waitForWorkspaceEditor(timeout: number = 30000) {
    await this.workspaceEditor.first().waitFor({ state: 'visible', timeout });
  }

  /* Opens an automation from the tree by name. */
  async openAutomationByName(name: string) {
    await this.sectionSidebar.getByLabel(name, { exact: true }).click({ force: true });
    await expect(this.workspaceEditor.first()).toBeVisible();
  }

  /* --- Workspace editor actions -------------------------------------------------------- */

  /* Lit inputs commit on input events; `fill` is reliable here, but the field must be cleared
   * first or the new value is appended to the scaffolded one. */
  async enterName(name: string) {
    await this.nameInput.waitFor({ state: 'visible' });
    await this.nameInput.fill('');
    await this.nameInput.fill(name);
  }

  /* The alias input is read-only until the padlock is released. */
  async enterAlias(alias: string) {
    await this.page.getByRole('button', { name: 'Unlock input' }).click({ force: true });
    await this.aliasInput.fill('');
    await this.aliasInput.fill(alias);
  }

  async clickSave() {
    await this.saveButton.click({ force: true });
  }

  async clickSaveAndPublish() {
    await this.saveAndPublishButton.click({ force: true });
  }

  /* Records every create (POST) of an automation from here on, so a spec can check that
   * client-side validation stopped a save before it reached the server. */
  trackAutomationCreates(): string[] {
    const urls: string[] = [];
    this.page.on('request', (request) => {
      if (request.method() === 'POST' && /\/management\/api\/v1\/automations$/.test(new URL(request.url()).pathname)) {
        urls.push(request.url());
      }
    });
    return urls;
  }

  /**
   * Opens the workspace's Actions menu and clicks the named entry.
   *
   * The entry must be scoped to the menu: an action like "Delete" also exists elsewhere on the
   * page (canvas nodes, membership rows), so an unscoped locator hits a strict-mode violation.
   * The CMS marks the menu with `data-mark="workspace:action-menu-button"`, which is what
   * `testIdAttribute` resolves to.
   */
  async clickAction(actionName: string) {
    await this.actionsButton.click({ force: true });
    await this.page
      .getByTestId('workspace:action-menu-button')
      .getByRole('button', { name: actionName, exact: true })
      .click({ force: true });
  }

  /**
   * Opens the workspace's Actions menu and clicks an entity action by its manifest alias.
   *
   * Prefer this over `clickAction` for Automate's own actions: the CMS marks each entry with
   * `data-mark="entity-action:<alias>"`, so the locator survives a label change or translation.
   */
  async clickEntityAction(alias: string) {
    await this.actionsButton.click({ force: true });
    await this.entityActionInMenu(alias).click({ force: true });
  }

  /* An entity action inside the open workspace Actions menu. Open the menu first. */
  entityActionInMenu(alias: string): Locator {
    return this.page.getByTestId('workspace:action-menu-button').getByTestId(`entity-action:${alias}`);
  }

  /* Opens the workspace Actions menu without choosing anything, to assert on what it offers. */
  async openActionsMenu() {
    await this.actionsButton.click({ force: true });
    // Delete is unconditional, so its presence means the menu has rendered. Entries gated by a
    // condition (Run now, Re-enable) resolve it asynchronously, so let those requests settle
    // before a spec asserts that one is absent.
    await expect(this.entityActionInMenu(ConstantHelper.extensions.deleteAutomationEntityAction)).toBeVisible();
    await this.page.waitForLoadState('networkidle');
  }

  /* A workspace footer action (Save, Test connection, …) by its manifest alias. */
  workspaceAction(alias: string): Locator {
    return this.page.getByTestId(`workspace-action:${alias}`);
  }

  async clickWorkspaceAction(alias: string) {
    await this.workspaceAction(alias).click({ force: true });
  }

  /* The Create button a collection view renders in its toolbar (#297). */
  get collectionCreateButton(): Locator {
    return this.page.locator('umb-collection').getByRole('button', { name: 'Create', exact: true });
  }

  /* --- Modals -------------------------------------------------------------------------- */

  /**
   * Clicks a control inside a sidebar modal.
   *
   * Modals slide in from the side. A forced click while one is still animating fails with
   * "Element is outside of the viewport" — `force` skips the actionability wait that would have
   * covered the animation — so wait for the control to be on screen first.
   */
  async clickInModal(control: Locator) {
    await expect(control).toBeInViewport();
    await control.click({ force: true });
  }

  get nodePickerModal(): Locator {
    return this.page.locator(ConstantHelper.elements.nodePickerModal);
  }

  get nodeSettingsModal(): Locator {
    return this.page.locator(ConstantHelper.elements.nodeSettingsModal);
  }

  /**
   * An entry in the action picker, by its exact name. It is a `uui-ref-node`, so assert on its
   * `disabled` and `detail` attributes (an unavailable action carries its reason in `detail`).
   */
  pickerItem(actionName: string): Locator {
    return this.nodePickerModal.locator(`uui-ref-node[name="${actionName}"]`);
  }

  /**
   * Narrows the action picker with its search box.
   *
   * Waits for the list first: the catalogue loads asynchronously, and a query typed while it is
   * still loading filters an empty list, so the picker shows "No items found" for good.
   */
  async searchPicker(query: string) {
    await this.nodePickerModal.locator('uui-ref-node').first().waitFor({ state: 'visible' });
    await this.nodePickerModal.getByRole('searchbox').fill(query);
  }

  /**
   * Chooses an action in the picker opened by any "Add action" button on the canvas.
   *
   * The list is taller than the modal, and a forced click on an entry below the fold fails with
   * "Element is outside of the viewport" — `force` skips the scroll that would have fixed it. So
   * filter by name first, which also rules out partial matches such as "Get Content" vs
   * "Get Content Property".
   */
  async chooseActionInPicker(actionName: string) {
    await this.searchPicker(actionName);
    await this.clickInModal(this.pickerItem(actionName).getByRole('button').first());
  }

  /**
   * Saves the step settings modal that opens straight after an action is picked.
   *
   * This is not optional: the canvas adds the step provisionally and **rolls it back** if the
   * settings modal is closed without saving. Pick an action whose required fields have defaults
   * (Delay, Run Script, Request Approval, If, While, Parallel) unless the spec fills them in.
   */
  async submitNodeSettings() {
    await this.nodeSettingsModal.waitFor({ state: 'visible' });
    await this.clickInModal(this.nodeSettingsModal.getByRole('button', { name: 'Save', exact: true }));
    await this.nodeSettingsModal.waitFor({ state: 'detached' });
  }

  /* Opens a step's settings from its node. */
  async openStepSettings(stepId: string) {
    await this.canvasNode(stepId).getByRole('button', { name: 'Settings', exact: true }).click({ force: true });
    await this.nodeSettingsModal.waitFor({ state: 'visible' });
  }

  get bindingPicker(): Locator {
    return this.page.locator(ConstantHelper.elements.bindingPickerModal).locator('ua-binding-picker');
  }

  /**
   * Opens the binding picker for a binding-enabled field in the step settings modal.
   *
   * The picker is a property action, so it hangs off the field's "View actions" menu
   * (`data-mark="open-property-actions"`) rather than being a button of its own. `fieldIndex`
   * picks among several binding fields; most actions have one.
   */
  async openBindingPicker(fieldIndex: number = 0) {
    // The "View actions" popover occasionally ignores the first click while the settings form is
    // still rendering its property actions, so retry the open until the entry shows.
    const insertBinding = this.page.getByRole('button', { name: 'Insert binding', exact: true }).first();
    await expect(async () => {
      await this.clickInModal(this.nodeSettingsModal.getByTestId('open-property-actions').nth(fieldIndex));
      await expect(insertBinding).toBeVisible({ timeout: 2000 });
    }).toPass({ timeout: 15000 });
    await insertBinding.click({ force: true });
    await this.bindingPicker.waitFor({ state: 'visible' });
    // The picker slides in, and clickInModal's in-viewport check passes mid-slide. A forced click
    // on a leaf that is still moving can land on the modal backdrop instead, which closes the
    // picker with no selection — the field keeps its old value (flaky on CI build 289743).
    await this.waitForStopMoving(this.bindingPicker);
  }

  /**
   * The step aliases the binding picker lists, top to bottom. Only real predecessor steps carry
   * the Step ID chip; the trigger, "Previous" and "Loop" pseudo-sources are skipped.
   */
  async bindingSourceStepAliases(): Promise<string[]> {
    return await this.bindingPicker.locator('uui-box').evaluateAll((boxes) =>
      boxes
        .filter((box) => box.querySelector('code[title="Step ID"]'))
        .map((box) => box.querySelector('code[title="Alias"]')?.textContent?.trim() ?? '')
    );
  }

  /* A leaf of one binding source, e.g. `bindingLeaf('alpha', 'value')`. Its `detail` holds the
   * type and, since #307, the property description. */
  bindingLeaf(stepAlias: string, path: string): Locator {
    return this.bindingPicker
      .locator('uui-box')
      .filter({ has: this.page.locator(`code[title="Alias"]`, { hasText: new RegExp(`^${stepAlias}$`) }) })
      .locator(`uui-ref-node[name="${path}"]`);
  }

  /* The `detail` text of every leaf in the open binding picker. */
  async bindingLeafDetails(): Promise<string[]> {
    return await this.bindingPicker
      .locator('uui-ref-node')
      .evaluateAll((nodes) => nodes.map((node) => node.getAttribute('detail') ?? ''));
  }

  /* Picks a leaf in the open binding picker, which inserts its expression and closes the picker. */
  async chooseBindingLeaf(stepAlias: string, path: string) {
    await this.clickInModal(this.bindingLeaf(stepAlias, path).getByRole('button').first());
    await this.bindingPicker.waitFor({ state: 'detached' });
  }

  get runsTable(): Locator {
    return this.page.locator(ConstantHelper.elements.runsTable);
  }

  get runDetailModal(): Locator {
    return this.page.locator(ConstantHelper.elements.runDetailModal);
  }

  /* One box per step run in the open run detail modal. */
  get runDetailSteps(): Locator {
    return this.runDetailModal.locator('.step-header');
  }

  /* One ua-step-run-detail per step run, in execution order. The trigger row is a separate element. */
  runDetailStep(index: number): Locator {
    return this.runDetailModal.locator('ua-step-run-detail').nth(index);
  }

  /* Expands a step run, then opens one of its tabs. The labels are localised, so tabs are picked
   * by their fixed order in ua-step-run-detail: Details, Input, Output, Logs. Logs only renders
   * when the step wrote log entries, so asking for it on a silent step times out. */
  async openStepRunTab(index: number, tab: 'details' | 'input' | 'output' | 'logs') {
    const step = this.runDetailStep(index);
    await step.locator('.step-header').click();
    await step.locator('uui-tab').nth(['details', 'input', 'output', 'logs'].indexOf(tab)).click();
  }

  /* The expandable trigger row at the top of the run detail modal. It is a role=button div that
   * reports its state through `aria-expanded`. */
  get runDetailTriggerHeader(): Locator {
    return this.runDetailModal.locator('ua-run-trigger-detail div.header');
  }

  /* The trigger row's body, rendered only while the row is expanded. */
  get runDetailTriggerContent(): Locator {
    return this.runDetailModal.locator('ua-run-trigger-detail .content');
  }

  /* A footer button in the open run detail modal (Close, Suspend, Resume, Terminate, Replay).
   * Scoped to the modal's `actions` slot, because step runs render buttons of their own. Which
   * of them shows depends on the run's status, so assert presence and absence here. */
  runDetailAction(label: string): Locator {
    return this.runDetailModal.locator('[slot="actions"]').getByRole('button', { name: label, exact: true });
  }

  /* The run-level error in the open run detail modal's Run Info sidebar. */
  get runDetailError(): Locator {
    return this.runDetailModal.locator('.sidebar .error-output');
  }

  /* The duration shown in a step run's header. */
  runDetailStepDuration(index: number): Locator {
    return this.runDetailStep(index).locator('.step-duration');
  }

  /* The open tab's content in a step run. */
  stepRunTabPanel(index: number): Locator {
    return this.runDetailStep(index).locator('.tab-panel');
  }

  /* The runs table links each run by the first eight characters of its id. The link is a
   * button (ua-run-link) because it opens the run modal rather than navigating. */
  runLink(runId: string): Locator {
    return this.runsTable.getByRole('button', { name: runId.slice(0, 8), exact: true });
  }

  async openRun(runId: string) {
    await this.runLink(runId).click({ force: true });
    await this.runDetailModal.waitFor({ state: 'visible' });
  }

  /**
   * The status tag in one run's row of the runs table.
   *
   * The row is found by the link inside it. That inner locator must start from the page, not
   * from `runLink()`: a `has` locator is resolved relative to each candidate row, and `runLink`
   * is rooted at ua-runs-table, which is never inside a row, so it would match nothing.
   */
  runStatusTag(runId: string): Locator {
    const link = this.page.getByRole('button', { name: runId.slice(0, 8), exact: true });
    return this.runsTable.locator('uui-table-row').filter({ has: link }).locator('uui-tag');
  }

  /* --- Dashboards ---------------------------------------------------------------------- */

  /**
   * A run in the Overview dashboard's recent activity, by automation name. The list spans every
   * automation on the site, so the name (unique per test) is what scopes it. The row is a
   * role=button, so it opens the run modal from the keyboard as well as on click.
   */
  recentActivityItem(automationName: string): Locator {
    return this.dashboard
      .locator('.activity-item')
      .filter({ has: this.page.locator('.activity-name', { hasText: automationName }) });
  }

  get approvalDashboard(): Locator {
    return this.page.locator(ConstantHelper.elements.approvalDashboard);
  }

  /**
   * One automation's row on the Approvals dashboard.
   *
   * The dashboard lists every pending approval on the site, and the demo site may hold other
   * automations' approvals, so never act on a row that is not scoped to the spec's own automation.
   */
  approvalRow(automationName: string): Locator {
    return this.approvalDashboard.locator('uui-table-row').filter({ hasText: automationName });
  }

  get approvalDecisionModal(): Locator {
    return this.page.locator(ConstantHelper.elements.approvalDecisionModal);
  }

  /* Opens the decision modal from one automation's row on the Approvals dashboard. */
  async openApprovalReview(automationName: string) {
    await this.approvalRow(automationName).getByRole('button', { name: 'Review', exact: true }).click({ force: true });
    await this.approvalDecisionModal.waitFor({ state: 'visible' });
  }

  /* The connection type picker opened when creating a connection. */
  get connectionTypePickerModal(): Locator {
    return this.page.locator(ConstantHelper.elements.connectionTypePickerModal);
  }

  async chooseConnectionType(typeName: string) {
    const modal = this.connectionTypePickerModal;
    await modal.waitFor({ state: 'visible' });
    await modal.getByRole('button', { name: new RegExp(typeName, 'i') }).first().click({ force: true });
  }

  /* The CMS confirm dialog destructive actions go through. Assert `toHaveCount(0)` on it to prove
   * an action did not ask. */
  get confirmModal(): Locator {
    return this.page.locator('umb-confirm-modal');
  }

  /* Confirms a destructive action in the CMS confirm dialog. */
  async confirmDialog(buttonName: string = 'Delete') {
    const dialog = this.confirmModal;
    await dialog.waitFor({ state: 'visible' });
    await dialog.getByRole('button', { name: buttonName, exact: true }).click({ force: true });
  }

  /* Backs out of the CMS confirm dialog, which should leave the item in place. Waits for the
   * dialog to close, so the caller's next assertion sees the settled state. */
  async cancelDialog() {
    const dialog = this.confirmModal;
    await dialog.waitFor({ state: 'visible' });
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click({ force: true });
    await dialog.waitFor({ state: 'detached' });
  }

  /* --- Request stubs ------------------------------------------------------------------- */

  /**
   * Answers every request matching `pattern` with a 500, and returns the matching unroute.
   *
   * Error states are provoked this way rather than by breaking real data, so nothing on the shared
   * site is touched. Call the returned function before any cleanup that goes through the page.
   */
  async failRequests(pattern: string): Promise<() => Promise<void>> {
    const handler = (route: import('@playwright/test').Route) =>
      // A ProblemDetails body, as the server sends: the backoffice only reads `title` from a body
      // that also has `type` and `status`, so a bare `{ title }` would surface as the status text.
      route.fulfill({
        status: 500,
        contentType: 'application/problem+json',
        body: JSON.stringify({ type: 'Error', title: 'Stubbed failure', status: 500 }),
      });
    await this.page.route(pattern, handler);
    return async () => await this.page.unroute(pattern, handler);
  }

  /* --- Connections: OAuth ------------------------------------------------------------- */

  get oauthEditor(): Locator {
    return this.page.locator(ConstantHelper.elements.oauthEditor);
  }

  /* The warning the OAuth editor shows once the browser has blocked its sign-in popup. */
  get oauthPopupBlockedWarning(): Locator {
    return this.oauthEditor.locator('.popup-blocked-warning');
  }

  /* The "Authenticate with <provider>" button — the editor's primary button when disconnected. */
  async clickAuthenticate() {
    const button = this.oauthEditor.locator('.oauth-state > uui-button[look="primary"]');
    await expect(button).toBeEnabled();
    await button.click({ force: true });
  }

  async clickContinueInThisTab() {
    await this.oauthPopupBlockedWarning.locator('uui-button').click({ force: true });
  }

  /**
   * Makes the OAuth editor believe its provider is configured, by answering the status endpoint.
   *
   * The demo site has no Slack client id/secret, so the real answer is "not configured", which
   * disables Authenticate before any popup logic runs. Call before navigating to the connection.
   */
  async stubOAuthProviderConfigured() {
    await this.page.route('**/umbraco/automate/oauth/status/**', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ isConfigured: true }) })
    );
  }

  /* Makes `window.open` return null for every page load — what a blocked popup looks like to the
   * page. Call before navigating. */
  async blockPopups() {
    await this.page.addInitScript(() => {
      window.open = () => null;
    });
  }

  /**
   * Intercepts the OAuth challenge navigation so it never reaches the real provider, and resolves
   * with the request once the page makes it. Answers with an empty page on the site's origin, so
   * the tab's sessionStorage stays readable afterwards.
   */
  interceptOAuthChallenge(): Promise<import('@playwright/test').Request> {
    const pattern = '**/umbraco/automate/oauth/challenge/**';
    const requested = this.page.waitForRequest(pattern);
    void this.page.route(pattern, (route) =>
      route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><title>challenge</title>' })
    );
    return requested;
  }

  /* The nonce the same-tab flow stored for a provider (see nonceStorageKey in the editor). */
  async storedOAuthNonce(provider: string): Promise<string | null> {
    return await this.page.evaluate(
      (key) => sessionStorage.getItem(key),
      `umb-automate-oauth-nonce:${provider.toLowerCase()}`
    );
  }

  /* --- Canvas -------------------------------------------------------------------------- */

  /**
   * A canvas node, located by its step id.
   *
   * xyflow tags nodes with `data-testid="rf__node-<id>"`, but `playwright.config.ts` sets
   * `testIdAttribute: 'data-mark'` for the CMS backoffice, so `getByTestId` will NOT find
   * these. Match the attribute directly.
   */
  canvasNode(stepId: string): Locator {
    return this.page.locator(`[data-testid="rf__node-${stepId}"]`);
  }

  get triggerNode(): Locator {
    return this.page.locator('[data-testid="rf__node-__trigger__"]');
  }

  /* The id xyflow gives the trigger node. Pass it to `addActionFromNode` to add from the trigger. */
  static readonly triggerNodeId = '__trigger__';

  /* Waits until the canvas has rendered the saved graph, not just the React Flow shell. */
  async waitForCanvas(timeout: number = 30000) {
    await this.triggerNode.waitFor({ state: 'visible', timeout });
  }

  /* The "Add action" button on a node, optionally for one branch ("Add action — rejected"). */
  addActionButton(stepId: string, branch?: string): Locator {
    const label = branch ? `Add action — ${branch}` : 'Add action';
    return this.canvasNode(stepId).getByRole('button', { name: label, exact: true });
  }

  /**
   * Adds an action from a node's own "Add action" button and chooses it in the picker. `branch`
   * targets a branching node's outcome, e.g. "approved" or "body", which renders as
   * "Add action — approved". Follow with `submitNodeSettings()`, or the step is rolled back.
   */
  async addActionFromNode(stepId: string, actionName: string, branch?: string) {
    await this.addActionButton(stepId, branch).click({ force: true });
    await this.chooseActionInPicker(actionName);
  }

  /**
   * A connection handle on a node. xyflow renders handles as `.react-flow__handle` with the
   * handle id in `data-handleid` (`true`, `false`, `approved`, `body`, …). Unnamed handles — a
   * plain action's single output, and every node's input — carry no id, so omit `handleId`.
   */
  canvasHandle(stepId: string, type: 'source' | 'target', handleId?: string): Locator {
    const byId = handleId ? `[data-handleid="${handleId}"]` : '';
    return this.canvasNode(stepId).locator(`.react-flow__handle.${type}${byId}`);
  }

  /**
   * Draws a connection between two existing steps by dragging from a source handle to the
   * target's input handle.
   *
   * This is the one canvas gesture with no button equivalent: every "+" creates a **new** step,
   * so wiring a branch into a step that is already on the canvas (a merge) needs a real drag.
   * The mouse is moved in steps because xyflow only starts a connection after pointer movement.
   */
  async connectHandles(sourceStepId: string, sourceHandleId: string | undefined, targetStepId: string) {
    const source = await this.canvasHandle(sourceStepId, 'source', sourceHandleId).boundingBox();
    const target = await this.canvasHandle(targetStepId, 'target').boundingBox();
    if (!source || !target) {
      throw new Error(`Could not locate handles to connect ${sourceStepId} → ${targetStepId}.`);
    }

    await this.page.mouse.move(source.x + source.width / 2, source.y + source.height / 2);
    await this.page.mouse.down();
    await this.page.mouse.move(target.x + target.width / 2, target.y + target.height / 2, { steps: 15 });
    await this.page.mouse.up();
  }

  /* An edge, by the accessible name xyflow gives it ("Edge from <source> to <target>"). */
  canvasEdge(sourceStepId: string, targetStepId: string): Locator {
    return this.page.getByRole('group', { name: `Edge from ${sourceStepId} to ${targetStepId}`, exact: true });
  }

  get edgeFilterModal(): Locator {
    return this.page.locator(ConstantHelper.elements.edgeFilterModal);
  }

  /**
   * Opens the filter modal of the one edge that already has a filter.
   *
   * Only a filtered edge renders its filter button in the active state, which is what picks it
   * out; an automation with several filtered edges needs a locator scoped to `canvasEdge()`.
   */
  async openActiveEdgeFilter() {
    await this.page.locator('.ua-edge__actions .ua-action-bar__btn--active').click({ force: true });
    await this.edgeFilterModal.waitFor({ state: 'visible' });
    // The first click into this modal is usually a small icon button (Remove group), and
    // `clickInModal`'s in-viewport check passes while the modal is still sliding, so a forced
    // click lands where the button was a frame ago and is silently lost. Wait for it to stop.
    await this.waitForStopMoving(this.edgeFilterModal);
  }

  /* Waits until an element's position has been the same for two reads in a row — the end of a
   * slide-in animation, which no attribute or event on the modal reports. */
  async waitForStopMoving(locator: Locator) {
    let previous = '';
    await expect
      .poll(
        async () => {
          const box = JSON.stringify(await locator.boundingBox());
          const settled = box === previous && box !== 'null';
          previous = box;
          return settled;
        },
        { intervals: [100] }
      )
      .toBe(true);
  }
}
