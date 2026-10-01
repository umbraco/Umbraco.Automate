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
 * Reading a run in the run detail modal, and reaching it from the keyboard.
 *
 * The runs-table link, the dashboard's recent-activity rows, the trigger header and each step
 * header are all keyboard operable: role=button, focusable, Enter (and Space, on the headers)
 * activates them, and the headers report their state through `aria-expanded`. Also covers the
 * Logs tab, which lists what a step wrote through `ActionContext.Log` as one row per entry,
 * classed by level.
 *
 * Each test seeds its own completed run (Manual trigger → Log Message at Warning) so the tests
 * stay independent. Runs need an execution identity, so these use the tier-2
 * `automateServiceAccountWorkspace`.
 */
const { actions } = ConstantHelper;

/* Seeds, publishes and runs a one-step automation that logs `message` at Warning, and waits for
 * the run to complete. Returns the automation's name and id and the run's id. */
async function seedCompletedWarningRun(umbracoAutomateApi: any, workspaceId: string, message: string) {
  const log = automationStep(actions.logMessage, 'warn', { message, logLevel: 'Warning' }, { x: 250, y: 200 });
  const name = uniqueName('Run View');
  const id = await umbracoAutomateApi.automations.create(name, workspaceId, {
    trigger: manualTrigger(),
    steps: [log],
    connections: [automationConnection('trigger', log)]
  });
  const runId: string = await umbracoAutomateApi.automations.publishAndRunUntil(id, 'Completed');
  return { name, id, runId };
}

test.describe('Run view', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('opens a run from the runs table with Enter on its focused link', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange
    const { id, runId } = await seedCompletedWarningRun(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'unused');
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
    const link = umbracoAutomateUi.automate.runLink(runId);
    await expect(link).toBeVisible();

    // Act — focus the link without clicking it, then activate it from the keyboard.
    await link.focus();
    await page.keyboard.press('Enter');

    // Assert
    await expect(umbracoAutomateUi.automate.runDetailModal).toBeVisible();
    await expect(umbracoAutomateUi.automate.runDetailSteps).toHaveCount(1);
  });

  test('expands a step run with Enter and collapses it with Space', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange — a completed step starts collapsed (only a failed step is expanded on open).
    const { id, runId } = await seedCompletedWarningRun(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'unused');
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
    await umbracoAutomateUi.automate.openRun(runId);
    const step = umbracoAutomateUi.automate.runDetailStep(0);
    const header = step.locator('.step-header');
    await expect(header).toHaveAttribute('role', 'button');
    await expect(header).toHaveAttribute('aria-expanded', 'false');
    await expect(step.locator('uui-tab')).toHaveCount(0);

    // Act — Enter expands.
    await header.focus();
    await page.keyboard.press('Enter');

    // Assert
    await expect(header).toHaveAttribute('aria-expanded', 'true');
    await expect(step.locator('uui-tab').first()).toBeVisible();

    // Act — Space collapses. The header re-renders in place, so it keeps focus.
    await page.keyboard.press('Space');

    // Assert
    await expect(header).toHaveAttribute('aria-expanded', 'false');
    await expect(step.locator('uui-tab')).toHaveCount(0);
  });

  test('expands the trigger row with Enter', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange
    const { id, runId } = await seedCompletedWarningRun(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'unused');
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
    await umbracoAutomateUi.automate.openRun(runId);
    const header = umbracoAutomateUi.automate.runDetailTriggerHeader;
    await expect(header).toHaveAttribute('role', 'button');
    await expect(header).toHaveAttribute('aria-expanded', 'false');

    // Act
    await header.focus();
    await page.keyboard.press('Enter');

    // Assert — expanded, with its content shown.
    await expect(header).toHaveAttribute('aria-expanded', 'true');
    await expect(umbracoAutomateUi.automate.runDetailTriggerContent).toBeVisible();

    // Act — Enter again collapses.
    await page.keyboard.press('Enter');

    // Assert
    await expect(header).toHaveAttribute('aria-expanded', 'false');
  });

  test('lists a warning logged by the step in its Logs tab', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const message = `warning-${uniqueSuffix()}`;
    const { id, runId } = await seedCompletedWarningRun(umbracoAutomateApi, automateServiceAccountWorkspace.id, message);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
    await umbracoAutomateUi.automate.openRun(runId);

    // Act — the Logs tab only renders because the step wrote an entry.
    await umbracoAutomateUi.automate.openStepRunTab(0, 'logs');

    // Assert — one warning row carrying the message.
    const entries = umbracoAutomateUi.automate.stepRunTabPanel(0).locator('.log-entry');
    await expect(entries).toHaveCount(1);
    await expect(entries.first()).toHaveClass(/\blog-entry--warning\b/);
    await expect(entries.first().locator('.log-message')).toHaveText(message);
  });

  test('opens a run from the dashboard recent activity with Enter', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange — the dashboard lists the most recent runs across automations, so find this
    // automation's row by name.
    const { name } = await seedCompletedWarningRun(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'unused');
    await umbracoAutomateUi.goToAutomateSection();
    await umbracoAutomateUi.automate.waitForDashboard();
    const row = umbracoAutomateUi.automate.recentActivityItem(name);
    await expect(row).toHaveAttribute('role', 'button');

    // Act
    await row.focus();
    await page.keyboard.press('Enter');

    // Assert
    await expect(umbracoAutomateUi.automate.runDetailModal).toBeVisible();
    await expect(umbracoAutomateUi.automate.runDetailSteps).toHaveCount(1);
  });
});
