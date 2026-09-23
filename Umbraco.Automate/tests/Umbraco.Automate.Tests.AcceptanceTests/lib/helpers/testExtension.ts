import { test as base } from '@umbraco-cms/acceptance-test-helpers';
import { ApiHelpers as AutomateApiHelpers, UiHelpers as AutomateUiHelpers } from '.';
import type { TestWorkspace } from './WorkspaceApiHelper';

type AutomateFixtures = {
  umbracoAutomateApi: AutomateApiHelpers;
  umbracoAutomateUi: AutomateUiHelpers;
  automateWorkspace: TestWorkspace;
  automateServiceAccountWorkspace: TestWorkspace;
};

const test = base.extend<AutomateFixtures>({
  umbracoAutomateApi: async ({ umbracoApi, page }, use) => {
    const umbracoAutomateApi = new AutomateApiHelpers(page, umbracoApi);
    await use(umbracoAutomateApi);
  },

  // Reuse the injected CMS umbracoUi fixture rather than constructing a second façade, so a
  // spec that injects both umbracoUi and umbracoAutomateUi shares one CMS UiHelpers instance.
  umbracoAutomateUi: async ({ page, umbracoUi }, use) => {
    const umbracoAutomateUi = new AutomateUiHelpers(page, umbracoUi);
    await use(umbracoAutomateUi);
  },

  /**
   * Tier 1 — the default. A workspace that exists, with no service account.
   *
   * Enough for the Automate sidebar menu to appear, the tree to render, and for automation
   * CRUD. Automations inside it cannot **run**: the empty service account key does not resolve
   * to a user, so there is no execution identity. Use `automateServiceAccountWorkspace` when a
   * spec actually executes something.
   *
   * Playwright only sets up a fixture a test injects, so specs that do not ask for a workspace
   * pay nothing for this.
   */
  automateWorkspace: async ({ umbracoAutomateApi }, use) => {
    const workspace = await umbracoAutomateApi.workspaces.createForTest('Acceptance Test Workspace');
    await use(workspace);
    await umbracoAutomateApi.workspaces.cleanUp(workspace.id);
  },

  /**
   * Tier 2 — a workspace whose automations can run, backed by a real service account
   * (a CMS user of kind Api, in the Administrators group).
   *
   * Costs two extra API round trips and leaves a user behind if teardown is interrupted, so
   * reach for tier 1 unless a spec needs execution.
   */
  automateServiceAccountWorkspace: async ({ umbracoAutomateApi }, use) => {
    const serviceAccountKey = await umbracoAutomateApi.serviceAccounts.create('Acceptance Test Service Account');
    // User groups are populated too: the settings view marks both this and the service account
    // as required, so a workspace missing either cannot be saved from the UI even though the
    // API accepts it. Tier 2 is therefore the fixture to use for any UI spec that saves.
    const userGroupId = await umbracoAutomateApi.serviceAccounts.getUserGroupId('Administrators');
    const workspace = await umbracoAutomateApi.workspaces.createForTest('Acceptance Test Runnable Workspace', {
      serviceAccountKey,
      userGroups: [userGroupId]
    });

    await use(workspace);

    // Workspace first: its automations reference the service account.
    await umbracoAutomateApi.workspaces.cleanUp(workspace.id);
    await umbracoAutomateApi.serviceAccounts.deleteById(serviceAccountKey);
  }
});

export { test };
