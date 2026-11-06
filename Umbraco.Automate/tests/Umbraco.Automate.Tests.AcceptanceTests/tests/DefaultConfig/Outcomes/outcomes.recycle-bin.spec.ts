import { expect } from '@playwright/test';
import { test, ConstantHelper, LibraryApiHelper, automationConnection, automationStep, manualTrigger, uniqueName } from '../../../lib/index';
import { CmsContentApiHelper } from '../../../lib/helpers/CmsContentApiHelper';

/**
 * An item in the recycle bin counts as missing for every service account (#507).
 *
 * The CMS lets an account with a root start node into the recycle bin, so before the fix these
 * actions updated a trashed item, or created a new one under a trashed parent, and took their
 * success exit. An account confined to a start node already took the not-found exit, because the
 * bin sits outside every start-node path. `automateServiceAccountWorkspace` runs as an
 * Administrators account, which has the root start node, so it is the case that used to go wrong.
 *
 * Each automation is published while its item is still live, as a real one would be, and the item
 * is trashed just before the run.
 */
const { actions, outcomes } = ConstantHelper;

/* A step the run can execute without setup, whose alias shows which path ran. */
function marker(alias: string, x: number) {
  return automationStep(actions.setVariable, alias, { name: alias, value: alias }, { x, y: 450 });
}

/** trigger → `actionAlias`, with an `onSuccess` step on its success exit and an `onNotFound` step on `notFoundOutcome`. Published. */
async function seedPublished(
  umbracoAutomateApi: any,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>,
  notFoundOutcome: string
): Promise<string> {
  const branching = automationStep(actionAlias, 'branching', settings, { x: 250, y: 200 });
  const onSuccess = marker('onSuccess', 100);
  const onNotFound = marker('onNotFound', 400);
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Recycle Bin'), workspaceId, {
    trigger: manualTrigger(),
    steps: [branching, onSuccess, onNotFound],
    connections: [
      automationConnection('trigger', branching),
      automationConnection(branching, onSuccess, outcomes.success),
      automationConnection(branching, onNotFound, notFoundOutcome)
    ]
  });
  await umbracoAutomateApi.automations.publish(id);
  return id;
}

/* Runs the published automation to completion; returns the aliases of the steps that ran, in order. */
async function runToCompletion(umbracoAutomateApi: any, automationId: string): Promise<string[]> {
  const runId = await umbracoAutomateApi.automations.runUntil(automationId, 'Completed');
  const run = await umbracoAutomateApi.automations.getRun(runId);
  const automation = await umbracoAutomateApi.automations.getById(automationId);
  const aliasById = new Map<string, string>(automation.steps.map((s: any) => [s.id, s.alias]));
  return run.stepRuns.map((stepRun: any) => aliasById.get(stepRun.stepId)) as string[];
}

const elementActions = actions;

/** trigger → `actionAlias` alone, for actions with no outcomes (Publish and Unpublish Element). Published. */
async function seedPublishedStep(
  umbracoAutomateApi: any,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>
): Promise<{ id: string; stepId: string }> {
  const step = automationStep(actionAlias, 'branching', settings, { x: 250, y: 200 });
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Recycle Bin'), workspaceId, {
    trigger: manualTrigger(),
    steps: [step],
    connections: [automationConnection('trigger', step)]
  });
  await umbracoAutomateApi.automations.publish(id);
  const saved = await umbracoAutomateApi.automations.getById(id);
  return { id, stepId: umbracoAutomateApi.automations.stepByAlias(saved, 'branching').id };
}

/* Runs the published automation until it fails; returns the step run of `stepId`. */
async function runUntilFailed(umbracoAutomateApi: any, automationId: string, stepId: string) {
  const runId = await umbracoAutomateApi.automations.runUntil(automationId, 'Failed');
  return umbracoAutomateApi.automations.stepRunOf(await umbracoAutomateApi.automations.getRun(runId), stepId);
}

test.describe('Content and media in the recycle bin', () => {
  test('Update Content Property takes the notFound path for a trashed page', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const page = await cmsContent.createPage('Trashed Page');
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.updateContentProperty,
      { contentKey: page.id, propertyAlias: CmsContentApiHelper.pagePropertyAlias, value: 'never written' },
      outcomes.notFound
    );
    await cmsContent.trashPage(page.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  test('Update Media Property takes the notFound path for a trashed image', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const image = await cmsContent.createImage('Trashed Image');
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.updateMediaProperty,
      { mediaKey: image.id, propertyAlias: 'umbracoExtension', value: 'never written' },
      outcomes.notFound
    );
    await cmsContent.trashMedia(image.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  test('Create Content takes the parentNotFound path under a trashed page', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    cmsContent
  }) => {
    // Arrange
    const parent = await cmsContent.createPage('Trashed Parent');
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.createContent,
      { parentKey: parent.id, contentType: await cmsContent.pageTypeId(), name: uniqueName('Never Created') },
      outcomes.parentNotFound
    );
    await cmsContent.trashPage(parent.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  test('Create Media takes the parentNotFound path under a trashed folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    cmsContent
  }) => {
    // Arrange
    const folder = await cmsContent.createFolder('Trashed Folder');
    const folderType = await umbracoApi.mediaType.getByName('Folder');
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      actions.createMedia,
      { parentKey: folder.id, mediaType: folderType.id, name: uniqueName('Never Created') },
      outcomes.parentNotFound
    );
    await cmsContent.trashMedia(folder.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });
});

/**
 * Pending until T13 (docs/plans/library-element-actions/PLAN.md).
 *
 * The same rule for Library elements (R5, R6): an element or folder in the Library recycle bin
 * counts as missing for every service account. `automateServiceAccountWorkspace` runs as
 * Administrators, which has root Library access, as the content cases above do. Create Element
 * also runs for an account whose Library start node is a folder (STORY-7 AC12), the one element
 * criterion that names that account.
 */
test.describe('Library elements in the recycle bin', () => {
  // STORY-2 AC9
  test.fixme('Get Element takes the notFound path for a trashed element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange
    const element = await library.element('Trashed Element', { publish: true });
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.getElement,
      { elementKey: element.id },
      outcomes.notFound
    );
    await library.trash(element.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  // STORY-3 AC7
  test.fixme('Get Element Property takes the notFound path for a trashed element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange
    const element = await library.element('Trashed Element', { publish: true });
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.getElementProperty,
      { elementKey: element.id, propertyAlias: LibraryApiHelper.propertyAlias },
      outcomes.notFound
    );
    await library.trash(element.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  // STORY-4 AC14
  test.fixme('Update Element Property takes the notFound path for a trashed element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange
    const element = await library.element('Trashed Element');
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.updateElementProperty,
      { elementKey: element.id, propertyAlias: LibraryApiHelper.propertyAlias, value: 'never written' },
      outcomes.notFound
    );
    await library.trash(element.id);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  // STORY-5 AC9
  test.fixme('Publish Element fails the step for a trashed element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an unpublished element, trashed after the automation is published.
    const element = await library.element('Trashed Element');
    const { id, stepId } = await seedPublishedStep(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.publishElement,
      { elementKey: element.id }
    );
    await library.trash(element.id);

    // Act
    const stepRun = await runUntilFailed(umbracoAutomateApi, id, stepId);

    // Assert
    expect(stepRun.status).toBe('Failed');
  });

  // STORY-5 AC9
  test.fixme('Publish Element leaves a trashed element unpublished', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    umbracoApi,
    library
  }) => {
    // Arrange: as above.
    const element = await library.element('Trashed Element');
    const { id, stepId } = await seedPublishedStep(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.publishElement,
      { elementKey: element.id }
    );
    await library.trash(element.id);

    // Act
    await runUntilFailed(umbracoAutomateApi, id, stepId);

    // Assert
    expect(await umbracoApi.element.isElementPublished(element.id)).toBe(false);
  });

  // STORY-6 AC7
  test.fixme('Unpublish Element fails the step for a trashed element', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: a published element, trashed after the automation is published. The criterion's
    // "the CMS's unpublish is never called" is the unit spec's to prove; through the site, the
    // step failing is what shows.
    const element = await library.element('Trashed Element', { publish: true });
    const { id, stepId } = await seedPublishedStep(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.unpublishElement,
      { elementKey: element.id }
    );
    await library.trash(element.id);

    // Act
    const stepRun = await runUntilFailed(umbracoAutomateApi, id, stepId);

    // Assert
    expect(stepRun.status).toBe('Failed');
  });

  // STORY-7 AC11
  test.fixme('Create Element takes the parentNotFound path under a trashed folder', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateApi,
    library
  }) => {
    // Arrange
    const reviews = await library.folder('Trashed Reviews');
    const elementType = await library.elementType();
    const id = await seedPublished(
      umbracoAutomateApi,
      automateServiceAccountWorkspace.id,
      elementActions.createElement,
      { folderKey: reviews.id, elementType: elementType.id, name: uniqueName('Never Created') },
      outcomes.parentNotFound
    );
    await library.trash(reviews.id, true);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });

  // STORY-7 AC12
  test.fixme('Create Element takes the parentNotFound path under a trashed folder for a folder-scoped account', async ({
    umbracoAutomateApi,
    library
  }) => {
    // Arrange: an account whose Library start node is "Schema snippets", and "Reviews" inside it.
    const snippets = await library.folder('Schema snippets');
    const reviews = await library.folder('Trashed Reviews', snippets.id);
    const elementType = await library.elementType();
    const workspace = await library.workspaceStartingAt(snippets.id);
    const id = await seedPublished(
      umbracoAutomateApi,
      workspace.id,
      elementActions.createElement,
      { folderKey: reviews.id, elementType: elementType.id, name: uniqueName('Never Created') },
      outcomes.parentNotFound
    );
    await library.trash(reviews.id, true);

    // Act
    const ran = await runToCompletion(umbracoAutomateApi, id);

    // Assert
    expect(ran).toEqual(['branching', 'onNotFound']);
  });
});
