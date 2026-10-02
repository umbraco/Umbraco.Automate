import { expect } from '@playwright/test';
import { test, ConstantHelper, uniqueName } from '../../../lib/index';

/**
 * Testing and authenticating a connection from its workspace.
 *
 * The demo site's only connection type is Slack, normally without a client id/secret, so no spec
 * here reaches Slack. #347 does not need it: what is under test is that Test connection saves
 * the form first, which the saved record shows whatever the test itself reports. #348 is
 * exercised by stubbing the two things a real popup-blocked sign-in depends on — the provider
 * status endpoint (so the editor believes Slack is configured) and `window.open` (returning null,
 * which is what a blocked popup looks like) — and by intercepting the challenge request, so the
 * browser never leaves the site.
 */
const SLACK = 'slack';

test.describe('Connection testing and authentication', () => {
  test('Test connection saves unsaved edits before testing (#347)', async ({
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const originalName = uniqueName('Acceptance Connection');
    const editedName = uniqueName('Edited Connection');
    const id = await umbracoAutomateApi.connections.create(originalName, SLACK);

    try {
      // Arrange — an edit that has not been saved.
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.connectionEditUrl(id));
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.enterName(editedName);

      // Act
      await umbracoAutomateUi.automate.clickWorkspaceAction(ConstantHelper.extensions.testConnectionWorkspaceAction);

      // Assert — the edit is persisted, although Save was never clicked.
      await expect.poll(async () => (await umbracoAutomateApi.connections.getById(id)).name).toBe(editedName);
    } finally {
      await umbracoAutomateApi.connections.deleteById(id);
    }
  });

  test('offers a same-tab fallback when the sign-in popup is blocked (#348)', async ({
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const name = uniqueName('Acceptance Connection');
    const id = await umbracoAutomateApi.connections.create(name, SLACK);

    try {
      // Arrange
      await umbracoAutomateUi.automate.stubOAuthProviderConfigured();
      await umbracoAutomateUi.automate.blockPopups();
      const challenge = umbracoAutomateUi.automate.interceptOAuthChallenge();

      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.connectionEditUrl(id));
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();

      // Act — the popup is blocked.
      await umbracoAutomateUi.automate.clickAuthenticate();

      // Assert — the fallback is offered rather than the page navigating on its own.
      await expect(umbracoAutomateUi.automate.oauthPopupBlockedWarning).toBeVisible();
      expect(new URL(umbracoAutomateUi.page.url()).pathname).toBe(umbracoAutomateUi.automate.connectionEditUrl(id));

      // Act — continue in this tab.
      await umbracoAutomateUi.automate.clickContinueInThisTab();

      // Assert — the challenge carries a local return URL back to this connection, and the nonce
      // this tab stored for the callback to be matched against.
      const request = new URL((await challenge).url());
      expect(request.pathname.toLowerCase()).toBe(`/umbraco/automate/oauth/challenge/${SLACK}`);
      expect(request.searchParams.get('returnUrl')).toBe(umbracoAutomateUi.automate.connectionEditUrl(id));
      const nonce = request.searchParams.get('nonce');
      expect(nonce).toBeTruthy();
      expect(await umbracoAutomateUi.automate.storedOAuthNonce(SLACK)).toBe(nonce);
    } finally {
      await umbracoAutomateApi.connections.deleteById(id);
    }
  });
});
