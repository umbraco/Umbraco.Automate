import { expect } from '@playwright/test';
import { test, ConstantHelper, uniqueName } from '../../../lib/index';

/**
 * A save the server rejects tells the editor why, instead of only marking the button failed.
 *
 * The notification only appears because the detail data source opts into throwOnError: the SDK
 * client otherwise resolves a 4xx, and tryExecute stays silent.
 */
test.describe('Save errors', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('Save and publish without a name says the server rejected it', async ({
    page,
    automateWorkspace,
    umbracoAutomateUi
  }) => {
    // Arrange
    const automate = umbracoAutomateUi.automate;
    await umbracoAutomateUi.goToUrl(
      automate.automationCreateUrl(ConstantHelper.entityTypes.workspace, automateWorkspace.id)
    );
    await automate.waitForWorkspaceEditor();

    // Act — Save and publish skips the client-side name check, so the server answers 400.
    await automate.clickSaveAndPublish();

    // Assert
    await expect(
      page.locator('uui-toast-notification').filter({ hasText: 'One or more validation errors occurred' })
    ).toBeVisible();
  });

  test('saving a copy someone else has changed says to reload', async ({
    page,
    automateWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — open the editor, then save the same automation elsewhere so this copy is stale.
    const automations = umbracoAutomateApi.automations;
    const automate = umbracoAutomateUi.automate;
    const id = await automations.create(uniqueName('Stale Save'), automateWorkspace.id);
    await umbracoAutomateUi.goToUrl(automate.automationEditUrl(id));
    await automate.waitForWorkspaceEditor();
    await automations.rename(id, uniqueName('Saved Elsewhere'));

    // Act
    await automate.enterName(uniqueName('Stale Edit'));
    await automate.clickSave();

    // Assert — the server's 409 detail reaches the editor.
    await expect(
      page.locator('uui-toast-notification').filter({ hasText: 'Reload and try again' })
    ).toBeVisible();
  });
});
