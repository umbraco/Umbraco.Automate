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
    // Start Automation's target is required even to save a draft.
    const targetId = await umbracoAutomateApi.automations.create(uniqueName('Settings Target'), automateServiceAccountWorkspace.id);
    const start = automationStep(actions.startAutomation, 'start', { automationKey: targetId }, { x: 250, y: 200 });
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

  test('shows binding syntax inside a code span as written', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — For Each's Collection description puts its example in backticks. UFM leaves code
    // alone, so escaping there would show the entity as text.
    const description = await umbracoAutomateApi.catalogue.getFieldDescription(actions.forEach, 'collection');
    const example = /`(\$\{[^`]*\})`/.exec(description)?.[1];
    expect(example).toBeDefined();
    const loop = automationStep(actions.forEach, 'loop', { collection: '${ trigger.items }' }, { x: 250, y: 200 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Settings Code Span'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [loop],
      connections: [automationConnection('trigger', loop)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const loopId = umbracoAutomateApi.automations.stepByAlias(seeded, 'loop').id;

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(loopId);

    // Assert — the example renders as code, with `${` intact rather than `&#36;{`.
    const field = umbracoAutomateUi.automate.nodeSettingsModal.locator('umb-property[alias="collection"]');
    await expect(field.locator('#description code')).toHaveText(example!);
    await expect(field.locator('#description')).not.toContainText('&#36;');
  });
});
