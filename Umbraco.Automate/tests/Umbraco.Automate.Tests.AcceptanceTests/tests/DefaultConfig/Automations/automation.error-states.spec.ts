import { expect } from '@playwright/test';
import {
  test,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName
} from '../../../lib/index';

/**
 * What the UI shows when a request it depends on fails, rather than what it shows when that
 * request succeeds with nothing in it.
 *
 * Every failure here is made by `failRequests()` answering one endpoint with a 500, so no real
 * data is touched, and each test unroutes before it cleans up. The site holds unrelated data —
 * pending approvals included — so assertions are scoped to what the test itself created.
 */
const { actions } = ConstantHelper;

const apiBase = `**${ConstantHelper.api.basePath}`;

test.describe('Error states', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // automateWorkspace is not used directly: the Approvals dashboard only registers once a workspace
  // exists (UA_WORKSPACES_EXIST_CONDITION), and on a fresh site — each CI shard — there may be none.
  test('the Approvals dashboard reports a failed load instead of claiming there are none', async ({
    automateWorkspace,
    umbracoAutomateUi
  }) => {
    // Arrange
    const automate = umbracoAutomateUi.automate;
    const unroute = await automate.failRequests(`${apiBase}approvals/pending`);

    try {
      // Act
      await umbracoAutomateUi.goToUrl(automate.approvalDashboardUrl());

      // Assert — the error paragraph, and neither the empty message nor a table.
      const dashboard = automate.approvalDashboard;
      await expect(dashboard.locator('p.error')).toBeVisible();
      await expect(dashboard.locator('.center p:not(.error)')).toHaveCount(0);
      await expect(dashboard.locator('umb-table')).toHaveCount(0);
    } finally {
      await unroute();
    }
  });

  test('the connection type picker shows only the error when the catalogue fails to load', async ({
    umbracoAutomateUi
  }) => {
    // Arrange
    const automate = umbracoAutomateUi.automate;
    const unroute = await automate.failRequests(`${apiBase}catalogue/connection-types`);

    try {
      // Act — creating a connection starts with the type picker.
      await umbracoAutomateUi.goToUrl(automate.connectionRootUrl());
      await automate.collectionCreateButton.click({ force: true });

      // Assert — exactly one of loading / error / empty / list: here, the error.
      const modal = automate.connectionTypePickerModal;
      await expect(modal.locator('p.error')).toBeVisible();
      await expect(modal.locator('uui-loader')).toHaveCount(0);
      await expect(modal.locator('p.empty')).toHaveCount(0);
      await expect(modal.locator('uui-box')).toHaveCount(0);
      await expect(modal.locator('uui-ref-node')).toHaveCount(0);
    } finally {
      await unroute();
    }
  });

  test('a failed approval decision keeps the modal open and marks the pressed button failed', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → approval → after, published and run until it waits for a decision.
    const automations = umbracoAutomateApi.automations;
    const automate = umbracoAutomateUi.automate;
    const name = uniqueName('Approval Decision Failure');
    const approval = automationStep(actions.requestApproval, 'approval', { prompt: 'Error-state test' }, { x: 250, y: 200 });
    const after = automationStep(actions.delay, 'after', { duration: '00:00:01' }, { x: 250, y: 400 });
    const id = await automations.create(name, automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [approval, after],
      connections: [
        automationConnection('trigger', approval),
        automationConnection(approval, after, ConstantHelper.handles.approved)
      ]
    });
    await automations.startWaitingRun(id);
    await expect.poll(async () => (await automations.getPendingApprovals(id)).length, { timeout: 30000 }).toBe(1);
    const [pending] = await automations.getPendingApprovals(id);

    const unroute = await automate.failRequests(`${apiBase}approvals/${pending.runId}/steps/${pending.stepId}/decision`);
    try {
      // Act — Review this automation's row only, then Approve.
      await umbracoAutomateUi.goToUrl(automate.approvalDashboardUrl());
      await automate.openApprovalReview(name);
      const modal = automate.approvalDecisionModal;
      const approve = modal.locator('uui-button[color="positive"]');
      await automate.clickInModal(approve);

      // Assert — still open, the Approve button failed, and the approval is still pending.
      await expect.poll(async () => await approve.evaluate((el: any) => el.state)).toBe('failed');
      await expect(modal).toBeVisible();
      expect(await modal.locator('uui-button[color="danger"]').evaluate((el: any) => el.state ?? null)).toBeNull();
      expect(await automations.getPendingApprovals(id)).toHaveLength(1);
    } finally {
      await unroute();
      // Decide it for real, so no run of ours is left waiting. The workspace fixture deletes the
      // automation.
      await automations.decideApproval(pending.runId, pending.stepId, 'Rejected');
    }
  });
});
