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
 * The binding picker a step's settings open to insert a `${ … }` expression.
 *
 * Covers #299 (predecessors listed in flow order) and #307 (each expression shows the output
 * property's description). The graph is trigger → zeta → alpha → target, named so that each wrong
 * ordering is distinguishable from the right one: alphabetical order and the old nearest-first
 * traversal order both put `alpha` first, flow order puts `zeta` first.
 */
const { actions } = ConstantHelper;

test.describe('Binding picker', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  async function seedChain(umbracoAutomateApi: any, workspaceId: string) {
    const zeta = automationStep(actions.setVariable, 'zeta', { name: 'z', value: 'first' }, { x: 250, y: 200 });
    const alpha = automationStep(actions.setVariable, 'alpha', { name: 'a', value: 'second' }, { x: 250, y: 380 });
    const target = automationStep(actions.logMessage, 'target', { message: 'logged', logLevel: 'Information' }, { x: 250, y: 560 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Binding Picker'), workspaceId, {
      trigger: manualTrigger(),
      steps: [zeta, alpha, target],
      connections: [
        automationConnection('trigger', zeta),
        automationConnection(zeta, alpha),
        automationConnection(alpha, target)
      ]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    return { id, targetId: umbracoAutomateApi.automations.stepByAlias(seeded, 'target').id };
  }

  test('lists predecessor steps in flow order, not by name (#299)', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id, targetId } = await seedChain(umbracoAutomateApi, automateServiceAccountWorkspace.id);

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(targetId);
    await umbracoAutomateUi.automate.openBindingPicker();

    // Assert
    await expect.poll(async () => await umbracoAutomateUi.automate.bindingSourceStepAliases()).toEqual(['zeta', 'alpha']);
  });

  test('shows the description of each binding expression (#307)', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — the description is whatever the action's output schema declares.
    const { id, targetId } = await seedChain(umbracoAutomateApi, automateServiceAccountWorkspace.id);
    const description = await umbracoAutomateApi.catalogue.getOutputDescription(actions.setVariable, 'value');

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(targetId);
    await umbracoAutomateUi.automate.openBindingPicker();

    // Assert — the detail carries the type and then the description.
    const leaf = umbracoAutomateUi.automate.bindingLeaf('alpha', 'value');
    await expect(leaf).toBeVisible();
    await expect(leaf).toHaveAttribute('detail', new RegExp(escapeRegExp(description)));
  });

  test('inserts the chosen expression into the field', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id, targetId } = await seedChain(umbracoAutomateApi, automateServiceAccountWorkspace.id);

    // Act — pick alpha's value, save the step, save the automation.
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(targetId);
    await umbracoAutomateUi.automate.openBindingPicker();
    await umbracoAutomateUi.automate.chooseBindingLeaf('alpha', 'value');
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert — the saved message references alpha by its binding alias.
    await expect
      .poll(async () => {
        const saved = await umbracoAutomateApi.automations.getById(id);
        return umbracoAutomateApi.automations.stepByAlias(saved, 'target').settings.message as string;
      })
      .toMatch(/\$\{\s*steps\.alpha\.value\s*\}/);
  });
});

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
