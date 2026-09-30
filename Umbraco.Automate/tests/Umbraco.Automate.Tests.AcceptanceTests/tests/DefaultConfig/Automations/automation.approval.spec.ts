import { expect } from '@playwright/test';
import {
  test,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';

/**
 * Runs that pause on a Request Approval step. Such a run is `Suspended` with the approval step
 * `WaitingForInput` (it used to stay `Running`), and only a decision releases it: the run modal
 * offers no Resume, the resume endpoint refuses it, and Terminate asks before it ends the run.
 *
 * Runs need an execution identity, so these use the tier-2 `automateServiceAccountWorkspace`.
 *
 * The demo site may already hold pending approvals from other automations. Every assertion is
 * scoped to the run or automation the test created, and nothing else is decided or terminated.
 */
const { actions, handles } = ConstantHelper;

/* Automations created by the current test, so afterEach can end any run a failure left waiting. */
let createdAutomationIds: string[] = [];

/* trigger → Request Approval; approved → Log Message. The prompt is unique so the dashboard row
 * can be told apart from anything else pending on the site. */
async function seedApprovalAutomation(umbracoAutomateApi: any, workspaceId: string) {
  const prompt = `Approve acceptance run ${uniqueSuffix()}`;
  const approval = automationStep(actions.requestApproval, 'approval', { prompt }, { x: 250, y: 200 });
  const afterApproval = automationStep(
    actions.logMessage,
    'afterApproval',
    { message: 'approved', logLevel: 'Information' },
    { x: 250, y: 400 }
  );
  const name = uniqueName('Approval Run');
  const id = await umbracoAutomateApi.automations.create(name, workspaceId, {
    trigger: manualTrigger(),
    steps: [approval, afterApproval],
    connections: [automationConnection('trigger', approval), automationConnection(approval, afterApproval, handles.approved)]
  });
  createdAutomationIds.push(id);
  return { id, name, prompt };
}

test.describe('Automation approvals', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    createdAutomationIds = [];
    await umbracoUi.goToBackOffice();
  });

  // A failed test can leave its run waiting on the approval. End it so it does not linger on the
  // shared Approvals dashboard; the workspace fixture then deletes the automation.
  test.afterEach(async ({ umbracoAutomateApi }) => {
    for (const id of createdAutomationIds) {
      for (const run of await umbracoAutomateApi.automations.getRuns(id)) {
        if (run.status === 'Suspended' || run.status === 'Running' || run.status === 'Pending') {
          await umbracoAutomateApi.automations.postRunLifecycle(run.id, 'terminate');
        }
      }
    }
  });

  test('a run waiting on an approval is Suspended, and continues once approved on the dashboard', async ({
    page,
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id, name } = await seedApprovalAutomation(umbracoAutomateApi, automateServiceAccountWorkspace.id);
    const runId = await umbracoAutomateApi.automations.startWaitingRun(id);
    const waiting = await umbracoAutomateApi.automations.getRun(runId);
    expect(waiting.stepRuns.map((s: any) => [s.actionAlias, s.status])).toEqual([
      [actions.requestApproval, 'WaitingForInput']
    ]);

    // Assert — the Runs view reports Suspended, and the modal offers no Resume.
    const automate = umbracoAutomateUi.automate;
    await umbracoAutomateUi.goToUrl(automate.automationRunsUrl(id));
    await expect(automate.runStatusTag(runId)).toHaveText('Suspended');
    await automate.openRun(runId);
    await expect(automate.runDetailStep(0).locator('uui-tag')).toHaveText('WaitingForInput');
    // Terminate renders under the same Suspended condition, so its presence proves the actions
    // have loaded and Resume's absence is real rather than a render race.
    await expect(automate.runDetailAction('Terminate')).toBeVisible();
    await expect(automate.runDetailAction('Resume')).toHaveCount(0);

    // Act — approve this run's row on the Approvals dashboard.
    await umbracoAutomateUi.goToUrl(automate.approvalDashboardUrl());
    const row = automate.approvalRow(name);
    await expect(row).toHaveCount(1);
    // A marker on window survives only if the page is not reloaded.
    await page.evaluate(() => ((window as any).__approvalNoReload = true));
    await automate.openApprovalReview(name);
    const decisionModal = automate.approvalDecisionModal;
    await decisionModal.getByRole('textbox', { name: 'Comment' }).fill('Approved by acceptance test');
    await automate.clickInModal(decisionModal.getByRole('button', { name: 'Approve', exact: true }));

    // Assert — the row goes straight away, without a reload, and the run finishes.
    await decisionModal.waitFor({ state: 'detached' });
    await expect(row).toHaveCount(0);
    expect(await page.evaluate(() => (window as any).__approvalNoReload)).toBe(true);

    const run = await umbracoAutomateApi.automations.waitForRunStatus(runId, 'Completed');
    expect(run.stepRuns.map((s: any) => [s.actionAlias, s.status])).toEqual([
      [actions.requestApproval, 'Completed'],
      [actions.logMessage, 'Completed']
    ]);
  });

  test('refuses a manual resume of a run waiting on an approval', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id } = await seedApprovalAutomation(umbracoAutomateApi, automateServiceAccountWorkspace.id);
    const runId = await umbracoAutomateApi.automations.startWaitingRun(id);

    // Act
    const response = await umbracoAutomateApi.automations.postRunLifecycle(runId, 'resume');

    // Assert — refused as InvalidState, and the run stays parked on the approval.
    expect(response.status()).toBe(409);
    const run = await umbracoAutomateApi.automations.getRun(runId);
    expect(run.status).toBe('Suspended');
    expect(run.stepRuns.map((s: any) => [s.actionAlias, s.status])).toEqual([
      [actions.requestApproval, 'WaitingForInput']
    ]);

    // Clean up — end the waiting run here rather than leaving it to afterEach.
    expect((await umbracoAutomateApi.automations.postRunLifecycle(runId, 'terminate')).ok()).toBe(true);
    await umbracoAutomateApi.automations.waitForRunStatus(runId, 'Cancelled');
  });

  test('Terminate asks for confirmation before ending a run waiting on an approval', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id } = await seedApprovalAutomation(umbracoAutomateApi, automateServiceAccountWorkspace.id);
    const runId = await umbracoAutomateApi.automations.startWaitingRun(id);
    const automate = umbracoAutomateUi.automate;
    await umbracoAutomateUi.goToUrl(automate.automationRunsUrl(id));
    await automate.openRun(runId);
    const terminate = automate.runDetailAction('Terminate');

    // Act — Terminate, then back out of the confirmation.
    await automate.clickInModal(terminate);
    await automate.cancelDialog();

    // Assert — nothing was called; the run is still waiting.
    expect((await umbracoAutomateApi.automations.getRun(runId)).status).toBe('Suspended');

    // Act — Terminate again, and confirm this time.
    await automate.clickInModal(terminate);
    await automate.confirmDialog('Terminate');

    // Assert
    await umbracoAutomateApi.automations.waitForRunStatus(runId, 'Cancelled');
  });
});
