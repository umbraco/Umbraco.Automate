import { expect } from '@playwright/test';
import { test, ConstantHelper, automationConnection, automationStep, manualTrigger, uniqueName } from '../../../lib/index';
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
