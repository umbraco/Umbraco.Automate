import { randomUUID } from 'crypto';
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
 * Action outcomes on a real action: Get Content declares a `success` exit (the default, labelled
 * "Found") and a `notFound` exit. These specs draw, save, publish and run against the demo site,
 * then assert on the saved model, the run record, and what the canvas and run view show.
 *
 * Covers S3 (exits and layout), S6 (old unnamed lines as "Any result"), S9 (Get Content routes
 * to the right exit) and S10 (the run view shows the exit taken).
 *
 * Runs need an execution identity, so every spec uses the tier-2 `automateServiceAccountWorkspace`.
 * The "found" run reads content that already exists on the demo site; nothing is published here.
 */
const { actions, outcomes, outcomeLabels } = ConstantHelper;

type Seeded = {
  id: string;
  getContentId: string;
  /** Step ids by alias, as the server assigned them. */
  stepIds: Record<string, string>;
};

/* A step the run can execute without any setup, and whose alias shows which path ran. */
function marker(alias: string, y: number) {
  return automationStep(actions.setVariable, alias, { name: alias, value: alias }, { x: 250, y });
}

/**
 * trigger → Get Content, plus one marker step per entry in `lines`, each wired from the given
 * handle. A `null` handle is an old unnamed line (the shape saved before Get Content declared
 * outcomes). Markers with no line are seeded unconnected, for a spec to draw to.
 */
async function seedGetContent(
  umbracoAutomateApi: any,
  workspaceId: string,
  contentKey: string,
  lines: { alias: string; handle: string | null | undefined }[]
): Promise<Seeded> {
  const getContent = automationStep(actions.getContent, 'getContent', { contentKey }, { x: 250, y: 200 });
  const markers = lines.map((line, index) => marker(line.alias, 420 + index * 180));
  const connections = [
    automationConnection('trigger', getContent),
    ...lines.flatMap((line, index) =>
      line.handle === undefined ? [] : [automationConnection(getContent, markers[index], line.handle)]
    )
  ];
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Get Content'), workspaceId, {
    trigger: manualTrigger(),
    steps: [getContent, ...markers],
    connections
  });
  const saved = await umbracoAutomateApi.automations.getById(id);
  const stepIds: Record<string, string> = {};
  for (const step of saved.steps) {
    stepIds[step.alias] = step.id;
  }
  return { id, getContentId: stepIds['getContent'], stepIds };
}

/* trigger → first → second, all Set Variable: steps that declare no outcomes and take no exit. */
async function seedPlainChain(umbracoAutomateApi: any, workspaceId: string) {
  const first = marker('first', 200);
  const second = marker('second', 400);
  const id = await umbracoAutomateApi.automations.create(uniqueName('Outcomes Plain'), workspaceId, {
    trigger: manualTrigger(),
    steps: [first, second],
    connections: [automationConnection('trigger', first), automationConnection(first, second)]
  });
  const saved = await umbracoAutomateApi.automations.getById(id);
  return {
    id,
    firstId: umbracoAutomateApi.automations.stepByAlias(saved, 'first').id as string,
    secondId: umbracoAutomateApi.automations.stepByAlias(saved, 'second').id as string
  };
}

/* Runs the automation to completion and returns the run detail. */
async function publishAndRun(umbracoAutomateApi: any, automationId: string) {
  const runId = await umbracoAutomateApi.automations.publishAndRunUntil(automationId, 'Completed');
  return { runId, run: await umbracoAutomateApi.automations.getRun(runId) };
}

/* The steps that ran, by alias, in execution order. */
function ranAliases(run: any, automation: any): string[] {
  const aliasById = new Map<string, string>(automation.steps.map((s: any) => [s.id, s.alias]));
  return run.stepRuns.map((stepRun: any) => aliasById.get(stepRun.stepId));
}

test.describe('Get Content outcomes', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.describe('exits on the canvas', () => {
    test('shows a Found (default) and a Not found exit, in that order', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, getContentId } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(getContentId)).toHaveText([
        outcomeLabels.withDefault(outcomeLabels.found),
        outcomeLabels.notFound
      ]);
    });

    test('draws both exits on the right edge of the node, not the bottom (S3 AC9)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, getContentId } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — xyflow marks a handle's side with a class.
      const handles = umbracoAutomateUi.automate.canvasNode(getContentId).locator('.react-flow__handle.source');
      await expect(handles).toHaveClass([/react-flow__handle-right/, /react-flow__handle-right/]);
    });

    test('gives each exit an add button', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, getContentId } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), []);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.addActionButton(getContentId, outcomes.success)).toBeVisible();
      await expect(umbracoAutomateUi.automate.addActionButton(getContentId, outcomes.notFound)).toBeVisible();
    });

    test('saves a line drawn from the Not found exit as outcome "notFound" (S3 AC5)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — an unconnected marker step to draw to.
      const { id, getContentId, stepIds } = await seedGetContent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        randomUUID(),
        [{ alias: 'onNotFound', handle: undefined }]
      );

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.connectHandles(getContentId, outcomes.notFound, stepIds['onNotFound']);
      await expect(umbracoAutomateUi.automate.canvasEdge(getContentId, stepIds['onNotFound'])).toHaveCount(1);
      await umbracoAutomateUi.automate.clickSave();

      // Assert
      await expect
        .poll(async () => (await umbracoAutomateApi.automations.getById(id)).connections.length, { timeout: 15000 })
        .toBe(2);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const line = saved.connections.find((c: any) => c.targetStepId === stepIds['onNotFound']);
      expect(line.sourceStepId).toBe(getContentId);
      expect(line.sourceHandle).toBe(outcomes.notFound);
      expect(line.outcome).toBe(outcomes.notFound);
    });

    test('inserting a Get Content step onto a line continues through its default exit (S3 AC6)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — trigger → first → second; insert onto the first → second line.
      const { id, firstId, secondId } = await seedPlainChain(umbracoAutomateApi, automateServiceAccountWorkspace.id);
      const seeded = await umbracoAutomateApi.automations.getById(id);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Act
      await umbracoAutomateUi.automate.clickEdgeInsert(firstId, secondId);
      await umbracoAutomateUi.automate.chooseActionInPicker(await umbracoAutomateApi.catalogue.getStepTypeName(actions.getContent));
      await umbracoAutomateUi.automate.nodeSettingsModal.waitFor({ state: 'visible' });
      await umbracoAutomateUi.automate.changeStepSettingAndSave('Content Key', randomUUID());
      await umbracoAutomateUi.automate.clickSave();

      // Assert — the inserted Get Content step carries the old line on its default exit, `success`.
      await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(3);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
      const line = saved.connections.find((c: any) => c.sourceStepId === added.id);
      expect(line.targetStepId).toBe(secondId);
      expect(line.sourceHandle).toBe(outcomes.success);
      expect(line.outcome).toBe(outcomes.success);
    });

    test('draws a plain action with one unnamed exit on the bottom (S3 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, firstId } = await seedPlainChain(umbracoAutomateApi, automateServiceAccountWorkspace.id);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — no exit rows, and a single source handle with no id, on the bottom.
      await expect(umbracoAutomateUi.automate.outcomeExits(firstId)).toHaveCount(0);
      const handles = umbracoAutomateUi.automate.canvasSourceHandles(firstId);
      await expect(handles).toHaveCount(1);
      await expect(handles).toHaveClass(/react-flow__handle-bottom/);
      await expect(handles).not.toHaveAttribute('data-handleid', /.+/);
    });

    test('does not overlap the Get Content node when a step is added from an exit (S3 AC11)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — steps already on both exits, laid out where the canvas would put them.
      const { id, getContentId, stepIds } = await seedGetContent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        randomUUID(),
        [
          { alias: 'onFound', handle: outcomes.success },
          { alias: 'onNotFound', handle: outcomes.notFound }
        ]
      );
      const seeded = await umbracoAutomateApi.automations.getById(id);

      // Act — add a third step from the Not found exit.
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.addActionFromNode(
        getContentId,
        await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay),
        outcomes.notFound
      );
      await umbracoAutomateUi.automate.submitNodeSettings();
      await umbracoAutomateUi.automate.clickSave();

      // Assert — no node's rendered box overlaps another's.
      await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(4);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
      const ids = [getContentId, stepIds['onFound'], stepIds['onNotFound'], added.id];
      const boxes = await Promise.all(ids.map((stepId) => umbracoAutomateUi.automate.canvasNode(stepId).boundingBox()));
      expect(boxes.every((box) => box !== null)).toBe(true);
      const overlapping: string[] = [];
      for (let a = 0; a < ids.length; a++) {
        for (let b = a + 1; b < ids.length; b++) {
          const first = boxes[a]!;
          const second = boxes[b]!;
          const overlaps =
            first.x < second.x + second.width &&
            second.x < first.x + first.width &&
            first.y < second.y + second.height &&
            second.y < first.y + first.height;
          if (overlaps) {
            overlapping.push(`${ids[a]} / ${ids[b]}`);
          }
        }
      }
      expect(overlapping).toEqual([]);
    });
  });

  test.describe('running', () => {
    test('takes the notFound path when the content key does not exist (S9 AC5, AC8)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange — a key that cannot exist, with a step on each exit.
      const { id } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
        { alias: 'onFound', handle: outcomes.success },
        { alias: 'onNotFound', handle: outcomes.notFound }
      ]);

      // Act
      const { run } = await publishAndRun(umbracoAutomateApi, id);

      // Assert — the step did not fail, and only the notFound path ran.
      expect(ranAliases(run, await umbracoAutomateApi.automations.getById(id))).toEqual(['getContent', 'onNotFound']);
    });

    test('takes the success path when the content key exists (S9 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange — content already on the demo site; nothing is published for this.
      const contentKey = await umbracoAutomateApi.content.getPublishedRootDocumentKey();
      const { id } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, contentKey, [
        { alias: 'onFound', handle: outcomes.success },
        { alias: 'onNotFound', handle: outcomes.notFound }
      ]);

      // Act
      const { run } = await publishAndRun(umbracoAutomateApi, id);

      // Assert
      expect(ranAliases(run, await umbracoAutomateApi.automations.getById(id))).toEqual(['getContent', 'onFound']);
    });
  });

  test.describe('an old unnamed line ("Any result")', () => {
    test('shows as an Any result exit after the declared ones (S6 AC1)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — a line with no handle and no outcome, as saved before the action declared any.
      const { id, getContentId } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
        { alias: 'always', handle: null }
      ]);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(getContentId)).toHaveText([
        outcomeLabels.withDefault(outcomeLabels.found),
        outcomeLabels.notFound,
        outcomeLabels.anyResult
      ]);
    });

    test('has no add button, so no new Any result lines can be drawn (S6 AC3)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, getContentId } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
        { alias: 'always', handle: null }
      ]);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExit(getContentId, outcomes.anyResult)).toBeVisible();
      await expect(umbracoAutomateUi.automate.outcomeExit(getContentId, outcomes.anyResult).getByRole('button')).toHaveCount(0);
    });

    test('still fires when the content is not found (S6 AC5)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
        { alias: 'always', handle: null }
      ]);

      // Act
      const { run } = await publishAndRun(umbracoAutomateApi, id);

      // Assert
      expect(ranAliases(run, await umbracoAutomateApi.automations.getById(id))).toEqual(['getContent', 'always']);
    });

    test('still fires when the content is found (S6 AC6)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const contentKey = await umbracoAutomateApi.content.getPublishedRootDocumentKey();
      const { id } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, contentKey, [
        { alias: 'always', handle: null }
      ]);

      // Act
      const { run } = await publishAndRun(umbracoAutomateApi, id);

      // Assert
      expect(ranAliases(run, await umbracoAutomateApi.automations.getById(id))).toEqual(['getContent', 'always']);
    });

    test('saves unchanged when nothing is edited (S6 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, getContentId, stepIds } = await seedGetContent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        randomUUID(),
        [{ alias: 'always', handle: null }]
      );
      const before = (await umbracoAutomateApi.automations.getById(id)).version;

      // Act — rename through the canvas-owned save so the canvas serialises the graph back.
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.clickSave();

      // Assert — the line still has no handle and no outcome.
      await expect
        .poll(async () => (await umbracoAutomateApi.automations.getById(id)).version, { timeout: 15000 })
        .not.toBe(before);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const line = saved.connections.find((c: any) => c.sourceStepId === getContentId);
      expect(line.targetStepId).toBe(stepIds['always']);
      expect(line.sourceHandle).toBeNull();
      expect(line.outcome).toBeNull();
    });

    test('moving the line to the default exit saves it as outcome "success" (S6 AC7)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — an old unnamed line to "always".
      const { id, getContentId, stepIds } = await seedGetContent(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        randomUUID(),
        [{ alias: 'always', handle: null }]
      );
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Act — remove the Any result line and draw it again from the Found exit, then save.
      await umbracoAutomateUi.automate.deleteEdge(getContentId, stepIds['always']);
      await umbracoAutomateUi.automate.connectHandles(getContentId, outcomes.success, stepIds['always']);
      await expect(umbracoAutomateUi.automate.canvasEdge(getContentId, stepIds['always'])).toHaveCount(1);
      await umbracoAutomateUi.automate.clickSave();

      // Assert
      await expect
        .poll(async () => {
          const saved = await umbracoAutomateApi.automations.getById(id);
          return saved.connections.find((c: any) => c.sourceStepId === getContentId)?.outcome;
        }, { timeout: 15000 })
        .toBe(outcomes.success);
      const saved = await umbracoAutomateApi.automations.getById(id);
      const line = saved.connections.find((c: any) => c.sourceStepId === getContentId);
      expect(line.sourceHandle).toBe(outcomes.success);
      expect(line.targetStepId).toBe(stepIds['always']);
    });

    test.describe('next to a named line', () => {
      /* Any result → always, plus an unconnected step to draw a Found line to. */
      async function openWithAnyResultAndSpareStep(
        automateServiceAccountWorkspace: { id: string },
        umbracoAutomateUi: any,
        umbracoAutomateApi: any
      ) {
        const seeded = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
          { alias: 'always', handle: null },
          { alias: 'spare', handle: undefined }
        ]);
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(seeded.id));
        await umbracoAutomateUi.automate.waitForCanvas();
        return seeded;
      }

      test('warns that both paths run once a Found line is drawn (S6 AC8)', async ({
        automateServiceAccountWorkspace,
        umbracoAutomateUi,
        umbracoAutomateApi
      }) => {
        // Arrange
        const { getContentId, stepIds } = await openWithAnyResultAndSpareStep(
          automateServiceAccountWorkspace,
          umbracoAutomateUi,
          umbracoAutomateApi
        );
        await expect(umbracoAutomateUi.automate.outcomeWarning(getContentId)).toHaveCount(0);

        // Act
        await umbracoAutomateUi.automate.connectHandles(getContentId, outcomes.success, stepIds['spare']);

        // Assert
        await expect(umbracoAutomateUi.automate.outcomeWarning(getContentId)).toBeVisible();
      });

      test('clears the warning when the Any result line is removed (S6 AC9)', async ({
        automateServiceAccountWorkspace,
        umbracoAutomateUi,
        umbracoAutomateApi
      }) => {
        // Arrange — the warning is showing.
        const { getContentId, stepIds } = await openWithAnyResultAndSpareStep(
          automateServiceAccountWorkspace,
          umbracoAutomateUi,
          umbracoAutomateApi
        );
        await umbracoAutomateUi.automate.connectHandles(getContentId, outcomes.success, stepIds['spare']);
        await expect(umbracoAutomateUi.automate.outcomeWarning(getContentId)).toBeVisible();

        // Act
        await umbracoAutomateUi.automate.deleteEdge(getContentId, stepIds['always']);

        // Assert
        await expect(umbracoAutomateUi.automate.outcomeWarning(getContentId)).toHaveCount(0);
      });

      test('drops the Any result exit with its line (S6 AC2)', async ({
        automateServiceAccountWorkspace,
        umbracoAutomateUi,
        umbracoAutomateApi
      }) => {
        // Arrange
        const { getContentId, stepIds } = await openWithAnyResultAndSpareStep(
          automateServiceAccountWorkspace,
          umbracoAutomateUi,
          umbracoAutomateApi
        );

        // Act
        await umbracoAutomateUi.automate.deleteEdge(getContentId, stepIds['always']);

        // Assert
        await expect(umbracoAutomateUi.automate.outcomeExits(getContentId)).toHaveText([
          outcomeLabels.withDefault(outcomeLabels.found),
          outcomeLabels.notFound
        ]);
      });
    });
  });

  test.describe('publishing a stale line', () => {
    test('is refused with the stale-outcome error (S5 AC5)', async ({
      page,
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — a line from "b", an outcome Get Content does not declare. Draft saves allow it.
      const { id } = await seedGetContent(umbracoAutomateApi, automateServiceAccountWorkspace.id, randomUUID(), [
        { alias: 'decide', handle: 'b' }
      ]);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.clickSaveAndPublish();

      // Assert — named for the Get Content step, which owns the line, not the step it points at.
      await expect(
        page.locator('uui-toast-notification').filter({
          hasText: ConstantHelper.staleOutcomeError('getContent', 'b')
        })
      ).toBeVisible();
      expect((await umbracoAutomateApi.automations.getById(id)).status).not.toBe('Published');
    });
  });

  test.describe('the run view', () => {
    /* A completed run that took the notFound exit: Found → onFound, Not found → onNotFound. */
    async function runThatTookNotFound(workspaceId: string, umbracoAutomateApi: any) {
      const seeded = await seedGetContent(umbracoAutomateApi, workspaceId, randomUUID(), [
        { alias: 'onFound', handle: outcomes.success },
        { alias: 'onNotFound', handle: outcomes.notFound }
      ]);
      const { runId } = await publishAndRun(umbracoAutomateApi, seeded.id);
      return { ...seeded, runId };
    }

    test('styles the line from the taken Not found exit as taken (S10 AC4)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { runId, getContentId, stepIds } = await runThatTookNotFound(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.runWorkspaceUrl(runId, 'canvas'));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.canvasEdge(getContentId, stepIds['onNotFound'])).toHaveClass(/ua-edge--taken/);
    });

    test('styles the line from the Found exit as not taken (S10 AC5)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { runId, getContentId, stepIds } = await runThatTookNotFound(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.runWorkspaceUrl(runId, 'canvas'));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.canvasEdge(getContentId, stepIds['onFound'])).toHaveClass(/ua-edge--not-taken/);
    });

    test('shows no add buttons on the exits (S3 AC7)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { runId, getContentId } = await runThatTookNotFound(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.runWorkspaceUrl(runId, 'canvas'));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert — the exits are drawn, with nothing to click.
      await expect(umbracoAutomateUi.automate.outcomeExits(getContentId)).toHaveCount(2);
      await expect(umbracoAutomateUi.automate.canvasNode(getContentId).getByRole('button', { name: /^Add action/ })).toHaveCount(0);
    });

    /* A completed run of trigger → first → second, neither of which branches. */
    async function plainRun(workspaceId: string, umbracoAutomateApi: any) {
      const seeded = await seedPlainChain(umbracoAutomateApi, workspaceId);
      const { runId } = await publishAndRun(umbracoAutomateApi, seeded.id);
      return { ...seeded, runId };
    }

    test('shows no "Exit taken" row for a step that does not branch (S10 AC8)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, runId } = await plainRun(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
      await umbracoAutomateUi.automate.openRun(runId);
      await umbracoAutomateUi.automate.openStepRunTab(0, 'details');

      // Assert — the Details tab is open (it has a Started row), and has no exit row.
      await expect(umbracoAutomateUi.automate.runDetailStep(0).locator('umb-property-layout').first()).toBeVisible();
      await expect(umbracoAutomateUi.automate.stepRunExitTaken(0)).toHaveCount(0);
    });

    test('styles a non-branching step\'s line as neither taken nor not taken (S10 AC8)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { runId, firstId, secondId } = await plainRun(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.runWorkspaceUrl(runId, 'canvas'));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      const line = umbracoAutomateUi.automate.canvasEdge(firstId, secondId);
      await expect(line).toHaveCount(1);
      await expect(line).not.toHaveClass(/ua-edge--(taken|not-taken)/);
    });

    test('shows "Exit taken: Not found" in the step detail (S10 AC6)', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, runId } = await runThatTookNotFound(automateServiceAccountWorkspace.id, umbracoAutomateApi);

      // Act — the Get Content step is the first step run.
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationRunsUrl(id));
      await umbracoAutomateUi.automate.openRun(runId);
      await umbracoAutomateUi.automate.openStepRunTab(0, 'details');

      // Assert
      await expect(umbracoAutomateUi.automate.stepRunExitTaken(0)).toHaveText(outcomeLabels.notFound);
    });
  });
});
