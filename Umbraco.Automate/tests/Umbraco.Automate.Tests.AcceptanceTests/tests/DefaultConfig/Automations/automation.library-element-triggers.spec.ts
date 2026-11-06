// STORY-9, STORY-10: library element triggers (docs/plans/library-element-actions)
// Pending until T20 (docs/plans/library-element-actions/PLAN.md)
import { expect, Page } from '@playwright/test';
import {
  ApiHelpers as UmbracoApiHelpers,
  ConstantHelper as CmsConstantHelper,
  UserGroupBuilder
} from '@umbraco-cms/acceptance-test-helpers';
import {
  test,
  ApiHelpers,
  AutomateUiHelper,
  ConstantHelper,
  LibraryApiHelper,
  automationConnection,
  automationStep,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';
import { ensureOk } from '../../../lib/helpers/ApiResponse';

/**
 * Trigger picker, Trigger settings and Runs for the six Library element triggers (T20).
 *
 * Every test is `test.fixme` until T17-T19 add the triggers and T20 turns this file on against
 * the LocalDB demo site.
 *
 * Element changes go through the CMS Management API, as an editor's would, never through
 * Automate: the CMS helpers (`umbracoApi.element`) where they have the call, and a raw request
 * through the same authenticated context where they don't (unpublish, move, restore). The
 * Library data comes from the `library` fixture, which removes everything afterwards, live or in
 * the Library recycle bin.
 *
 * Elements are created before the automation that watches them is published, so their creation
 * never counts as a run.
 *
 * TODO(T20): the helpers below marked TODO(T20) are inline so this file type-checks before the
 * triggers exist. Move them into lib/helpers (ConstantHelper, AutomationBuilder,
 * CatalogueApiHelper, AutomationApiHelper, LibraryApiHelper, AutomateUiHelper) when turning the
 * spec on, as the suite's CLAUDE.md asks: AutomateUiHelper is the single page object.
 */

// TODO(T20): move to ConstantHelper.triggers.
const elementTriggers = {
  elementSaved: 'umbracoAutomate.elementSaved',
  elementPublished: 'umbracoAutomate.elementPublished',
  elementUnpublished: 'umbracoAutomate.elementUnpublished',
  elementBatchSaved: 'umbracoAutomate.elementBatchSaved'
};

// TODO(T20): move to ConstantHelper (e.g. `elementTriggerSettings`). The camelCase of
// ElementSavedTriggerSettings' properties, as ContentSavedTriggerSettings has them.
const triggerSettingKeys = {
  elementTypes: 'elementTypes',
  onAutomationOriginatedEvent: 'onAutomationOriginatedEvent'
};

const { pickerGroups, requiresSectionAccess } = ConstantHelper;
const settingKeys = ConstantHelper.elementSettings;
const MANAGEMENT_API = '/umbraco/management/api/v1';

/**
 * How long a "no run starts" test watches for a run before taking it as none. Dispatch goes
 * through the outbox, which the dispatcher polls every 500 ms (OutboxOptions.PollInterval), so a
 * run normally appears within a couple of seconds. A bounded wait can only show that nothing
 * started within the window: 10 s is 20 dispatcher polls.
 */
const NO_RUN_WINDOW_MS = 10_000;
const NO_RUN_POLL_MS = 1_000;

// Each test builds a workspace, Library items and an automation, and the Runs tests then wait on
// a run or watch the window above, which together go past the 30s local default.
test.describe.configure({ timeout: 90000 });

/** TODO(T20): move to AutomationBuilder as `elementTrigger(alias, settings)`. */
function elementTrigger(triggerAlias: string, settings: Record<string, unknown> = {}) {
  return { triggerAlias, settings };
}

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

/** TODO(T20): move to CatalogueApiHelper as `getTriggers()` / `getTriggerName(alias)`. */
async function triggerNames(umbracoApi: UmbracoApiHelpers, aliases: string[]): Promise<string[]> {
  const response = await umbracoApi.get(umbracoApi.baseUrl + ConstantHelper.api.basePath + 'catalogue/triggers');
  await ensureOk(response, 'Reading catalogue triggers');
  const body = await response.json();
  const triggers: any[] = Array.isArray(body) ? body : body.items ?? [];
  return aliases.map((alias) => {
    const trigger = triggers.find((t) => t.alias === alias);
    if (!trigger) {
      throw new Error(`Trigger "${alias}" is not in the catalogue.`);
    }
    return trigger.name as string;
  });
}

/**
 * The data the trigger passed to a run, parsed, from `runs/{id}/trigger-data`, the endpoint the
 * run view's trigger row reads.
 *
 * TODO(T20): move to AutomationApiHelper as `getRunTriggerData(runId)`. Check the property casing
 * on the running site: the run view binds `trigger.contentKey`, so `elementKey` is expected.
 */
async function runTriggerData(umbracoApi: UmbracoApiHelpers, runId: string): Promise<any> {
  const response = await umbracoApi.get(umbracoApi.baseUrl + ConstantHelper.api.basePath + 'runs/' + runId + '/trigger-data');
  await ensureOk(response, `Getting the trigger data of run ${runId}`);
  const body = await response.json();
  return body.triggerData ? JSON.parse(body.triggerData) : null;
}

/** Waits until the automation has `count` runs, and returns them (newest first). */
async function waitForRuns(api: ApiHelpers, automationId: string, count: number = 1): Promise<any[]> {
  await expect.poll(async () => (await api.automations.getRuns(automationId)).length, { timeout: 30000 }).toBe(count);
  return await api.automations.getRuns(automationId);
}

/**
 * Watches the automation's runs for NO_RUN_WINDOW_MS and fails as soon as the count is not
 * `count`. This is how a test shows that something did **not** start a run.
 *
 * TODO(T20): move to AutomationApiHelper (e.g. `expectRunCountToStay`).
 */
async function expectRunCountToStay(api: ApiHelpers, automationId: string, count: number) {
  const deadline = Date.now() + NO_RUN_WINDOW_MS;
  do {
    const runs = await api.automations.getRuns(automationId);
    expect(runs, `runs of automation ${automationId} within ${NO_RUN_WINDOW_MS} ms`).toHaveLength(count);
    await new Promise((resolve) => setTimeout(resolve, NO_RUN_POLL_MS));
  } while (Date.now() < deadline);
}

/**
 * Saves an element as an editor would: reads it, changes its Textstring value and PUTs it back
 * through the CMS Management API.
 *
 * TODO(T20): move to LibraryApiHelper as `save(id, text)`.
 */
async function saveElement(umbracoApi: UmbracoApiHelpers, id: string, text: string = `saved-${uniqueSuffix()}`) {
  const element = await umbracoApi.element.get(id);
  const value = element.values.find((v: any) => v.alias === LibraryApiHelper.propertyAlias);
  if (!value) {
    throw new Error(`Element ${id} has no "${LibraryApiHelper.propertyAlias}" value.`);
  }
  value.value = text;
  await ensureOk(await umbracoApi.element.update(id, element), `Saving element ${id}`);
}

/**
 * Unpublishes an element through `PUT element/{id}/unpublish`. The CMS helpers have no unpublish.
 *
 * TODO(T20): move to LibraryApiHelper as `unpublish(id)`.
 */
async function unpublishElement(umbracoApi: UmbracoApiHelpers, id: string) {
  const response = await umbracoApi.put(`${umbracoApi.baseUrl}${MANAGEMENT_API}/element/${id}/unpublish`, { cultures: null });
  await ensureOk(response, `Unpublishing element ${id}`);
  await expect.poll(async () => await umbracoApi.element.isElementPublished(id)).toBe(false);
}

/**
 * Moves an element to a Library folder through `PUT element/{id}/move`. The CMS helpers move
 * folders only.
 *
 * TODO(T20): move to LibraryApiHelper as `move(id, folderId)`.
 */
async function moveElement(umbracoApi: UmbracoApiHelpers, id: string, folderId: string) {
  const response = await umbracoApi.put(`${umbracoApi.baseUrl}${MANAGEMENT_API}/element/${id}/move`, { target: { id: folderId } });
  await ensureOk(response, `Moving element ${id} to folder ${folderId}`);
}

/**
 * Restores an element from the Library recycle bin to the Library root through
 * `PUT recycle-bin/element/{id}/restore`. The CMS helpers have no restore.
 *
 * TODO(T20): move to LibraryApiHelper as `restore(id, targetFolderId?)`.
 */
async function restoreElement(umbracoApi: UmbracoApiHelpers, id: string) {
  const response = await umbracoApi.put(`${umbracoApi.baseUrl}${MANAGEMENT_API}/recycle-bin/element/${id}/restore`, { target: null });
  await ensureOk(response, `Restoring element ${id}`);
}

/**
 * The trigger's own settings modal (`ua-trigger-settings-modal`), which is not the step settings
 * modal, so `AutomateUiHelper.settingsField` and `openStepSettings` don't reach it.
 *
 * TODO(T20): move into AutomateUiHelper (`triggerSettingsModal`, `openTriggerSettings`,
 * `triggerSettingsField`, `openTriggerPicker`, `submitTriggerSettings`), per the suite's rule that
 * no spec holds raw locators.
 */
function triggerUi(page: Page) {
  const modal = page.locator('ua-trigger-settings-modal');
  const field = (key: string) => modal.locator(`umb-property[alias="${key}"]`);
  return {
    modal,
    field,
    /* The empty-canvas placeholder the trigger node shows until a trigger is chosen. Clicking it
     * opens `ua-node-picker-modal` in trigger mode. */
    placeholder: page.locator(`[data-testid="rf__node-${AutomateUiHelper.triggerNodeId}"] button.ua-node--placeholder`),
    async waitForForm() {
      await modal.waitFor({ state: 'visible' });
      await modal.locator('ua-settings-form').waitFor({ state: 'attached' });
    },
    async open(automate: AutomateUiHelper) {
      await automate.triggerNode.getByRole('button', { name: 'Settings', exact: true }).click({ force: true });
      await this.waitForForm();
    },
    async openDocumentTypePicker(automate: AutomateUiHelper, key: string) {
      await automate.clickInModal(field(key).locator('uui-button#btn-add'));
      await automate.documentPickerModal.waitFor({ state: 'visible' });
    },
    async submit(automate: AutomateUiHelper) {
      await this.waitForForm();
      await automate.clickInModal(modal.locator('[slot="actions"] uui-button[look="primary"]'));
      await modal.waitFor({ state: 'detached' });
    }
  };
}

test.describe('Trigger picker', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  // STORY-10 AC1
  test.fixme('lists the six element triggers in a Library group', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    umbracoApi,
    page
  }) => {
    // Arrange: an automation with no trigger yet, in a workspace whose service account has the Library section.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Trigger Picker'), automateServiceAccountWorkspace.id);
    const expected = (await triggerNames(umbracoApi, Object.values(elementTriggers))).sort((a, b) => a.localeCompare(b));
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act: open the trigger picker and wait for the catalogue.
    await triggerUi(page).placeholder.click({ force: true });
    await expect(umbracoAutomateUi.automate.pickerItem(expected[0])).toBeVisible();

    // Assert: there is a Library group, and it holds exactly the six element triggers.
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
  test.fixme('lets only element types be picked in Element Types', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    library,
    page
  }) => {
    // Arrange: an element type, a page type, and an automation with an Element Saved trigger.
    const elementType = await library.elementType();
    const pageType = await library.pageType();
    const id = await seedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTrigger(elementTriggers.elementSaved));
    const trigger = triggerUi(page);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await trigger.open(umbracoAutomateUi.automate);

    // Act: click Choose on Element Types.
    await trigger.openDocumentTypePicker(umbracoAutomateUi.automate, triggerSettingKeys.elementTypes);

    // Assert: the element type can be picked; the page type is listed greyed out and can't be.
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).toBeVisible();
    await expect(umbracoAutomateUi.automate.treePickerItem(elementType.name)).not.toHaveAttribute('disabled');
    await expect(umbracoAutomateUi.automate.treePickerItem(pageType.name)).toHaveAttribute('disabled');
  });

  // STORY-10 AC3
  test.fixme('saves an Element Saved trigger with Element Types empty', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi,
    umbracoApi,
    page
  }) => {
    // Arrange: an automation with no trigger yet.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Trigger Empty Filter'), automateServiceAccountWorkspace.id);
    const [elementSavedName] = await triggerNames(umbracoApi, [elementTriggers.elementSaved]);
    const trigger = triggerUi(page);
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();

    // Act: pick Element Saved, save its settings untouched, and save the automation.
    await trigger.placeholder.click({ force: true });
    await umbracoAutomateUi.automate.chooseActionInPicker(elementSavedName);
    await trigger.submit(umbracoAutomateUi.automate);
    await umbracoAutomateUi.automate.clickSave();

    // Assert: the automation saved the trigger, with no element type filter. Closing the settings
    // without saving rolls the trigger back, so a saved trigger means Save was accepted.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).trigger?.triggerAlias).toBe(elementTriggers.elementSaved);
    const saved = (await umbracoAutomateApi.automations.getById(id)).trigger;
    expect(saved.settings?.[triggerSettingKeys.elementTypes] ?? '').toBe('');
  });

  // STORY-10 AC5
  test.fixme('refuses to publish an Element Saved trigger without the Library section', async ({
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
    const id = await seedTriggered(umbracoAutomateApi, workspace.id, elementTrigger(elementTriggers.elementSaved));

    // Act
    const publishing = umbracoAutomateApi.automations.publish(id);

    // Assert: refused with the section access error.
    await expect(publishing).rejects.toThrow(requiresSectionAccess);
  });
});

test.describe('Runs', () => {
  // STORY-9 AC1
  test.fixme('starts one run with the element key when an editor saves an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an element, then a published automation with an Element Saved trigger.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementSaved);

    // Act: an editor saves the element.
    await saveElement(umbracoApi, element.id);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await waitForRuns(umbracoAutomateApi, id);
    const output = await runTriggerData(umbracoApi, run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC5
  test.fixme('starts one run with the element key when an editor publishes an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an unpublished element, then a published automation with an Element Published trigger.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementPublished);

    // Act: an editor publishes the element.
    expect(await umbracoApi.element.publish(element.id)).toBeLessThan(300);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await waitForRuns(umbracoAutomateApi, id);
    const output = await runTriggerData(umbracoApi, run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC6
  test.fixme('starts one run with the element key when an editor unpublishes an element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: a published element, then a published automation with an Element Unpublished trigger.
    const element = await library.element('G2 review rating', { publish: true });
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementUnpublished);

    // Act: an editor unpublishes the element.
    await unpublishElement(umbracoApi, element.id);

    // Assert: one run, whose trigger output carries the element's key.
    const [run] = await waitForRuns(umbracoAutomateApi, id);
    const output = await runTriggerData(umbracoApi, run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC16
  test.fixme('starts an Element Unpublished run when a published element is restored from the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: a published element sent to the recycle bin, then a published automation with an
    // Element Unpublished trigger. Trashing comes first, so only the restore can start a run.
    const element = await library.element('G2 review rating', { publish: true });
    await library.trash(element.id);
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementUnpublished);

    // Act: an editor restores the element, which the CMS unpublishes as it comes back (R19).
    await restoreElement(umbracoApi, element.id);

    // Assert: one run, for that element.
    const [run] = await waitForRuns(umbracoAutomateApi, id);
    const output = await runTriggerData(umbracoApi, run.id);
    expect(output?.elementKey).toBe(element.id);
  });

  // STORY-9 AC13
  test.fixme('starts no Element Saved run when an editor moves an element to another folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an element in "Reviews", a "Campaigns" folder, and a published Element Saved automation.
    const reviews = await library.folder('Reviews');
    const campaigns = await library.folder('Campaigns');
    const element = await library.element('G2 review rating', { folderId: reviews.id });
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementSaved);
    // Arrange guard, not a second outcome: a real save starts a run, so the trigger is live and
    // "no new run" below can't pass just because nothing is listening.
    await saveElement(umbracoApi, element.id);
    await waitForRuns(umbracoAutomateApi, id, 1);

    // Act: an editor moves the element to "Campaigns".
    await moveElement(umbracoApi, element.id, campaigns.id);

    // Assert: the move starts no run; the guard's run stays the only one.
    await expectRunCountToStay(umbracoAutomateApi, id, 1);
  });

  // STORY-9 AC14
  test.fixme('starts no Element Saved run when an editor sends an element to the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an element and a published Element Saved automation.
    const element = await library.element('G2 review rating');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementSaved);
    // Arrange guard, not a second outcome: a real save starts a run, so the trigger is live.
    await saveElement(umbracoApi, element.id);
    await waitForRuns(umbracoAutomateApi, id, 1);

    // Act: an editor sends the element to the recycle bin.
    await library.trash(element.id);

    // Assert: trashing starts no run; the guard's run stays the only one.
    await expectRunCountToStay(umbracoAutomateApi, id, 1);
  });

  // STORY-9 AC15
  test.fixme('starts no Element Saved run when an editor restores an element from the recycle bin', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an element already in the recycle bin, a second element to prove the trigger is
    // live, and a published Element Saved automation. Trashing comes before the automation, so
    // AC14 can't leak into this test.
    const element = await library.element('G2 review rating');
    await library.trash(element.id);
    const control = await library.element('Control element');
    const id = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementSaved);
    // Arrange guard, not a second outcome: saving the control element starts a run, so the
    // trigger is live.
    await saveElement(umbracoApi, control.id);
    await waitForRuns(umbracoAutomateApi, id, 1);

    // Act: an editor restores the element.
    await restoreElement(umbracoApi, element.id);

    // Assert: restoring starts no run; the guard's run stays the only one.
    await expectRunCountToStay(umbracoAutomateApi, id, 1);
  });

  // STORY-9 AC17
  test.fixme('starts no run in a workspace whose service account lacks Read on the element folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
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
    const restrictedId = await publishedTriggered(umbracoAutomateApi, restricted.id, elementTriggers.elementSaved);
    const controlId = await publishedTriggered(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementTriggers.elementSaved);

    // Act: an editor saves the element.
    await saveElement(umbracoApi, element.id);
    // Guard, not a second outcome: the control workspace's run shows the save was dispatched, so
    // the restricted workspace having none is down to the permission.
    await waitForRuns(umbracoAutomateApi, controlId, 1);

    // Assert: no run in the restricted workspace.
    await expectRunCountToStay(umbracoAutomateApi, restrictedId, 0);
  });

  // STORY-9 AC21
  test.fixme('starts exactly one run when the automation updates the element that triggered it', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: an element, and Element Saved (default "Skip if this would loop") → Update Element
    // Property on that same element, published.
    const element = await library.element('G2 review rating');
    const update = automationStep(ConstantHelper.actions.updateElementProperty, 'update', {
      [settingKeys.element]: element.id,
      propertyAlias: LibraryApiHelper.propertyAlias,
      value: `by-automation-${uniqueSuffix()}`
    });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Library Element No Loop'), automateServiceAccountWorkspace.id, {
      trigger: elementTrigger(elementTriggers.elementSaved),
      steps: [update],
      connections: [automationConnection('trigger', update)]
    });
    await umbracoAutomateApi.automations.publish(id);

    // Act: an editor saves the element.
    await saveElement(umbracoApi, element.id);
    // Guard, not a second outcome: the first run completed, so its Update Element Property save,
    // the one that could loop, has happened.
    const [run] = await waitForRuns(umbracoAutomateApi, id, 1);
    await umbracoAutomateApi.automations.waitForRunStatus(run.id, 'Completed');

    // Assert: the automation's own save starts no second run.
    await expectRunCountToStay(umbracoAutomateApi, id, 1);
  });
});
