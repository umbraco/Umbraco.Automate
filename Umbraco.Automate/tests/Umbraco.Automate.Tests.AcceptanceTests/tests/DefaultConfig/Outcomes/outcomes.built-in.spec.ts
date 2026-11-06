import { randomUUID } from 'crypto';
import { expect } from '@playwright/test';
import {
  test,
  ConstantHelper,
  LibraryApiHelper,
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
const { actions, outcomes, outcomeLabels, outcomeDescriptions } = ConstantHelper;
const elementActions = actions;

type Seeded = { id: string; branchingId: string };

/* A step the run can execute without setup, whose alias shows which path ran. */
function marker(alias: string, x: number) {
  return automationStep(actions.setVariable, alias, { name: alias, value: alias }, { x, y: 450 });
}

/**
 * trigger → `actionAlias` (alias "branching"), with an `onSuccess` and an `onNotFound` step on its
 * exits. `notFoundOutcome` names the second exit, for actions whose not-found exit has another key.
 */
async function seedBranching(
  umbracoAutomateApi: any,
  workspaceId: string,
  actionAlias: string,
  settings: Record<string, unknown>,
  notFoundOutcome: string = outcomes.notFound
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
      automationConnection(branching, onNotFound, notFoundOutcome)
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

  test.describe('Get Element', () => {
    // STORY-2 AC8
    test('takes the notFound path for an element key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.getElement, {
        elementKey: randomUUID()
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });
  });

  test.describe('Get Element Property', () => {
    // STORY-3 AC6
    test('takes the notFound path for an element key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.getElementProperty, {
        elementKey: randomUUID(),
        propertyAlias: LibraryApiHelper.propertyAlias
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });

    // STORY-3 AC8
    test('takes the propertyNotFound path for a property the element type does not have', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: a published element whose type has no `rating` property.
      const element = await library.element('G2 review rating', { publish: true });
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.getElementProperty,
        { elementKey: element.id, propertyAlias: 'rating' },
        outcomes.propertyNotFound
      );

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });
  });

  test.describe('Update Element Property', () => {
    // STORY-4 AC1
    test('writes the value to the element', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: an element whose property holds "old".
      const element = await library.element('G2 review rating', { text: 'old' });
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
        elementKey: element.id,
        propertyAlias: LibraryApiHelper.propertyAlias,
        value: 'new'
      });

      // Act
      await runToCompletion(umbracoAutomateApi, id);

      // Assert: the element's saved value is "new".
      expect(await library.propertyValue(element.id)).toBe('new');
    });

    // STORY-4 AC12
    test('takes the notFound path for an element key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, elementActions.updateElementProperty, {
        elementKey: randomUUID(),
        propertyAlias: LibraryApiHelper.propertyAlias,
        value: 'never written'
      });

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });

    // STORY-4 AC16
    test('takes the propertyNotFound path for a property the element type does not have', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: an element whose type has no `rating` property.
      const element = await library.element('G2 review rating');
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.updateElementProperty,
        { elementKey: element.id, propertyAlias: 'rating', value: 'never written' },
        outcomes.propertyNotFound
      );

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });
  });

  test.describe('Create Element', () => {
    // STORY-7 AC1
    test('creates an element of the type and name in the folder', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: folder "Reviews" and element type "Schema snippet".
      const reviews = await library.folder('Reviews');
      const elementType = await library.elementType('Schema snippet');
      const name = uniqueName('Acme – AggregateRating');
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.createElement,
        {
          folderKey: reviews.id,
          elementType: elementType.id,
          name
        },
        outcomes.parentNotFound
      );

      // Act
      await runToCompletion(umbracoAutomateApi, id);

      // Assert: an element of that type, with that name, is in "Reviews".
      const created = await library.adoptByName(name);
      expect(await library.childNames(reviews.id)).toContain(name);
      expect(created?.documentType?.id).toBe(elementType.id);
    });

    // STORY-7 AC1
    test('takes the success path when the element is created', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: as above.
      const reviews = await library.folder('Reviews');
      const elementType = await library.elementType('Schema snippet');
      const name = uniqueName('Acme – AggregateRating');
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.createElement,
        {
          folderKey: reviews.id,
          elementType: elementType.id,
          name
        },
        outcomes.parentNotFound
      );

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);
      await library.adoptByName(name);

      // Assert
      expect(ran).toEqual(['branching', 'onSuccess']);
    });

    // STORY-7 AC10
    test('takes the parentNotFound path for a folder key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange
      const elementType = await library.elementType();
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.createElement,
        {
          folderKey: randomUUID(),
          elementType: elementType.id,
          name: uniqueName('Never created')
        },
        outcomes.parentNotFound
      );

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });

    // STORY-7 AC14
    test('takes the elementTypeNotFound path for an element type key that does not exist', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateApi,
      library
    }) => {
      // Arrange: a real folder, so only the element type is missing.
      const reviews = await library.folder('Reviews');
      const { id } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.createElement,
        {
          folderKey: reviews.id,
          elementType: randomUUID(),
          name: uniqueName('Never created')
        },
        outcomes.elementTypeNotFound
      );

      // Act
      const { ran } = await runToCompletion(umbracoAutomateApi, id);

      // Assert
      expect(ran).toEqual(['branching', 'onNotFound']);
    });
  });

  test.describe('exits on the canvas', () => {
    test('Get Content Property shows its description as the Found exit tooltip', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, branchingId } = await seedBranching(umbracoAutomateApi, automateServiceAccountWorkspace.id, actions.getContentProperty, {
        contentKey: randomUUID(),
        propertyAlias: 'title'
      });

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(
        umbracoAutomateUi.automate.outcomeExit(branchingId, outcomes.success).locator('.ua-node__switch-case-label')
      ).toHaveAttribute('title', outcomeDescriptions.propertyFound);
    });

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

    // STORY-8 AC9
    test('Create Element draws Created (default), Folder not found and Element type not found', async ({
      automateServiceAccountWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const { id, branchingId } = await seedBranching(
        umbracoAutomateApi,
        automateServiceAccountWorkspace.id,
        elementActions.createElement,
        {
          folderKey: randomUUID(),
          elementType: randomUUID(),
          name: 'x'
        },
        outcomes.parentNotFound
      );

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();

      // Assert
      await expect(umbracoAutomateUi.automate.outcomeExits(branchingId)).toHaveText([
        outcomeLabels.withDefault(outcomeLabels.created),
        outcomeLabels.folderNotFound,
        outcomeLabels.elementTypeNotFound
      ]);
    });
  });
});
