import { expect } from '@playwright/test';
import {
  test,
  AutomateUiHelper,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';
import type { StubbedOutcome } from '../../../lib/index';

/**
 * Outcomes that depend on a step's settings. No built-in action has dynamic outcomes yet, so
 * these specs make Set Variable one in the browser: `stubDynamicOutcomes` rewrites its catalogue
 * entry to `hasDynamicOutcomes: true` and answers the outcomes endpoint itself. The stub reads the
 * step's Value setting as a comma-separated list of options, so a spec changes the exits by
 * changing that field, exactly as an author of a real dynamic action would.
 *
 * Nothing here publishes or runs, so the tier-1 `automateWorkspace`'s empty service account would
 * do, but the action picker and settings modal need an action-capable workspace; tier 2 it is.
 * The stubs live on the page, so no real catalogue or server data changes.
 *
 * Covers S4 AC4-AC6 and AC12, S5 AC1-AC3 and AC10, and S7 AC2 and AC4.
 */
const { actions, outcomeLabels } = ConstantHelper;

const OTHER: StubbedOutcome = { key: 'other', label: 'other', isDefault: true };

/* Value "a,b" → exits a, b, and the default "other". */
function optionsFromValue(settings: Record<string, unknown>): StubbedOutcome[] {
  const options = String(settings['value'] ?? '')
    .split(',')
    .map((option) => option.trim())
    .filter((option) => option.length > 0);
  return [...options.map((option) => ({ key: option, label: option, isDefault: false })), OTHER];
}

/* trigger → a dynamic step with `value`, with a target step on each of the given exit keys. */
async function seedDynamicStep(
  umbracoAutomateApi: any,
  workspaceId: string,
  value: string,
  lineKeys: string[]
) {
  const dynamic = automationStep(actions.setVariable, 'dynamic', { name: 'options', value }, { x: 250, y: 200 });
  const targets = lineKeys.map((key, index) => automationStep(actions.setVariable, `target${index}`, { name: key, value: key }, { x: 250 + index * 300, y: 450 }));
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Dynamic'), workspaceId, {
    trigger: manualTrigger(),
    steps: [dynamic, ...targets],
    connections: [
      automationConnection('trigger', dynamic),
      ...targets.map((target, index) => automationConnection(dynamic, target, lineKeys[index]))
    ]
  });
  const saved = await umbracoAutomateApi.automations.getById(id);
  return {
    id,
    dynamicId: umbracoAutomateApi.automations.stepByAlias(saved, 'dynamic').id as string
  };
}

test.describe('Dynamic outcomes', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.describe('exits on load', () => {
    test('are resolved from the step\'s saved settings (S4 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — literal labels as they are, the default marked.
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveText(['a', 'b', outcomeLabels.withDefault('other')]);
    });

    test('keep a connected line on a neutral exit when they cannot be resolved', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — the endpoint is down, and a line leaves exit "a".
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => null);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', ['a']);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — the exit is there, and is not flagged as missing: it is unknown, not gone.
      const exit = umbracoAutomateUi.automate.outcomeExit(dynamicId, 'a');
      await expect(exit).toBeVisible();
      await expect(exit).not.toHaveClass(/ua-node__switch-case--missing/);
    });
  });

  test.describe('after the settings are saved', () => {
    test('exits update to match (S4 AC5)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', []);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(3);

      // Act — add option "c".
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a,b,c');

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveText(['a', 'b', 'c', outcomeLabels.withDefault('other')]);
    });

    test('a line from an existing exit stays attached to it (S4 AC6)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — a line leaves exit "a".
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', ['a']);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Act — add option "c", then save the automation.
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a,b,c');
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(4);
      await umbracoAutomateUi.automate.clickSave();

      // Assert — the saved line still leaves "a", and the new settings were saved with it.
      await expect
        .poll(async () => umbracoAutomateApi.automations.stepByAlias(await umbracoAutomateApi.automations.getById(id), 'dynamic').settings.value)
        .toBe('a,b,c');
      const saved = await umbracoAutomateApi.automations.getById(id);
      const line = saved.connections.find((c: any) => c.sourceStepId === dynamicId);
      expect(line.sourceHandle).toBe('a');
      expect(line.outcome).toBe('a');
    });

    test('a failed resolve keeps the previous exits (S4 AC12)', async ({
      page,
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — resolves on load, then the endpoint goes down.
      let failing = false;
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, (settings) =>
        failing ? null : optionsFromValue(settings)
      );
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', []);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(3);

      // Act — save a change while the endpoint is down, and wait for the failed request, so the
      // assertion below is about the canvas after the failure rather than before it.
      failing = true;
      const failedResolve = page.waitForResponse((r) => r.url().endsWith('/outcomes') && r.status() === 500);
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a,b,c');
      await failedResolve;
      await expect(umbracoAutomateUi.automate.errorNotification).toBeVisible();

      // Assert — the exits did not change to anything, or vanish.
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveText(['a', 'b', outcomeLabels.withDefault('other')]);
    });

    test('a failed resolve shows exactly one error notification (S4 AC12)', async ({
      page,
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      let failing = false;
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, (settings) =>
        failing ? null : optionsFromValue(settings)
      );
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', []);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(3);

      // Act
      failing = true;
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a,b,c');

      // Assert
      await expect(umbracoAutomateUi.automate.errorNotification).toHaveCount(1);
      // A second notification would arrive after the first has settled, so look again.
      await page.waitForTimeout(1000);
      await expect(umbracoAutomateUi.automate.errorNotification).toHaveCount(1);
    });
  });

  test.describe('a line to an outcome the step no longer has', () => {
    test('shows a "Missing outcome" exit in error styling when an option is removed (S5 AC1)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — a line leaves exit "b".
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', ['b']);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(3);

      // Act — remove option "b".
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a');

      // Assert — the line is still attached, to a red exit that says why.
      const missing = umbracoAutomateUi.automate.outcomeExit(dynamicId, 'b');
      await expect(missing).toHaveText(`${outcomeLabels.missing}: b`);
      await expect(missing).toHaveClass(/ua-node__switch-case--missing/);
    });

    test('is kept in the saved draft, not dropped (S5 AC2, AC3)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — as above, with the option already removed.
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', ['b']);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.openStepSettings(dynamicId);
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Value', 'a');
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'b')).toBeVisible();

      // Act
      await umbracoAutomateUi.automate.clickSave();

      // Assert — the save succeeded with the new settings, and the line from "b" is still in it.
      await expect
        .poll(async () => umbracoAutomateApi.automations.stepByAlias(await umbracoAutomateApi.automations.getById(id), 'dynamic').settings.value)
        .toBe('a');
      const saved = await umbracoAutomateApi.automations.getById(id);
      expect(saved.connections.filter((c: any) => c.sourceStepId === dynamicId).map((c: any) => c.sourceHandle)).toEqual(['b']);
    });

    test('shows "Missing outcome" on load for an outcome the step does not list (S5 AC1)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — the saved line leaves "zzz", which the options never produce.
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, optionsFromValue);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a,b', ['zzz']);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'zzz')).toHaveText(`${outcomeLabels.missing}: zzz`);
    });

    test('shows "Missing outcome" when the list resolves to nothing (S5 AC10)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — an empty list, with a line leaving "a".
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => []);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'a', ['a']);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — the only exit is the missing one.
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveText([`${outcomeLabels.missing}: a`]);
    });
  });

  test.describe('layout and continuation', () => {
    test('a single declared outcome still draws on the right edge (S3 AC10)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — one option and no default.
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'only', label: 'only', isDefault: false }
      ]);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'only', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(dynamicId)).toHaveCount(1);
      await expect(umbracoAutomateUi.automate.canvasSourceHandles(dynamicId)).toHaveClass(/react-flow__handle-right/);
    });

    test('inserting a step with no default outcome continues through its first exit (S3 AC8)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — trigger → target, and an action whose outcomes are first, second with no default.
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'first', label: 'first', isDefault: false },
        { key: 'second', label: 'second', isDefault: false }
      ]);
      const target = automationStep(actions.delay, 'target', { duration: '00:00:01' }, { x: 250, y: 400 });
      const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Insert'), automateServiceAccountWorkspace.id, {
        trigger: manualTrigger(),
        steps: [target],
        connections: [automationConnection('trigger', target)]
      });
      const seeded = await umbracoAutomateApi.automations.getById(id);
      const targetId = umbracoAutomateApi.automations.stepByAlias(seeded, 'target').id;
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Act — insert the dynamic action onto the trigger → target line.
      await umbracoAutomateUi.automate.clickEdgeInsert(AutomateUiHelper.triggerNodeId, targetId);
      await umbracoAutomateUi.automate.chooseActionInPicker(await umbracoAutomateApi.catalogue.getStepTypeName(actions.setVariable));
      await umbracoAutomateUi.automate.nodeSettingsModal.waitFor({ state: 'visible' });
      await umbracoAutomateUi.automate.changeStepSettingsAndSave({ Name: 'inserted', Value: 'x' });
      await umbracoAutomateUi.automate.clickSave();

      // Assert
      await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(2);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
      const line = saved.connections.find((c: any) => c.sourceStepId === added.id);
      expect(line.targetStepId).toBe(targetId);
      expect(line.sourceHandle).toBe('first');
      expect(line.outcome).toBe('first');
    });
  });

  test.describe('labels', () => {
    const markup = '<img src=x onerror=alert(1)>';

    test('are rendered as literal text, not HTML (S7 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'x', label: markup, isDefault: false }
      ]);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'x', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'x')).toHaveText(markup);
    });

    test('create no element from markup (S7 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'x', label: markup, isDefault: false }
      ]);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'x', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'x')).toBeVisible();

      // Assert
      await expect(umbracoAutomateUi.automate.canvasNode(dynamicId).locator('img')).toHaveCount(0);
    });

    test('never load or run anything from markup (S7 AC4)', async ({
      page,
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — an <img> that would request a unique URL and then run onerror, if it were created.
      // The fixture accepts or dismisses any dialog, so record alerts separately.
      const probePath = `/xss-probe-${uniqueSuffix()}`;
      const alerts: string[] = [];
      const probed: string[] = [];
      page.on('dialog', (dialog) => {
        if (dialog.type() === 'alert') {
          alerts.push(dialog.message());
        }
      });
      page.on('request', (request) => {
        if (request.url().includes(probePath)) {
          probed.push(request.url());
        }
      });
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'x', label: `<img src=${probePath} onerror=alert(1)>`, isDefault: false }
      ]);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'x', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'x')).toBeVisible();
      // A created <img> fails to load within moments; give it a window to do so.
      await page.waitForTimeout(1500);

      // Assert — nothing was requested, so nothing ran.
      expect(probed).toEqual([]);
      expect(alerts).toEqual([]);
    });

    test('a #-prefixed label is rendered as text too, not looked up as a term and not as HTML (S7 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — the "#" makes the canvas try to translate the label, so this is the other path.
      await umbracoAutomateUi.automate.stubDynamicOutcomes(actions.setVariable, () => [
        { key: 'x', label: `#${markup}`, isDefault: false }
      ]);
      const { id, dynamicId } = await seedDynamicStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, 'x', []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await expect(umbracoAutomateUi.automate.outcomeExit(dynamicId, 'x')).toBeVisible();

      // Assert
      await expect(umbracoAutomateUi.automate.canvasNode(dynamicId).locator('img')).toHaveCount(0);
    });
  });
});
