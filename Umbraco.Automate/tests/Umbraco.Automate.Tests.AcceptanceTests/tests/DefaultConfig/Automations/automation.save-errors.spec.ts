import { expect } from '@playwright/test';
import { test, ConstantHelper, uniqueName } from '../../../lib/index';

/**
 * A save that cannot succeed tells the editor why, instead of only marking the button failed.
 *
 * Server rejections are only shown because the detail data source opts into throwOnError: the SDK
 * client otherwise resolves a 4xx, and tryExecute stays silent.
 */
test.describe('Save errors', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('Save and publish without a name marks the name field and sends nothing', async ({
    automateWorkspace,
    umbracoAutomateUi
  }) => {
    // Arrange
    const automate = umbracoAutomateUi.automate;
    await umbracoAutomateUi.goToUrl(
      automate.automationCreateUrl(ConstantHelper.entityTypes.workspace, automateWorkspace.id)
    );
    await automate.waitForWorkspaceEditor();
    const creates = automate.trackAutomationCreates();

    // Act — Save and publish runs the same client-side validation as Save (#460).
    await automate.clickSaveAndPublish();

    // Assert — the empty name is flagged inline, and the server is never asked.
    await expect(automate.automationNameField).not.toHaveAttribute('pristine');
    expect(creates).toHaveLength(0);
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
