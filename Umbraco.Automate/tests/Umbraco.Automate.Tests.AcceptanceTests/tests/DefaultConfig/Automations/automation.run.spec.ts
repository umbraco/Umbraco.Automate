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
 * Publishing and running an automation from the backoffice, and reading the result in the Runs
 * view. Covers #345: Run Script receives the step's binding context as `data`.
 *
 * Runs need an execution identity, so these use the tier-2 `automateServiceAccountWorkspace`.
 */
const { actions, extensions } = ConstantHelper;

/* Throws unless the upstream value reached the script through `data`, so a Completed run is
 * itself the proof that the script read it. Before #345 `data` was always empty. */
function echoScript(expected: string): string {
  return [
    'export default function (data) {',
    '  const value = data.steps.setVar.value;',
    `  if (value !== ${JSON.stringify(expected)}) {`,
    "    throw new Error('Run Script did not receive the upstream value: ' + JSON.stringify(data));",
    '  }',
    '  return { echoed: value };',
    '}'
  ].join('\n');
}

async function seedScriptAutomation(umbracoAutomateApi: any, workspaceId: string, token: string) {
  const setVar = automationStep(actions.setVariable, 'setVar', { name: 'greeting', value: token }, { x: 250, y: 200 });
  const script = automationStep(actions.runScript, 'script', { script: echoScript(token) }, { x: 250, y: 380 });
  return await umbracoAutomateApi.automations.create(uniqueName('Run Script'), workspaceId, {
    trigger: manualTrigger(),
    steps: [setVar, script],
    connections: [automationConnection('trigger', setVar), automationConnection(setVar, script)]
  });
}

test.describe('Automation runs', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('publishes, runs on demand, and shows a successful run whose script read upstream data (#345)', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const token = `hello-${uniqueSuffix()}`;
    const id = await seedScriptAutomation(umbracoAutomateApi, automateServiceAccountWorkspace.id, token);

    // Act — publish, then Run now from the Actions menu.
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.clickSaveAndPublish();
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).status).toBe('Published');

    await umbracoAutomateUi.automate.clickEntityAction(extensions.runNowEntityAction);

    // Assert — the run completes, every step with it.
    await expect
      .poll(async () => (await umbracoAutomateApi.automations.getRuns(id))[0]?.status, { timeout: 30000 })
      .toBe('Completed');
    const [listed] = await umbracoAutomateApi.automations.getRuns(id);
    const run = await umbracoAutomateApi.automations.getRun(listed.id);
    expect(run.initiatedBy).toBe('user');
    expect(run.stepRuns.map((s: any) => [s.actionAlias, s.status])).toEqual([
      [actions.setVariable, 'Completed'],
      [actions.runScript, 'Completed']
    ]);

    // …and the Runs view shows it, with both steps in the detail.
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
    await expect(umbracoAutomateUi.automate.runLink(listed.id)).toBeVisible();
    await umbracoAutomateUi.automate.openRun(listed.id);
    await expect(umbracoAutomateUi.automate.runDetailSteps).toHaveCount(2);
  });

  // PRODUCT GAP: the script's result is persisted (StepRun.OutputData), but neither the run detail
  // API (`stepRuns` has no output) nor ua-run-detail-modal (started, completed, retries and error
  // only) exposes step outputs, so the value cannot be read back in the Runs view. The test above
  // proves the script read the value instead: the script throws unless it did.
  test.fixme('shows the Run Script output in the run view', async () => {});

  // PRODUCT BUG: Run now is offered on a draft. UaEntityAutomationCanRunNowCondition checks only
  // that the trigger supports manual runs, never the automation's status, so the entry shows and
  // clicking it fails with the server's 409 "The automation must be published to be triggered."
  test.fixme('does not offer Run now on an automation that has never been published', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — a draft with a manual trigger, the case where Run now would be offered if published.
    const id = await seedScriptAutomation(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'unused');

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openActionsMenu();

    // Assert
    await expect(umbracoAutomateUi.automate.entityActionInMenu(extensions.runNowEntityAction)).toHaveCount(0);
  });
});
