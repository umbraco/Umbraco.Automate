import { expect } from '@playwright/test';
import {
  test,
  AutomateUiHelper,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName
} from '../../../lib/index';

/**
 * The node picker and the binding picker put the caret in their search box on open (the CMS
 * `umbFocus` directive), so a user can type straight away.
 *
 * Focus lives inside nested shadow roots, where `document.activeElement` only reports the
 * outermost host, so these assert it behaviourally: keys sent to the page with no click first
 * must land in the search box and filter the list.
 *
 * Both use the tier-2 `automateServiceAccountWorkspace`: the node picker is scoped to what the
 * workspace's service account may do, and lists no actions at all without one.
 */
const { actions } = ConstantHelper;

test.describe('Picker search focus', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('focuses the node picker search box on open', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange — a trigger with nothing after it, and two actions to tell apart.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Node Picker Focus'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger()
    });
    const logName = await umbracoAutomateApi.catalogue.getStepTypeName(actions.logMessage);
    const delayName = await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act — open the picker and, once the catalogue has loaded, type without clicking the box.
    // A query entered while it is still loading is overwritten by the loaded list.
    await umbracoAutomateUi.automate.addActionButton(AutomateUiHelper.triggerNodeId).click({ force: true });
    await expect(umbracoAutomateUi.automate.pickerItem(delayName)).toBeVisible();
    await page.keyboard.type(logName);

    // Assert — the list is narrowed to the typed action.
    await expect(umbracoAutomateUi.automate.nodePickerModal.getByRole('searchbox')).toHaveValue(logName);
    await expect(umbracoAutomateUi.automate.pickerItem(logName)).toBeVisible();
    await expect(umbracoAutomateUi.automate.pickerItem(delayName)).toHaveCount(0);
  });

  test('focuses the binding picker filter box on open', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    page
  }) => {
    // Arrange — Set Variable outputs `name` and `value`, so filtering on one hides the other.
    const upstream = automationStep(actions.setVariable, 'upstream', { name: 'n', value: 'v' }, { x: 250, y: 200 });
    const target = automationStep(actions.logMessage, 'target', { message: 'logged', logLevel: 'Information' }, { x: 250, y: 380 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Binding Picker Focus'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [upstream, target],
      connections: [automationConnection('trigger', upstream), automationConnection(upstream, target)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const targetId = umbracoAutomateApi.automations.stepByAlias(seeded, 'target').id;
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(targetId);

    // Act — open the picker and type without clicking the filter box.
    await umbracoAutomateUi.automate.openBindingPicker();
    await expect(umbracoAutomateUi.automate.bindingLeaf('upstream', 'name')).toBeVisible();
    await page.keyboard.type('value');

    // Assert — only the matching leaf is left.
    await expect(umbracoAutomateUi.automate.bindingPicker.getByRole('searchbox')).toHaveValue('value');
    await expect(umbracoAutomateUi.automate.bindingLeaf('upstream', 'value')).toBeVisible();
    await expect(umbracoAutomateUi.automate.bindingLeaf('upstream', 'name')).toHaveCount(0);
  });
});
