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

  get nameInput(): Locator {
    return this.page.getByRole('textbox', { name: 'Name', exact: true });
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

  /* --- Modals -------------------------------------------------------------------------- */

  /* The action picker opened by any "Add action" button on the canvas. */
  async chooseActionInPicker(actionName: string) {
    const modal = this.page.locator('ua-node-picker-modal');
    await modal.waitFor({ state: 'visible' });
    await modal.getByRole('button', { name: actionName }).first().click({ force: true });
  }

  /* The connection type picker opened when creating a connection. */
  async chooseConnectionType(typeName: string) {
    const modal = this.page.locator('ua-connection-type-picker-modal');
    await modal.waitFor({ state: 'visible' });
    await modal.getByRole('button', { name: new RegExp(typeName, 'i') }).first().click({ force: true });
  }

  /* Confirms a destructive action in the CMS confirm dialog. */
  async confirmDialog(buttonName: string = 'Delete') {
    const dialog = this.page.locator('umb-confirm-modal');
    await dialog.waitFor({ state: 'visible' });
    await dialog.getByRole('button', { name: buttonName, exact: true }).click({ force: true });
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

  /* Adds an action from a node's own "Add action" button. `branch` targets a branching node's
   * outcome, e.g. "approved" or "body", which renders as "Add action — approved". */
  async addActionFromNode(stepId: string, actionName: string, branch?: string) {
    const label = branch ? `Add action — ${branch}` : 'Add action';
    await this.canvasNode(stepId).getByRole('button', { name: label, exact: true }).click({ force: true });
    await this.chooseActionInPicker(actionName);
  }
}
