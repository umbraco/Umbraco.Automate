import { expect } from '@playwright/test';
import { test, uniqueName } from '../../../lib/index';

/**
 * Workspace management through the backoffice, as opposed to the API path the fixtures use.
 *
 * The settings view marks Service Account Key and User Groups as required, so this is also the
 * cover for the difference between what the API accepts (an empty service account) and what the
 * UI asks for.
 */
test.describe('Workspace management', () => {
  test('@smoke lists workspaces in the collection view', async ({
    automateWorkspace,
    umbracoUi,
    umbracoAutomateUi
  }) => {
    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.workspaceRootUrl());

    // Assert — the table renders name and alias as separate columns.
    const row = umbracoAutomateUi.page.getByRole('row', { name: new RegExp(automateWorkspace.name) });
    await expect(row).toBeVisible();
    await expect(row).toContainText(automateWorkspace.alias);
  });

  /* Tier 2, not tier 1: the settings view requires both a service account and user groups, so a
   * tier-1 workspace cannot be saved from the UI at all. */
  test('renames a workspace', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const newName = uniqueName('Renamed Workspace');

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(
      umbracoAutomateUi.automate.workspaceEditUrl(automateServiceAccountWorkspace.id)
    );
    await umbracoAutomateUi.automate.waitForWorkspaceEditor();
    await umbracoAutomateUi.automate.enterName(newName);
    await umbracoAutomateUi.automate.clickSave();

    // Assert
    await expect.poll(async () => await umbracoAutomateApi.workspaces.existsByName(newName)).toBe(true);
    expect(await umbracoAutomateApi.workspaces.existsByName(automateServiceAccountWorkspace.name)).toBe(false);

    // The fixture tears down by id, so the rename does not orphan anything.
  });

  test('shows the service account on a workspace that has one', async ({
    automateServiceAccountWorkspace,
    umbracoUi,
    umbracoAutomateUi
  }) => {
    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToUrl(
      umbracoAutomateUi.automate.workspaceEditUrl(automateServiceAccountWorkspace.id)
    );
    await umbracoAutomateUi.automate.waitForWorkspaceEditor();

    // Assert — the settings view renders the resolved user, not the raw key.
    await expect(
      umbracoAutomateUi.page.getByText('Acceptance Test Service Account', { exact: false })
    ).toBeVisible();
  });

  test('deletes a workspace', async ({ umbracoUi, umbracoAutomateUi, umbracoAutomateApi }) => {
    // Arrange — created directly, not via the fixture, because the test removes it itself.
    const workspace = await umbracoAutomateApi.workspaces.createForTest('Deletable Workspace');

    try {
      // Act
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.workspaceEditUrl(workspace.id));
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.clickAction('Delete');
      await umbracoAutomateUi.automate.confirmDialog();

      // Assert
      await expect.poll(async () => await umbracoAutomateApi.workspaces.existsByName(workspace.name)).toBe(false);
    } finally {
      if (await umbracoAutomateApi.workspaces.existsByName(workspace.name)) {
        await umbracoAutomateApi.workspaces.cleanUp(workspace.id);
      }
    }
  });
});
