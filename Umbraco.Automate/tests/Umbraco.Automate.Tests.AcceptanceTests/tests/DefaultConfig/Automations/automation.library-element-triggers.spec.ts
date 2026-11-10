// STORY-9, STORY-10: library element triggers (docs/plans/library-element-actions)
import { expect } from '@playwright/test';
import { ConstantHelper as CmsConstantHelper, UserGroupBuilder } from '@umbraco-cms/acceptance-test-helpers';
import {
  test,
  ApiHelpers,
  AutomateUiHelper,
  ConstantHelper,
  LibraryApiHelper,
  automationConnection,
  automationStep,
  elementTrigger,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';

/**
 * Trigger picker, Trigger settings and Runs for the four Library element triggers.
 *
 * Element changes go through the CMS Management API, as an editor's would, never through
 * Automate: the CMS helpers (`umbracoApi.element`) where they have the call, and `library`
 * (`save`, `unpublish`, `move`, `restore`) where they don't. The Library data comes from the
 * `library` fixture, which removes everything afterwards, live or in the Library recycle bin.
 *
 * Elements are created before the automation that watches them is published, so their creation
 * never counts as a run.
 */

const { pickerGroups, requiresSectionAccess, triggers } = ConstantHelper;
const elementTriggers = [triggers.elementSaved, triggers.elementPublished, triggers.elementUnpublished, triggers.elementBatchSaved];
const settingKeys = ConstantHelper.elementSettings;
const triggerSettingKeys = ConstantHelper.elementTriggerSettings;

// Each test builds a workspace, Library items and an automation, and the Runs tests then wait on
// a run or watch for none, which together go past the 30s local default.
test.describe.configure({ timeout: 90000 });

/**
 * Seeds an automation of one trigger and one Log Message step, and returns its id. The step gives
 * the run something to do; the tests are about whether a run starts, not what it does.
 */
async function seedTriggered(
  api: ApiHelpers,
  workspaceId: string,
  trigger: { triggerAlias: string; settings: Record<string, unknown> }
): Promise<string> {
  const log = automationStep(ConstantHelper.actions.logMessage, 'log', { message: 'Element changed', logLevel: 'Information' });
  return await api.automations.create(uniqueName('Library Element Trigger'), workspaceId, {
    trigger,
    steps: [log],
    connections: [automationConnection('trigger', log)]
  });
}

/** Seeds and publishes an automation that starts on `triggerAlias`. */
async function publishedTriggered(api: ApiHelpers, workspaceId: string, triggerAlias: string): Promise<string> {
  const id = await seedTriggered(api, workspaceId, elementTrigger(triggerAlias));
  await api.automations.publish(id);
  return id;
}

test.describe('Trigger picker', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-10 AC1
  test('lists the four element triggers in a Library group', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: an automation with no trigger yet, in a workspace whose service account has the Library section.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Trigger Picker'), automateServiceAccountWorkspace.id);
    const expected = (await Promise.all(elementTriggers.map((alias) => umbracoAutomateApi.catalogue.getTriggerName(alias)))).sort((a, b) => a.localeCompare(b));
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act: open the trigger picker and wait for the catalogue.
    await umbracoAutomateUi.automate.openTriggerPicker();
    await expect(umbracoAutomateUi.automate.pickerItem(expected[0])).toBeVisible();

    // Assert: there is a Library group, and it holds exactly the four element triggers.
    const headlines = await umbracoAutomateUi.automate.pickerGroupHeadlines();
    const libraryIndex = headlines.indexOf(pickerGroups.library);
    expect(libraryIndex).toBeGreaterThanOrEqual(0);
    expect(await umbracoAutomateUi.automate.pickerGroupItemNames(libraryIndex)).toEqual(expected);
  });
});

test.describe('Trigger settings', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-10 AC2
  test('lets only element types be picked in Element Types', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element type, a page type, and an automation with an Element Saved trigger.
    const elementType = await library.elementType();
    const pageType = await library.pageType();
    const id = await seedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTrigger(triggers.elementSaved));
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openTriggerSettings();

    // Act: click Choose on Element Types.
    await umbracoAutomateUi.automate.openTriggerDocumentTypePicker(triggerSettingKeys.elementTypes);

    // Assert: the element type can be picked; the page type is listed greyed out and can't be.
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).toBeVisible();
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).not.toHaveAttribute('disabled');
    await expect(umbracoAutomateUi.automate.treePickerItem(pageType.name)).toHaveAttribute('disabled');
  });

  // STORY-10 AC3
  test('saves an Element Saved trigger with Element Types empty', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange: an automation with no trigger yet.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Trigger Empty Filter'), automateServiceAccountWorkspace.id);
    const elementSavedName = await umbracoAutomateApi.catalogue.getTriggerName(triggers.elementSaved);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act: pick Element Saved, save its settings untouched, and save the automation.
    await umbracoAutomateUi.automate.openTriggerPicker();
    await umbracoAutomateUi.automate.chooseActionInPicker(elementSavedName);
    await umbracoAutomateUi.automate.submitTriggerSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the automation saved the trigger, with no element type filter. Closing the settings
    // without saving rolls the trigger back, so a saved trigger means Save was accepted.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).trigger?.triggerAlias).toBe(triggers.elementSaved);
    const saved = (await umbracoAutomateApi.automations.getById(id)).trigger;
    expect(saved.settings?.[triggerSettingKeys.elementTypes] ?? '').toBe('');
  });

  // STORY-10 AC5
  test('refuses to publish an Element Saved trigger without the Library section', async ({
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
    const id = await seedTriggered(umbracoAutomateApi, workspace.id, elementTrigger(triggers.elementSaved));

    // Act
    const publishing = umbracoAutomateApi.automations.publish(id);

    // Assert: refused with the section access error.
    await expect(publishing).rejects.toThrow(requiresSectionAccess);
  });
});

test.describe('Runs', () => {
  // STORY-9 AC1
  test('starts one run with the element key when an editor saves an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element, then a published automation with an Element Saved trigger.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);

    // Act: an editor saves the element.
    await library.save(element.id);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await umbracoAutomateApi.automations.waitForRuns(id);
    const output = await umbracoAutomateApi.automations.getRunTriggerData(run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC5
  test('starts one run with the element key when an editor publishes an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an unpublished element, then a published automation with an Element Published trigger.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementPublished);

    // Act: an editor publishes the element.
    await library.publish(element.id);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await umbracoAutomateApi.automations.waitForRuns(id);
    const output = await umbracoAutomateApi.automations.getRunTriggerData(run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC6
  test('starts one run with the element key when an editor unpublishes an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a published element, then a published automation with an Element Unpublished trigger.
    const element = await library.element('G2 review rating', { publish: true });
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementUnpublished);

    // Act: an editor unpublishes the element.
    await library.unpublish(element.id);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await umbracoAutomateApi.automations.waitForRuns(id);
    const output = await umbracoAutomateApi.automations.getRunTriggerData(run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC16
  test('starts an Element Unpublished run when a published element is restored from the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a published element sent to the recycle bin, then a published automation with an
    // Element Unpublished trigger. Trashing comes first, so only the restore can start a run.
    const element = await library.element('G2 review rating', { publish: true });
    await library.trash(element.id);
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementUnpublished);

    // Act: an editor restores the element, which the CMS unpublishes as it comes back (R19).
    await library.restore(element.id);

    // Assert: one run, for that element.
    const [run] = await umbracoAutomateApi.automations.waitForRuns(id);
    const output = await umbracoAutomateApi.automations.getRunTriggerData(run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC13
  test('starts no Element Saved run when an editor moves an element to another folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element in "Reviews", a "Campaigns" folder, and a published Element Saved automation.
    const reviews = await library.folder('Reviews');
    const campaigns = await library.folder('Campaigns');
    const element = await library.element('G2 review rating', { folderId: reviews.id });
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);
    // Arrange guard, not a second outcome: a real save starts a run, so the trigger is live and
    // "no new run" below can't pass just because nothing is listening.
    await library.save(element.id);
    await umbracoAutomateApi.automations.waitForRuns(id, 1);

    // Act: an editor moves the element to "Campaigns".
    await library.move(element.id, campaigns.id);

    // Assert: the move starts no run; the guard's run stays the only one.
    await umbracoAutomateApi.automations.expectRunCountToStay(id, 1);
  });

  // STORY-9 AC14
  test('starts no Element Saved run when an editor sends an element to the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element and a published Element Saved automation.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);
    // Arrange guard, not a second outcome: a real save starts a run, so the trigger is live.
    await library.save(element.id);
    await umbracoAutomateApi.automations.waitForRuns(id, 1);

    // Act: an editor sends the element to the recycle bin.
    await library.trash(element.id);

    // Assert: trashing starts no run; the guard's run stays the only one.
    await umbracoAutomateApi.automations.expectRunCountToStay(id, 1);
  });

  // STORY-9 AC15
  test('starts no Element Saved run when an editor restores an element from the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element already in the recycle bin, a second element to prove the trigger is
    // live, and a published Element Saved automation. Trashing comes before the automation, so
    // AC14 can't leak into this test.
    const element = await library.element('G2 review rating');
    await library.trash(element.id);
    const control = await library.element('Control element');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);
    // Arrange guard, not a second outcome: saving the control element starts a run, so the
    // trigger is live.
    await library.save(control.id);
    await umbracoAutomateApi.automations.waitForRuns(id, 1);

    // Act: an editor restores the element.
    await library.restore(element.id);

    // Assert: restoring starts no run; the guard's run stays the only one.
    await umbracoAutomateApi.automations.expectRunCountToStay(id, 1);
  });

  // STORY-9 AC15 (published). The CMS raises two Saved notifications when it restores a published element.
  test('starts no Element Saved run when an editor restores a published element from the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a published element in the recycle bin, a control element, and a published Element
    // Saved automation. Trashing comes before the automation.
    const element = await library.element('G2 review rating', { publish: true });
    await library.trash(element.id);
    const control = await library.element('Control element');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);
    // Arrange guard, not a second outcome: saving the control element starts a run, so the trigger is live.
    await library.save(control.id);
    await umbracoAutomateApi.automations.waitForRuns(id, 1);

    // Act: an editor restores the published element.
    await library.restore(element.id);

    // Assert: restoring starts no run; the guard's run stays the only one.
    await umbracoAutomateApi.automations.expectRunCountToStay(id, 1);
  });

  // STORY-9 AC17
  test('starts no run in a workspace whose service account lacks Read on the element folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element in "Reviews"; a workspace whose service account has a permission set on
    // "Reviews" without Read; and a control workspace (Administrators) with the same trigger.
    // Both automations are published.
    const reviews = await library.folder('Reviews');
    const element = await library.element('G2 review rating', { folderId: reviews.id });
    const restricted = await library.workspaceFor(
      LibraryApiHelper.userGroup()
        .addPermissions()
        .addElementPermission()
        .withElementId(reviews.id)
        .addVerbs()
        .withUpdateElementPermission(true)
        .withReadElementContainerPermission(true)
        .done()
        .done()
        .done()
    );
    const restrictedId = await publishedTriggered(umbracoAutomateApi, restricted.id, triggers.elementSaved);
    const controlId = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, triggers.elementSaved);

    // Act: an editor saves the element.
    await library.save(element.id);
    // Guard, not a second outcome: the control workspace's run shows the save was dispatched, so
    // the restricted workspace having none is down to the permission.
    await umbracoAutomateApi.automations.waitForRuns(controlId, 1);

    // Assert: no run in the restricted workspace.
    await umbracoAutomateApi.automations.expectRunCountToStay(restrictedId, 0);
  });

  // STORY-9 AC21
  test('starts exactly one run when the automation updates the element that triggered it', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an element, and Element Saved ("Skip if this would loop") → Update Element
    // Property on that same element, published.
    const element = await library.element('G2 review rating');
    const update = automationStep(ConstantHelper.actions.updateElementProperty, 'update', {
      [settingKeys.element]: element.id,
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: `by-automation-${uniqueSuffix()}`
    });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Element No Loop'), automateServiceAccountWorkspace.id, {
      // The settings the editor saves with the default behaviour. Settings left empty skip the
      // trigger's settings checks altogether, the loop check included.
      trigger: elementTrigger(triggers.elementSaved, { [triggerSettingKeys.onAutomationOriginatedEvent]: 'SkipOnCycle' }),      steps: [update],
      connections: [automationConnection('trigger', update)]
    });
    await umbracoAutomateApi.automations.publish(id);

    // Act: an editor saves the element.
    await library.save(element.id);
    // Guard, not a second outcome: the first run completed, so its Update Element Property save,
    // the one that could loop, has happened.
    const [run] = await umbracoAutomateApi.automations.waitForRuns(id, 1);
    await umbracoAutomateApi.automations.waitForRunStatus(run.id, 'Completed');

    // Assert: the automation's own save starts no second run.
    await umbracoAutomateApi.automations.expectRunCountToStay(id, 1);
  });
});
