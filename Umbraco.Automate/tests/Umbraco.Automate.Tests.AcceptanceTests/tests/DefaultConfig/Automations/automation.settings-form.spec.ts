import { expect } from '@playwright/test';
import { test, ConstantHelper, automationConnection, automationStep, manualTrigger, uniqueName } from '../../../lib/index';

/**
 * The settings form a step's settings modal renders from the action's settings schema.
 *
 * `umb-property` renders a field's description as UFM, which evaluates `${ … }` as a JavaScript
 * expression, so a description that documents binding syntax lost it. Start Automation's Trigger
 * Data field is a built-in example: its description reads "… with ${ trigger.yourKey } bindings.".
 */
const { actions } = ConstantHelper;

test.describe('Settings form', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('shows binding syntax in a field description as written', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const description = await umbracoAutomateApi.catalogue.getFieldDescription(actions.startAutomation, 'triggerData');
    expect(description).toContain('${');
    const start = automationStep(actions.startAutomation, 'start', {}, { x: 250, y: 200 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Settings Description'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [start],
      connections: [automationConnection('trigger', start)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const startId = umbracoAutomateApi.automations.stepByAlias(seeded, 'start').id;

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(startId);

    // Assert — the description keeps `${ … }` rather than evaluating it to nothing.
    const field = umbracoAutomateUi.automate.nodeSettingsModal.locator('umb-property[alias="triggerData"]');
    await expect(field.locator('#description')).toContainText(description);
    await expect(field.locator('umb-ufm-js-expression')).toHaveCount(0);
  });
});
