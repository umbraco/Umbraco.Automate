import { expect } from '@playwright/test';
import {
  test,
  AutomateUiHelper,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName
} from '../../../lib/index';

/**
 * Building a workflow on the canvas: where new steps land, how they are wired, and what survives
 * the edit. Each spec seeds a graph through the API, edits it through the canvas's own buttons,
 * saves, and then asserts on the saved **model** — connections, source handles, positions — so
 * a regression reports the wiring that broke rather than a screenshot diff.
 *
 * Every spec uses the tier-2 `automateServiceAccountWorkspace`, even those that never run or
 * publish: the action picker is scoped to what the workspace's service account may do, so in a
 * tier-1 workspace it lists control flows only and no actions at all.
 *
 * Covers #322 (placement beside the clicked handle), #323 (several connections into one step),
 * #324 (inserting a container keeps the downstream step), #325 (actions needing a connection the
 * workspace does not allow) and #346 (+ on a connected output inserts rather than replaces).
 */
const { actions, handles } = ConstantHelper;

/* Step types are chosen in the picker by their catalogue name, read from the API rather than
 * hard-coded. Delay and While are used because every required setting has a default, so the
 * settings modal that follows each pick can be saved as-is. */

/* Connections out of one step, optionally from one handle. */
function connectionsFrom(automation: any, stepId: string, handle?: string | null) {
  return automation.connections.filter(
    (c: any) => c.sourceStepId === stepId && (handle === undefined || (c.sourceHandle ?? null) === handle)
  );
}

test.describe('Automation canvas', () => {
  test('publishes when two branches rejoin on a shared step (#323)', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → If; If(true) → trueStep → merge. If(false) is left free.
    const ifStep = automationStep(actions.if, 'branch', { conditions: { groups: [] } }, { x: 250, y: 200 });
    const trueStep = automationStep(actions.delay, 'trueStep', { duration: '00:00:01' }, { x: 100, y: 400 });
    const merge = automationStep(actions.delay, 'merge', { duration: '00:00:01' }, { x: 250, y: 600 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Rejoin'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [ifStep, trueStep, merge],
      connections: [
        automationConnection('trigger', ifStep),
        automationConnection(ifStep, trueStep, handles.ifTrue),
        automationConnection(trueStep, merge)
      ]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const ifId = umbracoAutomateApi.automations.stepByAlias(seeded, 'branch').id;
    const trueId = umbracoAutomateApi.automations.stepByAlias(seeded, 'trueStep').id;
    const mergeId = umbracoAutomateApi.automations.stepByAlias(seeded, 'merge').id;

    // Act — draw If(false) into the step the true branch already reaches, then publish.
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.connectHandles(ifId, handles.ifFalse, mergeId);
    await expect(umbracoAutomateUi.automate.canvasEdge(ifId, mergeId)).toHaveCount(1);
    await umbracoAutomateUi.automate.clickSaveAndPublish();

    // Assert — published, with both incoming connections on the shared step.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).status).toBe('Published');
    const saved = await umbracoAutomateApi.automations.getById(id);
    const intoMerge = saved.connections.filter((c: any) => c.targetStepId === mergeId);
    expect(intoMerge).toHaveLength(2);
    expect(connectionsFrom(saved, ifId, handles.ifFalse).map((c: any) => c.targetStepId)).toEqual([mergeId]);
    expect(connectionsFrom(saved, ifId, handles.ifTrue).map((c: any) => c.targetStepId)).toEqual([trueId]);
    expect(connectionsFrom(saved, trueId).map((c: any) => c.targetStepId)).toEqual([mergeId]);
  });

  test("adding to an Approval's rejected output keeps the approved step connected", async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → Approval; Approval(approved) → approvedStep.
    const approval = automationStep(actions.requestApproval, 'approval', {}, { x: 250, y: 200 });
    const approvedStep = automationStep(actions.delay, 'approvedStep', { duration: '00:00:01' }, { x: 100, y: 400 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Approval'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [approval, approvedStep],
      connections: [
        automationConnection('trigger', approval),
        automationConnection(approval, approvedStep, handles.approved)
      ]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const approvalId = umbracoAutomateApi.automations.stepByAlias(seeded, 'approval').id;
    const approvedId = umbracoAutomateApi.automations.stepByAlias(seeded, 'approvedStep').id;

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionFromNode(approvalId, await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay), handles.rejected);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(3);
    const saved = await umbracoAutomateApi.automations.getById(id);
    const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
    expect(connectionsFrom(saved, approvalId, handles.approved).map((c: any) => c.targetStepId)).toEqual([approvedId]);
    expect(connectionsFrom(saved, approvalId, handles.rejected).map((c: any) => c.targetStepId)).toEqual([added.id]);
  });

  test('places a step added to a second output beside the existing one, not on top of it (#322)', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → If; If(true) → trueStep, sitting where the canvas would place it.
    const ifStep = automationStep(actions.if, 'branch', { conditions: { groups: [] } }, { x: 250, y: 200 });
    const trueStep = automationStep(actions.delay, 'trueStep', { duration: '00:00:01' }, { x: 150, y: 380 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Placement'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [ifStep, trueStep],
      connections: [automationConnection('trigger', ifStep), automationConnection(ifStep, trueStep, handles.ifTrue)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const ifId = umbracoAutomateApi.automations.stepByAlias(seeded, 'branch').id;
    const trueId = umbracoAutomateApi.automations.stepByAlias(seeded, 'trueStep').id;

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionFromNode(ifId, await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay), handles.ifFalse);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert — the saved positions differ, and the rendered nodes do not overlap.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(3);
    const saved = await umbracoAutomateApi.automations.getById(id);
    const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
    const existing = umbracoAutomateApi.automations.stepByAlias(saved, 'trueStep');
    expect(added.position).not.toEqual(existing.position);
    // The false handle sits right of centre, so its step goes to the right of the true branch.
    expect(added.position.x).toBeGreaterThan(existing.position.x);

    const addedBox = await umbracoAutomateUi.automate.canvasNode(added.id).boundingBox();
    const existingBox = await umbracoAutomateUi.automate.canvasNode(trueId).boundingBox();
    expect(addedBox).not.toBeNull();
    expect(existingBox).not.toBeNull();
    const overlaps =
      addedBox!.x < existingBox!.x + existingBox!.width &&
      existingBox!.x < addedBox!.x + addedBox!.width &&
      addedBox!.y < existingBox!.y + existingBox!.height &&
      existingBox!.y < addedBox!.y + addedBox!.height;
    expect(overlaps).toBe(false);
  });

  test('inserts a step between an output and its existing target (#346)', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → If; If(true) → oldStep.
    const ifStep = automationStep(actions.if, 'branch', { conditions: { groups: [] } }, { x: 250, y: 200 });
    const oldStep = automationStep(actions.delay, 'oldStep', { duration: '00:00:01' }, { x: 150, y: 380 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Insert'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [ifStep, oldStep],
      connections: [automationConnection('trigger', ifStep), automationConnection(ifStep, oldStep, handles.ifTrue)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const ifId = umbracoAutomateApi.automations.stepByAlias(seeded, 'branch').id;
    const oldId = umbracoAutomateApi.automations.stepByAlias(seeded, 'oldStep').id;

    // Act — + on the already-connected true output.
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionFromNode(ifId, await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay), handles.ifTrue);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert — If(true) → new → old, the branch handle kept on the upstream half.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(3);
    const saved = await umbracoAutomateApi.automations.getById(id);
    const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);

    const fromTrue = connectionsFrom(saved, ifId, handles.ifTrue);
    expect(fromTrue.map((c: any) => c.targetStepId)).toEqual([added.id]);
    expect(fromTrue[0].outcome).toBe(handles.ifTrue);
    expect(connectionsFrom(saved, added.id).map((c: any) => c.targetStepId)).toEqual([oldId]);

    // Nothing orphaned: the old step is still wired in, and If no longer points at it directly.
    expect(saved.connections.filter((c: any) => c.targetStepId === oldId)).toHaveLength(1);
    expect(connectionsFrom(saved, ifId).some((c: any) => c.targetStepId === oldId)).toBe(false);
  });

  test('inserting a container step keeps the downstream step after it (#324)', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — trigger → first → second.
    const first = automationStep(actions.delay, 'first', { duration: '00:00:01' }, { x: 250, y: 200 });
    const second = automationStep(actions.delay, 'second', { duration: '00:00:01' }, { x: 250, y: 400 });
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Container'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger(),
      steps: [first, second],
      connections: [automationConnection('trigger', first), automationConnection(first, second)]
    });
    const seeded = await umbracoAutomateApi.automations.getById(id);
    const firstId = umbracoAutomateApi.automations.stepByAlias(seeded, 'first').id;
    const secondId = umbracoAutomateApi.automations.stepByAlias(seeded, 'second').id;

    // Act — + on first's connected output, choosing a While loop.
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionFromNode(firstId, await umbracoAutomateApi.catalogue.getStepTypeName(actions.while));
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();

    // Assert — first → While, and second continues from While's done handle, not inside its body.
    await expect.poll(async () => (await umbracoAutomateApi.automations.getById(id)).steps.length).toBe(3);
    const saved = await umbracoAutomateApi.automations.getById(id);
    const [added] = umbracoAutomateApi.automations.addedSteps(seeded, saved);
    expect(added.actionAlias).toBe(actions.while);
    expect(umbracoAutomateApi.automations.stepByAlias(saved, 'second').id).toBe(secondId);

    expect(connectionsFrom(saved, firstId).map((c: any) => c.targetStepId)).toEqual([added.id]);
    expect(connectionsFrom(saved, added.id, handles.done).map((c: any) => c.targetStepId)).toEqual([secondId]);
    expect(connectionsFrom(saved, added.id, handles.body)).toHaveLength(0);
  });

  test('marks an action whose connection the workspace does not allow as unavailable (#325)', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — the fixture workspace allows no connections, so Slack actions cannot be used.
    const slackAction = await umbracoAutomateApi.catalogue.getActionByAlias(actions.slackSendMessage);
    const slackTypeName = await umbracoAutomateApi.catalogue.getConnectionTypeName(slackAction.connectionTypeAlias);
    const delayName = await umbracoAutomateApi.catalogue.getStepTypeName(actions.delay);
    const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Unavailable'), automateServiceAccountWorkspace.id, {
      trigger: manualTrigger()
    });

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.addActionButton(AutomateUiHelper.triggerNodeId).click({ force: true });
    await umbracoAutomateUi.automate.searchPicker(slackAction.name);

    // Assert — still listed, disabled, and the description is replaced by a reason naming the
    // connection type it needs.
    const item = umbracoAutomateUi.automate.pickerItem(slackAction.name);
    await expect(item).toBeVisible();
    await expect(item).toHaveAttribute('disabled', '');
    const detail = await item.getAttribute('detail');
    expect(detail).not.toBe(slackAction.description);
    expect(detail).toContain(slackTypeName);
    await expect(item.getByRole('button').first()).toBeDisabled();

    // Choosing it does nothing: the picker stays open and no settings modal appears.
    await item.getByRole('button').first().click({ force: true });
    await expect(umbracoAutomateUi.automate.nodePickerModal).toBeVisible();
    await expect(umbracoAutomateUi.automate.nodeSettingsModal).toHaveCount(0);

    // An action that needs no connection is selectable as normal.
    await umbracoAutomateUi.automate.searchPicker(delayName);
    await expect(umbracoAutomateUi.automate.pickerItem(delayName)).not.toHaveAttribute('disabled', '');
  });

  test('offers the action once the workspace allows a connection of its type (#325)', async ({
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const slackAction = await umbracoAutomateApi.catalogue.getActionByAlias(actions.slackSendMessage);
    const connectionName = uniqueName('Canvas Slack Connection');
    const connectionId = await umbracoAutomateApi.connections.create(connectionName, slackAction.connectionTypeAlias);
    // Built by hand rather than from a fixture, because it needs an allowed connection.
    const serviceAccountKey = await umbracoAutomateApi.serviceAccounts.create('Canvas Service Account');
    const workspace = await umbracoAutomateApi.workspaces.createForTest('Canvas Connected Workspace', {
      serviceAccountKey,
      allowedConnections: [connectionId]
    });

    try {
      const id = await umbracoAutomateApi.automations.create(uniqueName('Canvas Available'), workspace.id, {
        trigger: manualTrigger()
      });

      // Act
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.addActionButton(AutomateUiHelper.triggerNodeId).click({ force: true });
      await umbracoAutomateUi.automate.searchPicker(slackAction.name);

      // Assert
      const item = umbracoAutomateUi.automate.pickerItem(slackAction.name);
      await expect(item).toBeVisible();
      await expect(item).not.toHaveAttribute('disabled', '');
    } finally {
      await umbracoAutomateApi.workspaces.cleanUp(workspace.id);
      await umbracoAutomateApi.serviceAccounts.deleteById(serviceAccountKey);
      await umbracoAutomateApi.connections.deleteById(connectionId);
    }
  });
});
