// STORY-1, STORY-5, STORY-8: library element actions (docs/plans/library-element-actions)
import { randomUUID } from 'crypto';
import { expect } from '@playwright/test';
import { ConstantHelper as CmsConstantHelper, UserGroupBuilder } from '@umbraco-cms/acceptance-test-helpers';
import {
  test,
  ApiHelpers,
  AutomateUiHelper,
  ConstantHelper,
  LibraryApiHelper,
  UiHelpers,
  automationConnection,
  automationStep,
  contentSavedTrigger,
  manualTrigger,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';

/**
 * Step picker and Section access run (T11). The rest are `test.fixme` until the task each
 * describe names turns it on: T12 "Element picker" and "Create Element settings", T14 "Folder
 * permissions" and "Publish reaches the page".
 *
 * `test.fixme` here means "pending implementation", not a known product gap.
 *
 * The Library data comes from the CMS helpers (`umbracoApi.element`, `umbracoApi.documentType`,
 * `UserGroupBuilder`) through the `library` fixture below, which removes everything afterwards,
 * live or in the Library recycle bin. Every element type here has one Textstring property, whose
 * alias (`textstring`) stands in for the stories' `customSchema`.
 *
 * Fields are located by their settings key, and the CMS pickers by the items' unique names and
 * keys. Per this suite's CLAUDE.md, nothing asserts on a localised label.
 */

const STEP_ALIAS = 'target';

const libraryUserGroup = () => LibraryApiHelper.userGroup();

const { pickerGroups, requiresSectionAccess } = ConstantHelper;
const elementActions = {
  getElement: ConstantHelper.actions.getElement,
  getElementProperty: ConstantHelper.actions.getElementProperty,
  createElement: ConstantHelper.actions.createElement,
  updateElementProperty: ConstantHelper.actions.updateElementProperty,
  publishElement: ConstantHelper.actions.publishElement,
  unpublishElement: ConstantHelper.actions.unpublishElement
};
const settingKeys = ConstantHelper.elementSettings;

// Each test builds a service-account workspace, Library items and an automation before it opens
// a settings panel or runs, which on a loaded machine comes close to the 30s local default.
test.describe.configure({ timeout: 60000 });

/** The one step a test seeds, and the automation it is in. */
type Seeded = { id: string; stepId: string };

/** Seeds an automation of one step wired to the trigger, and returns the ids to open it with. */
async function seedStep(
  api: ApiHelpers,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>,
  trigger: { triggerAlias: string; settings: Record<string, unknown> } = contentSavedTrigger()
): Promise<Seeded> {
  const step = automationStep(actionAlias, STEP_ALIAS, settings);
  const id = await api.automations.create(uniqueName('Library Elements'), workspaceId, {
    trigger,
    steps: [step],
    connections: [automationConnection('trigger', step)]
  });
  const seeded = await api.automations.getById(id);
  return { id, stepId: api.automations.stepByAlias(seeded, STEP_ALIAS).id };
}

async function openStep(ui: UiHelpers, { id, stepId }: Seeded) {
  await ui.goToUrl(ui.automate.automationEditUrl(id));
  await ui.automate.waitForCanvas();
  await ui.automate.openStepSettings(stepId);
}

/** The settings the saved automation holds for the seeded step. */
async function savedSettings(api: ApiHelpers, { id }: Seeded): Promise<Record<string, any>> {
  return api.automations.stepByAlias(await api.automations.getById(id), STEP_ALIAS).settings;
}

/** The names of the six element actions, as the step picker lists them, sorted the way it sorts them. */
async function elementActionNames(api: ApiHelpers): Promise<string[]> {
  const names: string[] = [];
  for (const alias of Object.values(elementActions)) {
    names.push(await api.catalogue.getStepTypeName(alias));
  }
  return names.sort((a, b) => a.localeCompare(b));
}

test.describe('Step picker', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-8 AC1
  test('lists the six element actions in a Library group between Content and Media', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: a trigger with nothing after it, in a workspace whose service account has the Library section.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Step Picker'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger()
    });
    const expected = await elementActionNames(umbracoAutomateApi);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act: open the step picker and wait for the catalogue.
    await umbracoAutomateUi.automate.addActionButton(AutomateUiHelper.triggerNodeId).click({ force: true });
    await expect(umbracoAutomateUi.automate.pickerItem(expected[0])).toBeVisible();

    // Assert: Library comes after Content and right before Media, and holds exactly the six actions.
    const headlines = await umbracoAutomateUi.automate.pickerGroupHeadlines();
    const libraryIndex = headlines.indexOf(pickerGroups.library);
    // Groups sort by name, so Core sits between Content and Library; Library is still right before Media.
    expect(headlines.indexOf(pickerGroups.content)).toBeGreaterThanOrEqual(0);
    expect(headlines.indexOf(pickerGroups.content)).toBeLessThan(libraryIndex);
    expect(headlines[libraryIndex + 1]).toBe(pickerGroups.media);
    const listed = await umbracoAutomateUi.automate.pickerGroupItemNames(libraryIndex);
    expect(listed).toEqual(expected);
  });

  // STORY-8 AC2
  test('lists the six element actions when searching for "element"', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Step Search'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger()
    });
    const expected = await elementActionNames(umbracoAutomateApi);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionButton(AutomateUiHelper.triggerNodeId).click({ force: true });

    // Act
    await umbracoAutomateUi.automate.searchPicker('element');

    // Assert: each of the six is listed.
    for (const name of expected) {
      await expect(umbracoAutomateUi.automate.pickerItem(name)).toBeVisible();
    }
  });
});

test.describe('Section access', () => {
  // STORY-8 AC13
  test('refuses to publish an Update Element Property step without the Library section', async ({
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a workspace whose service account has the Content section only.
    const workspace = await library.workspaceFor(
      new UserGroupBuilder()
        .withName(uniqueName('Automate Content Only Group'))
        .addSection(CmsConstantHelper.sectionAliases.content)
        .withDocumentRootAccess(true)
    );
    const seeded = await seedStep(
      umbracoAutomateApi,
      workspace.id,
      elementActions.updateElementProperty,
      { [settingKeys.element]: randomUUID(), propertyAlias: LibraryApiHelper.propertyAlias, value: 'never written' },
      manualTrigger()
    );

    // Act
    const publishing = umbracoAutomateApi.automations.publish(seeded.id);

    // Assert: refused with the section access error.
    await expect(publishing).rejects.toThrow(requiresSectionAccess);
  });
});

test.describe('Element picker', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-8 AC3
  test('stores a picked element as one GUID string', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: content-saved trigger → Update Element Property, Element empty, and an element to pick.
    const element = await library.element('G2 review rating');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: 'new'
    });
    await openStep(umbracoAutomateUi, seeded);

    // Act: pick the element and save the step and automation.
    await umbracoAutomateUi.automate.pickElement(settingKeys.element, element.name);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the saved Element is the element's key, as a string.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded))[settingKeys.element]).toBe(element.id);
  });

  // STORY-8 AC4
  test('shows the picked element when the settings are reopened', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: seed Element = the element's key.
    const element = await library.element('G2 review rating');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
      [settingKeys.element]: element.id,
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: 'new'
    });

    // Act
    await openStep(umbracoAutomateUi, seeded);

    // Assert: picker mode, showing the element.
    await expect(umbracoAutomateUi.automate.bindingSwitch(settingKeys.element)).not.toBeChecked();
    await expect(umbracoAutomateUi.automate.pickedElementRef(settingKeys.element, element.id)).toBeVisible();
  });

  // STORY-8 AC5
  test('saves a binding expression typed with the switch on', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: content-saved trigger in scope → Update Element Property.
    const expression = '${ trigger.contentKey }';
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: 'new'
    });
    await openStep(umbracoAutomateUi, seeded);

    // Act: switch on, type the expression, save.
    await umbracoAutomateUi.automate.toggleBindingSwitch(settingKeys.element);
    await umbracoAutomateUi.automate.enterBindingExpression(settingKeys.element, expression);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the expression is stored.
    await expect.poll(async () => (await savedSettings(umbracoAutomateApi, seeded))[settingKeys.element]).toBe(expression);
  });

  // STORY-8 AC11
  test('reports Element as required when saved empty', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: Update Element Property with Element empty.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: 'new'
    });
    await openStep(umbracoAutomateUi, seeded);

    // Act
    await umbracoAutomateUi.automate.attemptSaveNodeSettings();

    // Assert: the field reports itself missing ("This field is required"), and the modal stays open.
    await expect.poll(async () => await umbracoAutomateUi.automate.isFieldMissing(settingKeys.element)).toBe(true);
    await expect(umbracoAutomateUi.automate.nodeSettingsModal).toBeVisible();
  });

  test('lets a saved key that no longer resolves be removed', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: seed Element = a key no element has.
    const missingKey = randomUUID();
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
      [settingKeys.element]: missingKey,
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: 'new'
    });

    // Act: reopen the step; the picker shows a not-found row with Remove.
    await openStep(umbracoAutomateUi, seeded);
    await expect(umbracoAutomateUi.automate.elementNotFoundRef(settingKeys.element, missingKey)).toBeVisible();
    await umbracoAutomateUi.automate.removeElementButton(settingKeys.element, missingKey).click();

    // Assert: the row is gone and the field is empty again, so Element reports as required.
    await expect(umbracoAutomateUi.automate.pickedElementRef(settingKeys.element, missingKey)).toHaveCount(0);
    await umbracoAutomateUi.automate.attemptSaveNodeSettings();
    await expect.poll(async () => await umbracoAutomateUi.automate.isFieldMissing(settingKeys.element)).toBe(true);
  });
});

test.describe('Create Element settings', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-8 AC6
  test('lists Library folders and no elements in the Folder picker', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a folder and an element, both at the Library root.
    const folder = await library.folder('Reviews');
    const element = await library.element('Loose element');
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.createElement, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: click Choose on Folder.
    await umbracoAutomateUi.automate.openDocumentPicker(settingKeys.folder);

    // Assert: the folder is listed and the element is not.
    await expect(umbracoAutomateUi.automate.treePickerItem(folder.name)).toBeVisible();
    await expect(umbracoAutomateUi.automate.treePickerItem(element.name)).toHaveCount(0);
  });

  // STORY-8 AC7
  test('saves with Folder empty', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: Element Type and Name set, Folder empty.
    const elementType = await library.elementType();
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.createElement, {
      [settingKeys.elementType]: elementType.id,
      name: uniqueName('At the root')
    });
    await openStep(umbracoAutomateUi, seeded);

    // Act: save.
    await umbracoAutomateUi.automate.attemptSaveNodeSettings();

    // Assert: the settings modal closed, so the step saved.
    await expect(umbracoAutomateUi.automate.nodeSettingsModal).toHaveCount(0);
  });

  // STORY-8 AC8
  test('lets only element types be picked in the Element Type picker', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element type and a page type.
    const elementType = await library.elementType();
    const pageType = await library.pageType();
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.createElement, {});
    await openStep(umbracoAutomateUi, seeded);

    // Act: click Choose on Element Type.
    await umbracoAutomateUi.automate.openDocumentPicker(settingKeys.elementType);

    // Assert: the element type can be picked. The CMS picker still lists the page type, but greyed
    // out and not selectable, so it is the "disabled" state that keeps it out of the choice.
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).toBeVisible();
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).not.toHaveAttribute('disabled');
    await expect(umbracoAutomateUi.automate.treePickerItem(pageType.name)).toHaveAttribute('disabled');
  });

  // STORY-8 AC12
  test('reports Element Type as required when saved empty', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: Create Element with Name set and Element Type empty.
    const seeded = await seedStep(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.createElement, {
      name: uniqueName('Never saved')
    });
    await openStep(umbracoAutomateUi, seeded);

    // Act
    await umbracoAutomateUi.automate.attemptSaveNodeSettings();

    // Assert: the field is marked invalid ("This field is required"), and the modal stays open.
    await expect(umbracoAutomateUi.automate.fieldLayout(settingKeys.elementType)).toHaveAttribute('invalid', '');
    await expect(umbracoAutomateUi.automate.nodeSettingsModal).toBeVisible();
  });
});

// Pending until T14 (docs/plans/library-element-actions/PLAN.md).
test.describe('Folder permissions', () => {
  // STORY-1 AC14
  test.fixme('fails Create Element in a folder whose permissions leave out Create', async ({
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a service account with Create by default, and a granular permission on "Reviews"
    // that grants Read and Update but not Create.
    // TODO(T14): the CMS helper's element permission builder sends `ElementPermissionPresentationModel`
    // with the folder's key, which is the model the server maps (ElementPermissionMapper). The
    // backoffice's folder UI sends `ElementContainerPermissionPresentationModel` instead; confirm the
    // builder's form takes effect on a folder at 18.1, or send that shape.
    const reviews = await library.folder('Reviews');
    const elementType = await library.elementType();
    const workspace = await library.workspaceFor(
      libraryUserGroup()
        .addPermissions()
        .addElementPermission()
        .withElementId(reviews.id)
        .addVerbs()
        .withReadElementPermission(true)
        .withUpdateElementPermission(true)
        .withReadElementContainerPermission(true)
        .done()
        .done()
        .done()
    );
    const seeded = await seedStep(
      umbracoAutomateApi,
      workspace.id,
      elementActions.createElement,
      { [settingKeys.folder]: reviews.id, [settingKeys.elementType]: elementType.id, name: uniqueName('Never created') },
      manualTrigger()
    );

    // Act
    const runId = await umbracoAutomateApi.automations.publishAndRunUntil(seeded.id, 'Failed');

    // Assert: the run view's Create Element step failed, carrying the permission reason.
    const run = await umbracoAutomateApi.automations.getRun(runId);
    const stepRun = umbracoAutomateApi.automations.stepRunOf(run, seeded.stepId);
    expect(stepRun.status).toBe('Failed');
    expect(stepRun.error).toBeTruthy();
  });
});

// Pending until T14 (docs/plans/library-element-actions/PLAN.md).
test.describe('Publish reaches the page', () => {
  // STORY-5 AC4
  test.fixme('shows the value written and published by the automation on the page', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoUi,
    library
  }) => {
    // Arrange: a published element holding "old", a published page picking it, and
    // Manual trigger → Update Element Property → Publish Element.
    const newValue = `new-${uniqueSuffix()}`;
    const element = await library.element('G2 review rating', { text: 'old', publish: true });
    const pageUrl = await library.pageShowing(element.id);
    const update = automationStep(
      elementActions.updateElementProperty,
      'update',
      { [settingKeys.element]: element.id, propertyAlias: LibraryApiHelper.propertyAlias, value: newValue },
      { x: 250, y: 200 }
    );
    const publish = automationStep(elementActions.publishElement, 'publish', { [settingKeys.element]: element.id }, { x: 250, y: 380 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Publish To Page'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [update, publish],
      connections: [automationConnection('trigger', update), automationConnection(update, publish)]
    });

    // Act: run it.
    await umbracoAutomateApi.automations.publishAndRunUntil(id, 'Completed');

    // Assert: the page's next render shows the new value.
    // If it still shows "old", stop and ask Rick before adding a cache refresh (PLAN T14).
    await umbracoUi.contentRender.navigateToRenderedContentPage(pageUrl);
    await umbracoUi.contentRender.doesContentRenderValueContainText(newValue);
  });
});
