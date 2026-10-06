// STORY-1 to STORY-5: bindable settings, pick or bind (docs/plans/bindable-settings)
import { expect, Page } from '@playwright/test';
import {
  test,
  ApiHelpers,
  CmsContentApiHelper,
  ConstantHelper,
  UiHelpers,
  automationConnection,
  automationStep,
  contentSavedTrigger,
  manualTrigger,
  uniqueName
} from '../../../lib/index';

/**
 * Pick or bind: a settings field that holds a picked value (a content node, a media item) can
 * instead hold a `${ }` binding, switched with a toggle above the picker.
 *
 * Every test drives the real step settings form. The data comes from the API: the content and
 * media a test points at are created by the `cmsContent` fixture and removed afterwards, and each
 * automation is seeded through the Automate API. Specs that run an automation use the tier-2
 * `automateServiceAccountWorkspace`. The catalogue only lists a step's settings for a workspace
 * with a service account, so every test here uses it.
 *
 * Fields are located by their settings key (`contentKey`), and the CMS pickers by the nodes'
 * unique names and keys. Per this suite's CLAUDE.md, nothing asserts on a localised label: the
 * switch is read from its checkbox, the expression from its box, and the "required" state from the
 * control's validity.
 */
const { actions } = ConstantHelper;

// Each test builds a service-account workspace, content and an automation before it opens a
// settings panel, which on a loaded machine comes close to the 30s local default. CI allows 60s.
test.describe.configure({ timeout: 60000 });

const TRIGGER_CONTENT_KEY = '${ trigger.contentKey }';

/** The one step a test seeds, and the automation it is in. */
type Seeded = { id: string; stepId: string };

const STEP_ALIAS = 'target';

/** Seeds an automation of one step wired to the trigger, and returns the ids to open it with. */
async function seedStep(
  api: ApiHelpers,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>,
  trigger: { triggerAlias: string; settings: Record<string, unknown> } = contentSavedTrigger()
): Promise<Seeded> {
  const step = automationStep(actionAlias, STEP_ALIAS, settings);
  const id = await api.automations.create(uniqueName('Bindable Settings'), workspaceId, {
    trigger,
    steps: [step],
    connections: [automationConnection('trigger', step)]
  });
  const seeded = await api.automations.getById(id);
  return { id, stepId: api.automations.stepByAlias(seeded, STEP_ALIAS).id };
}

async function openAutomation(ui: UiHelpers, { id }: Seeded) {
  await ui.goToUrl(ui.automate.automationEditUrl(id));
  await ui.automate.waitForCanvas();
}

async function openStep(ui: UiHelpers, seeded: Seeded) {
  await openAutomation(ui, seeded);
  await ui.automate.openStepSettings(seeded.stepId);
}

/** Inserts the trigger's content key through the binding picker, as an author would. */
async function insertTriggerContentKey(ui: UiHelpers, fieldIndex: number = 0) {
  await ui.automate.openBindingPicker(fieldIndex);
  await ui.automate.chooseBindingLeaf(ConstantHelper.triggers.contentSaved, 'contentKey');
}

/** The settings the saved automation holds for the seeded step. */
async function savedSettings(api: ApiHelpers, { id }: Seeded): Promise<Record<string, any>> {
  return api.automations.stepByAlias(await api.automations.getById(id), STEP_ALIAS).settings;
}

/** Collects uncaught page errors, so a spec can assert the form never threw while it ran. */
function collectPageErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  return errors;
}

test.describe('Field value kind', () => {
  test('reports contentKey on Publish Content as String', async ({ umbracoAutomateApi }) => {
    // Arrange / Act: read Publish Content from the catalogue.
    const action = await umbracoAutomateApi.catalogue.getActionByAlias('umbracoAutomate.publishContent');
    const field = action.settingsSchema.fields.find((f: any) => f.key === 'contentKey');

    // Assert
    expect(field.valueKind).toBe('String');
  });
});

test.describe('Pick or bind', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('shows the picker and the switch when bindings are in scope', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: content-saved trigger → Publish Content, Content Key empty.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});

    // Act: open the step settings.
    await openStep(umbracoAutomateUi, seeded);

    // Assert: the document picker's Choose button and the switch are visible.
    await expect(umbracoAutomateUi.automate.pickerChooseButton('contentKey')).toBeVisible();
    await expect(umbracoAutomateUi.automate.bindingSwitchHost('contentKey')).toBeVisible();
  });

  test('stores a picked node as its GUID', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: as above, and a node to pick.
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});

    // Act: pick Home and save the step and automation.
    await openStep(umbracoAutomateUi, seeded);
    await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the saved step's settings.contentKey equals Home's key.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).contentKey).toBe(home.id);
  });

  test('Insert binding switches to binding mode with the expression', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: as above.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: Insert binding → trigger → contentKey.
    await insertTriggerContentKey(umbracoAutomateUi);

    // Assert: the switch is on and the expression box shows ${ trigger.contentKey }.
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
    await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(TRIGGER_CONTENT_KEY);
  });

  test('reopens a stored binding in binding mode', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: seed contentKey = '${ trigger.contentKey }' via the API.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
      contentKey: TRIGGER_CONTENT_KEY
    });

    // Act: open the step settings.
    await openStep(umbracoAutomateUi, seeded);

    // Assert: the switch is on and the box shows the expression.
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
    await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(TRIGGER_CONTENT_KEY);
  });

  test('reopens a stored GUID in picker mode showing the node', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: seed contentKey = Home's key.
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
      contentKey: home.id
    });

    // Act: open the step settings.
    await openStep(umbracoAutomateUi, seeded);

    // Assert: the switch is off and a node card named Home is shown.
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
  });

  test.describe('sad path', () => {
    test('shows no switch when nothing is in scope and the field is empty', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: manual trigger (no outputs) → Publish Content as the first step.
      const seeded = await seedStep(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        actions.publishContent,
        {},
        manualTrigger()
      );

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: Choose is visible and the switch is not present.
      await expect(umbracoAutomateUi.automate.pickerChooseButton('contentKey')).toBeVisible();
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toHaveCount(0);
    });

    test('shows the switch for a stored binding even with nothing in scope', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: as above, with contentKey seeded as a binding.
      const seeded = await seedStep(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        actions.publishContent,
        { contentKey: TRIGGER_CONTENT_KEY },
        manualTrigger()
      );

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: the switch is on.
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
    });

    test('shows the required message for an empty binding', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: a required field in binding mode.
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
        contentKey: TRIGGER_CONTENT_KEY
      });
      await openStep(umbracoAutomateUi, seeded);

      // Act: clear the box, then try to save.
      await umbracoAutomateUi.automate.enterBindingExpression('contentKey', '');
      await umbracoAutomateUi.automate.attemptSaveNodeSettings();

      // Assert: the field reports itself missing, and the modal stays open.
      await expect.poll(async () => await umbracoAutomateUi.automate.isFieldMissing('contentKey')).toBe(true);
      await expect(umbracoAutomateUi.automate.nodeSettingsModal).toBeVisible();
    });

    test('leaves a collection field unwrapped', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: Switch's cases are a bindable collection edited by the case builder.
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.switch, {});

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: the editor renders with no switch.
      await expect(umbracoAutomateUi.automate.settingsField('cases')).toBeVisible();
      await expect(umbracoAutomateUi.automate.bindableEditor('cases')).toHaveCount(0);
    });

    test('leaves a non-string field unwrapped', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: If's conditions are a bindable object (not a string) edited by the condition builder.
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.if, {});

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: the editor renders with no switch.
      await expect(umbracoAutomateUi.automate.settingsField('conditions')).toBeVisible();
      await expect(umbracoAutomateUi.automate.bindableEditor('conditions')).toHaveCount(0);
    });

    test('keeps the switch while editing when nothing is in scope but a binding is stored', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: manual trigger → Publish Content with contentKey seeded as a binding.
      const seeded = await seedStep(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        actions.publishContent,
        { contentKey: TRIGGER_CONTENT_KEY },
        manualTrigger()
      );
      await openStep(umbracoAutomateUi, seeded);

      // Act: switch off, then on.
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

      // Assert: the switch is still there and the expression is back.
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
      await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(TRIGGER_CONTENT_KEY);
    });

    test('leaves the key/value editor unwrapped', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange: HTTP Request with body type Form.
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.httpRequest, {
        url: 'https://example.com',
        bodyMode: 'Form'
      });

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: the Form fields editor has no switch.
      await expect(umbracoAutomateUi.automate.settingsField('formFields')).toBeVisible();
      await expect(umbracoAutomateUi.automate.bindableEditor('formFields')).toHaveCount(0);
    });

    test('falls back to binding mode when the declared editor is not registered', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: a field whose EditorUiAlias has no manifest. The catalogue answer is rewritten
      // for that, since no installed action declares one. It holds a stored value.
      const home = await cmsContent.createPage('Home');
      await umbracoAutomateUi.automate.stubActionFieldEditor(
        actions.publishContent,
        'contentKey',
        'Umb.PropertyEditorUi.NotInstalled'
      );
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
        contentKey: home.id
      });

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: binding mode with the stored value and no switch.
      await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(home.id);
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toHaveCount(0);
    });
  });
});

test.describe('Switching modes', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('restores the expression after switching off and on', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: insert ${ trigger.contentKey }, switch off, switch on.
    await insertTriggerContentKey(umbracoAutomateUi);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: the box shows it again.
    await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(TRIGGER_CONTENT_KEY);
  });

  test('restores the picked node after binding and switching off', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick Home, switch on, insert a binding, switch off.
    await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
    await insertTriggerContentKey(umbracoAutomateUi);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: the Home node card is shown.
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
  });

  test('carries a picked GUID into the expression box', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick Home, switch on.
    await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: the box shows Home's key.
    await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue(home.id);
  });

  test('shows a GUID typed as text as a node in the picker', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: switch on, type Home's key, switch off.
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
    await umbracoAutomateUi.automate.enterBindingExpression('contentKey', home.id);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: the Home node card is shown.
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
  });

  test('prefers the GUID in the box over the remembered pick', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: two nodes.
    const nodeA = await cmsContent.createPage('Node A');
    const nodeB = await cmsContent.createPage('Node B');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick node A, switch on, paste node B's key, switch off.
    await umbracoAutomateUi.automate.pickDocument('contentKey', nodeA.name);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
    await umbracoAutomateUi.automate.enterBindingExpression('contentKey', nodeB.id);
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: node B's card is shown.
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', nodeB.name);
  });

  test('Insert binding from picker mode remembers the pick', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick Home, Insert binding → contentKey, switch off.
    await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);
    await insertTriggerContentKey(umbracoAutomateUi);
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
    await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

    // Assert: the Home node card is shown.
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
  });

  test.describe('sad path', () => {
    test('saves no binding when switched off', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: a node picked first, so the field is valid again once the binding is switched off.
      const home = await cmsContent.createPage('Home');
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
      await openStep(umbracoAutomateUi, seeded);
      await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);

      // Act: insert a binding, switch off, save.
      await insertTriggerContentKey(umbracoAutomateUi);
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
      await umbracoAutomateUi.automate.submitNodeSettings();
      await umbracoAutomateUi.automate.clickSave();

      // Assert: the saved contentKey has no "${".
      await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).contentKey).toBe(home.id);
      expect((await savedSettings(umbracoAutomateApi, seeded)).contentKey).not.toContain('${');
    });

    test('drops text that is neither a binding nor a GUID on switch-off', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
      await openStep(umbracoAutomateUi, seeded);

      // Act: switch on, type 'home', switch off.
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
      await umbracoAutomateUi.automate.enterBindingExpression('contentKey', 'home');
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

      // Assert: the picker is empty.
      await expect(umbracoAutomateUi.automate.pickerChooseButton('contentKey')).toBeVisible();
      await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveCount(0);
    });

    test('forgets remembered values when the panel is reopened', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
      await openStep(umbracoAutomateUi, seeded);

      // Act: insert a binding, switch off, close, reopen, switch on.
      await insertTriggerContentKey(umbracoAutomateUi);
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
      await umbracoAutomateUi.automate.closeNodeSettings();
      await umbracoAutomateUi.automate.openStepSettings(seeded.stepId);
      await umbracoAutomateUi.automate.toggleBindingSwitch('contentKey');

      // Assert: the box is empty.
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).toBeChecked();
      await expect(umbracoAutomateUi.automate.bindingExpressionInput('contentKey')).toHaveValue('');
    });
  });
});

test.describe('Content key fields', () => {
  test('publishes a picked node when the automation runs', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    umbracoApi,
    cmsContent
  }) => {
    // Arrange: manual trigger → Publish Content, and a draft node to publish.
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.publishContent,
      {},
      manualTrigger()
    );
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(seeded.id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(seeded.stepId);
    await umbracoAutomateUi.automate.pickDocument('contentKey', home.name);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSaveAndPublish();
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(seeded.id)).status).toBe('Published');

    // Act: run it.
    await umbracoAutomateUi.automate.clickEntityAction(ConstantHelper.extensions.runNowEntityAction);

    // Assert: the run completes and Home is published.
    await expect
      .poll(async () => (await umbracoAutomateApi.automations.getRuns(seeded.id))[0]?.status, { timeout: 30000 })
      .toBe('Completed');
    expect(await umbracoApi.document.isDocumentPublished(home.id)).toBe(true);
  });

  test('publishes the triggering node through a binding', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    cmsContent
  }) => {
    // Arrange: content-saved trigger → Publish Content bound to ${ trigger.contentKey }.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
      contentKey: TRIGGER_CONTENT_KEY
    });
    await umbracoAutomateApi.automations.publish(seeded.id);

    // Act: save a content node.
    const saved = await cmsContent.createPage('Triggering node');

    // Assert: that node is published.
    await expect.poll(async () => await umbracoApi.document.isDocumentPublished(saved.id), { timeout: 30000 }).toBe(true);
  });

  test('Get Content Property reads the node bound from an earlier step', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: Get Content (a node picked) → Get Content Property bound to its key.
    const text = `value-${uniqueName('text')}`;
    const home = await cmsContent.createPage('Home', { text, publish: true });
    const get = automationStep(actions.getContent, 'get', { contentKey: home.id }, { x: 250, y: 200 });
    const property = automationStep(
      actions.getContentProperty,
      STEP_ALIAS,
      { contentKey: '${ steps.get.contentKey }', propertyAlias: CmsContentApiHelper.pagePropertyAlias },
      { x: 250, y: 380 }
    );
    const id = await umbracoAutomateApi.automations.create(uniqueName('Bindable Chain'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [get, property],
      connections: [automationConnection('trigger', get), automationConnection(get, property)]
    });
    const stepId = umbracoAutomateApi.automations.stepByAlias(await umbracoAutomateApi.automations.getById(id), STEP_ALIAS).id;

    // Act: run.
    const runId = await umbracoAutomateApi.automations.publishAndRunUntil(id, 'Completed');

    // Assert: the step output is Home's property value.
    expect((await umbracoAutomateApi.automations.getStepOutput(runId, stepId)).value).toBe(text);
  });

  test('opens an automation saved with a plain GUID in picker mode', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: seed a GUID through the API and publish, as automations saved before this change have.
    const home = await cmsContent.createPage('Home');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
      contentKey: home.id
    });
    await umbracoAutomateApi.automations.publish(seeded.id);

    // Act
    await openStep(umbracoAutomateUi, seeded);

    // Assert: picker mode showing the node.
    await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
    await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
  });

  for (const [form, toForm] of [
    ['upper-case', (key: string) => key.toUpperCase()],
    ['braced', (key: string) => `{${key}}`]
  ] as const) {
    test(`opens a ${form} stored GUID in picker mode showing the node`, async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: seed contentKey = Home's key written in a non-canonical form.
      const home = await cmsContent.createPage('Home');
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {
        contentKey: toForm(home.id)
      });

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: picker mode showing the node.
      await expect(umbracoAutomateUi.automate.bindingSwitch('contentKey')).not.toBeChecked();
      await expect(umbracoAutomateUi.automate.pickedDocumentCard('contentKey')).toHaveAttribute('name', home.name);
    });
  }

  test('saves an expression inserted into an empty required field', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: Publish Content with contentKey empty.
    const errors = collectPageErrors(umbracoAutomateUi.page);
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.publishContent, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: Insert binding, then save.
    await insertTriggerContentKey(umbracoAutomateUi);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the expression is stored and nothing threw.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).contentKey).toBe(TRIGGER_CONTENT_KEY);
    expect(errors).toEqual([]);
  });

  test('Move Content shows the switch on both picker fields', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: content-saved trigger → Move Content.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.moveContent, {});

    // Act
    await openStep(umbracoAutomateUi, seeded);

    // Assert: two switches.
    await expect(umbracoAutomateUi.automate.bindingSwitchHost('contentKey')).toBeVisible();
    await expect(umbracoAutomateUi.automate.bindingSwitchHost('targetParentKey')).toBeVisible();
  });

  test.describe('sad path', () => {
    /** Create Content under a parent binding that resolves to nothing, run on demand. */
    async function seedCreateUnderMissingParent(
      api: ApiHelpers,
      workspaceId: string,
      cmsContent: CmsContentApiHelper,
      parentKey: string
    ) {
      const name = uniqueName('Created by automation');
      const seeded = await seedStep(
        api,
        workspaceId,
        actions.createContent,
        { parentKey, contentType: await cmsContent.pageTypeId(), name },
        manualTrigger()
      );
      return { seeded, name };
    }

    test('fails the step when a parent binding resolves to nothing', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: manual trigger → Create Content with parentKey '${ trigger.missing }'.
      const { seeded } = await seedCreateUnderMissingParent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        cmsContent,
        '${ trigger.missing }'
      );

      // Act: run.
      const runId = await umbracoAutomateApi.automations.publishAndRunUntil(seeded.id, 'Failed');

      // Assert: the run's Create Content step is Failed.
      const run = await umbracoAutomateApi.automations.getRun(runId);
      expect(umbracoAutomateApi.automations.stepRunOf(run, seeded.stepId).status).toBe('Failed');
    });

    test('creates nothing at the root when a parent binding resolves to nothing', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange / Act: as above.
      const { seeded, name } = await seedCreateUnderMissingParent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        cmsContent,
        '${ trigger.missing }'
      );
      await umbracoAutomateApi.automations.publishAndRunUntil(seeded.id, 'Failed');

      // Assert: no new node exists at the content root.
      expect(await cmsContent.rootPageExists(name)).toBe(false);
    });

    test('still creates at the root when the parent is left empty', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: Create Content with parentKey empty.
      const { seeded, name } = await seedCreateUnderMissingParent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        cmsContent,
        ''
      );

      // Act: run.
      await umbracoAutomateApi.automations.publishAndRunUntil(seeded.id, 'Completed');

      // Assert: the node is at the root.
      expect(await cmsContent.adoptPageByName(name)).not.toBeNull();
      expect(await cmsContent.rootPageExists(name)).toBe(true);
    });
  });
});

test.describe('Media fields', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('Move Media can select an image', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: an image in a folder.
    const folder = await cmsContent.createFolder('Sample Images');
    const image = await cmsContent.createImage('Sample image', folder.id);
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.moveMedia, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: open the Media field's picker, enter the folder.
    await umbracoAutomateUi.automate.openMediaPicker('mediaKey');
    await umbracoAutomateUi.automate.enterMediaPickerFolder(folder.id);

    // Assert: an image can be selected.
    await expect(umbracoAutomateUi.automate.mediaPickerCard(image.id)).toHaveAttribute('selectable', '');
    await umbracoAutomateUi.automate.pickMedia(image.id);
    await expect(umbracoAutomateUi.automate.pickedMediaCards('mediaKey')).toHaveCount(1);
  });

  test('Move Media can select a folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const folder = await cmsContent.createFolder('Folder to move');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.moveMedia, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: open the Media field's picker at the media root.
    await umbracoAutomateUi.automate.openMediaPicker('mediaKey');

    // Assert: a folder can be selected.
    await expect(umbracoAutomateUi.automate.mediaPickerCard(folder.id)).toHaveAttribute('selectable', '');
  });

  test('Get Media outputs the picked image', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: manual trigger → Get Media with an image picked in the UI.
    const image = await cmsContent.createImage('Picked image');
    const seeded = await seedStep(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.getMedia,
      {},
      manualTrigger()
    );
    await openStep(umbracoAutomateUi, seeded);
    await umbracoAutomateUi.automate.openMediaPicker('mediaKey');
    await umbracoAutomateUi.automate.pickMedia(image.id);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).mediaKey).toBe(image.id);

    // Act: run.
    const runId = await umbracoAutomateApi.automations.publishAndRunUntil(seeded.id, 'Completed');

    // Assert: the step output's key is the image's key.
    expect((await umbracoAutomateApi.automations.getStepOutput(runId, seeded.stepId)).mediaKey).toBe(image.id);
  });

  test('stores a picked media item as its GUID', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const image = await cmsContent.createImage('Stored image');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMedia, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick an image and save.
    await umbracoAutomateUi.automate.openMediaPicker('mediaKey');
    await umbracoAutomateUi.automate.pickMedia(image.id);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the saved mediaKey equals the image's key.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).mediaKey).toBe(image.id);
  });

  for (const [form, toForm] of [
    ['upper-case', (key: string) => key.toUpperCase()],
    ['braced', (key: string) => `{${key}}`]
  ] as const) {
    test(`Get Media opens a ${form} stored GUID in picker mode showing the item`, async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange: seed mediaKey = the image's key written in a non-canonical form.
      const image = await cmsContent.createImage('Stored image');
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMedia, {
        mediaKey: toForm(image.id)
      });

      // Act
      await openStep(umbracoAutomateUi, seeded);

      // Assert: picker mode showing the item.
      await expect(umbracoAutomateUi.automate.bindingSwitch('mediaKey')).not.toBeChecked();
      await expect(umbracoAutomateUi.automate.pickedMediaCards('mediaKey')).toHaveCount(1);
    });
  }

  test('Get Media saves an expression inserted into an empty required field', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: Get Media with mediaKey empty.
    const errors = collectPageErrors(umbracoAutomateUi.page);
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMedia, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: Insert binding, then save.
    await insertTriggerContentKey(umbracoAutomateUi);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the expression is stored and nothing threw.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded)).mediaKey).toBe(TRIGGER_CONTENT_KEY);
    expect(errors).toEqual([]);
  });

  test('Create Media parent stays folders-only', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange: an image in a folder.
    const folder = await cmsContent.createFolder('Sample Images');
    const image = await cmsContent.createImage('Sample image', folder.id);
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.createMedia, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: open the Parent picker, enter the folder.
    await umbracoAutomateUi.automate.openMediaPicker('parentKey');
    await umbracoAutomateUi.automate.enterMediaPickerFolder(folder.id);

    // Assert: images can't be selected.
    await expect(umbracoAutomateUi.automate.mediaPickerCard(image.id)).toBeVisible();
    await expect(umbracoAutomateUi.automate.mediaPickerCard(image.id)).not.toHaveAttribute('selectable', '');
  });

  test.describe('sad path', () => {
    test('Get Media Property cannot select a folder', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi,
      cmsContent
    }) => {
      // Arrange
      const folder = await cmsContent.createFolder('Folder');
      const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMediaProperty, {});
      await openStep(umbracoAutomateUi, seeded);

      // Act: open the Media field's picker at the media root.
      await umbracoAutomateUi.automate.openMediaPicker('mediaKey');

      // Assert: folders aren't selectable.
      await expect(umbracoAutomateUi.automate.mediaPickerCard(folder.id)).toBeVisible();
      await expect(umbracoAutomateUi.automate.mediaPickerCard(folder.id)).not.toHaveAttribute('selectable', '');
    });
  });
});
