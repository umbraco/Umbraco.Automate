import { randomUUID } from 'crypto';
import { expect } from '@playwright/test';
import {
  test,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName,
  uniqueSuffix
} from '../../../lib/index';

/**
 * The other built-in content and media actions declare exits too. This proves, through the real
 * site, that a missing item (or an empty search) takes the `notFound` exit instead of failing the
 * step, for one action of each group, and that a second action of each group draws its declared
 * exits with their translated labels.
 *
 * Nothing here publishes or creates content: the "found" case reads what is already on the demo
 * site, and every not-found case uses a key or a name that cannot exist. Runs need an execution
 * identity, so every spec uses the tier-2 `automateServiceAccountWorkspace`.
 */
const { actions, outcomes, outcomeLabels } = ConstantHelper;

type Seeded = { id: string; branchingId: string };

/* A step the run can execute without setup, whose alias shows which path ran. */
function marker(alias: string, x: number) {
  return automationStep(actions.setVariable, alias, { name: alias, value: alias }, { x, y: 450 });
}

/** trigger → `actionAlias` (alias "branching"), with an `onSuccess` and an `onNotFound` step on its exits. */
async function seedBranching(
  umbracoAutomateApi: any,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>
): Promise<Seeded> {
  const branching = automationStep(actionAlias, 'branching', settings, { x: 250, y: 200 });
  const onSuccess = marker('onSuccess', 100);
  const onNotFound = marker('onNotFound', 400);
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Built-in'), workspaceId, {
    trigger: manualTrigger(),
    steps: [branching, onSuccess, onNotFound],
    connections: [
      automationConnection('trigger', branching),
      automationConnection(branching, onSuccess, outcomes.success),
      automationConnection(branching, onNotFound, outcomes.notFound)
    ]
  });
  const saved = await umbracoAutomateApi.automations.getById(id);
  return { id, branchingId: umbracoAutomateApi.automations.stepByAlias(saved, 'branching').id };
}

/* Publishes and runs to completion; returns the run and the aliases of the steps that ran, in order. */
async function runToCompletion(umbracoAutomateApi: any, automationId: string) {
  const runId = await umbracoAutomateApi.automations.publishAndRunUntil(automationId, 'Completed');
  const run = await umbracoAutomateApi.automations.getRun(runId);
  const automation = await umbracoAutomateApi.automations.getById(automationId);
  const aliasById = new Map<string, string>(automation.steps.map((s: any) => [s.id, s.alias]));
  return { run, ran: run.stepRuns.map((stepRun: any) => aliasById.get(stepRun.stepId)) as string[] };
}

test.describe('Built-in content and media outcomes', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.describe('Get Media', () => {
    test('takes the notFound path for a media key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMedia, {
        mediaKey: randomUUID()
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert — the step did not fail, and only the notFound path ran.
      expect(ran).toEqual(['branching', 'onNotFound']);
    });

    test('records "notFound" as the exit the step took', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, branchingId } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMedia, {
        mediaKey: randomUUID()
      });

      // Act
      const { run } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      const stepRun = run.stepRuns.find((s: any) => s.stepId === branchingId);
      expect(stepRun.status).toBe('Completed');
      expect(stepRun.branchOutcome).toBe(outcomes.notFound);
    });
  });

  test.describe('Find Content', () => {
    test('takes the notFound path when no content matches the name', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange — a name nothing can have.
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.findContent, {
        name: `no-such-content-${uniqueSuffix()}`,
        matchMode: 'Exact'
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });

    test('takes the success path when content matches the name', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange — the name of content already on the demo site.
      const { name } = await umbracoAutomateApi.content.getPublishedRootDocument();
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.findContent, {
        name,
        matchMode: 'Contains'
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onSuccess']);
    });
  });

  test.describe('Update Content Property', () => {
    test('takes the notFound path for a content key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.updateContentProperty, {
        contentKey: randomUUID(),
        propertyAlias: 'title',
        value: 'never written'
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });
  });

  test.describe('exits on the canvas', () => {
    test('Get Media Property draws Found (default), Not found and Property not found', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, branchingId } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getMediaProperty, {
        mediaKey: randomUUID(),
        propertyAlias: 'umbracoFile'
      });

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(branchingId)).toHaveText([
        outcomeLabels.withDefault(outcomeLabels.found),
        outcomeLabels.notFound,
        outcomeLabels.propertyNotFound
      ]);
    });

    test('Update Content Property draws Updated (default), Not found and Property not found', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, branchingId } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.updateContentProperty, {
        contentKey: randomUUID(),
        propertyAlias: 'title',
        value: 'x'
      });

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(branchingId)).toHaveText([
        outcomeLabels.withDefault(outcomeLabels.updated),
        outcomeLabels.notFound,
        outcomeLabels.propertyNotFound
      ]);
    });
  });
});
