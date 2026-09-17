import { expect } from '@playwright/test';
import { test, ConstantHelper, uniqueName } from '../../../lib/index';

/**
 * Automation create / rename / delete, driven through the backoffice and asserted through the
 * management API so a failure points at a field rather than at a screenshot.
 *
 * Uses the tier-1 `automateWorkspace` fixture: these flows never execute an automation, so they
 * do not need a service account.
 */
test.describe('Automation lifecycle', () => {
  test('@smoke creates an automation from the workspace', async ({
    automateWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const name = uniqueName('Acceptance Automation');

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(
      umbracoAutomateUi.automate.automationCreateUrl(ConstantHelper.entityTypes.workspace, automateWorkspace.id)
    );
    await umbracoAutomateUi.automate.waitForWorkspaceEditor();
    await umbracoAutomateUi.automate.enterName(name);
    await umbracoAutomateUi.automate.clickSave();

    // Assert
    await expect.poll(async () => await umbracoAutomateApi.automations.existsByName(name)).toBe(true);

    const created = await umbracoAutomateApi.automations.getFullByName(name);
    expect(created.name).toBe(name);
    expect(created.workspaceId).toBe(automateWorkspace.id);
  });

  test('renames an automation', async ({
    automateWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — seed via the API so the test only exercises the rename.
    const originalName = uniqueName('Acceptance Automation');
    const id = await umbracoAutomateApi.automations.create(originalName, automateWorkspace.id);
    const newName = uniqueName('Renamed Automation');

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForWorkspaceEditor();
    await umbracoAutomateUi.automate.enterName(newName);
    await umbracoAutomateUi.automate.clickSave();

    // Assert
    await expect.poll(async () => await umbracoAutomateApi.automations.existsByName(newName)).toBe(true);
    expect(await umbracoAutomateApi.automations.existsByName(originalName)).toBe(false);
  });

  test('deletes an automation', async ({
    automateWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const name = uniqueName('Acceptance Automation');
    const id = await umbracoAutomateApi.automations.create(name, automateWorkspace.id);

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForWorkspaceEditor();
    await umbracoAutomateUi.automate.clickAction('Delete');
    await umbracoAutomateUi.automate.confirmDialog();

    // Assert
    await expect.poll(async () => await umbracoAutomateApi.automations.existsByName(name)).toBe(false);
  });
});
