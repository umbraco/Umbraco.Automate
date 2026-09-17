import { expect } from '@playwright/test';
import { test, uniqueName } from '../../../lib/index';

/**
 * Connection create / rename / delete through the backoffice.
 *
 * Connection **types** come from installed provider packages, so this suite discovers what is
 * available rather than assuming. On the demo site that is Slack, normally unconfigured — which
 * is fine, because a connection record saves without credentials. Authenticating a connection
 * needs real OAuth and is out of scope here.
 */
const CONNECTION_TYPE = 'Slack';

test.describe('Connections', () => {
  test('creates a connection from the type picker', async ({
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const name = uniqueName('Acceptance Connection');

    try {
      // Act
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(
        umbracoAutomateUi.automate.connectionCreateUrl(CONNECTION_TYPE.toLowerCase())
      );
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.enterName(name);
      await umbracoAutomateUi.automate.clickSave();

      // Assert
      await expect.poll(async () => await umbracoAutomateApi.connections.existsByName(name)).toBe(true);

      const created = await umbracoAutomateApi.connections.getFullByName(name);
      expect(created.name).toBe(name);
      expect(created.type.toLowerCase()).toBe(CONNECTION_TYPE.toLowerCase());
    } finally {
      await umbracoAutomateApi.connections.ensureNameNotExists(name);
    }
  });

  test('renames a connection', async ({ umbracoUi, umbracoAutomateUi, umbracoAutomateApi }) => {
    const originalName = uniqueName('Acceptance Connection');
    const newName = uniqueName('Renamed Connection');

    try {
      // Arrange
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(
        umbracoAutomateUi.automate.connectionCreateUrl(CONNECTION_TYPE.toLowerCase())
      );
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.enterName(originalName);
      await umbracoAutomateUi.automate.clickSave();
      await expect.poll(async () => await umbracoAutomateApi.connections.existsByName(originalName)).toBe(true);

      const created = await umbracoAutomateApi.connections.getByName(originalName);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.connectionEditUrl(created.id));
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.enterName(newName);
      await umbracoAutomateUi.automate.clickSave();

      // Assert
      await expect.poll(async () => await umbracoAutomateApi.connections.existsByName(newName)).toBe(true);
      expect(await umbracoAutomateApi.connections.existsByName(originalName)).toBe(false);
    } finally {
      await umbracoAutomateApi.connections.ensureNameNotExists(originalName);
      await umbracoAutomateApi.connections.ensureNameNotExists(newName);
    }
  });

  test('lists a connection in the collection view', async ({
    umbracoUi,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    const name = uniqueName('Acceptance Connection');

    try {
      // Arrange
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToUrl(
        umbracoAutomateUi.automate.connectionCreateUrl(CONNECTION_TYPE.toLowerCase())
      );
      await umbracoAutomateUi.automate.waitForWorkspaceEditor();
      await umbracoAutomateUi.automate.enterName(name);
      await umbracoAutomateUi.automate.clickSave();
      await expect.poll(async () => await umbracoAutomateApi.connections.existsByName(name)).toBe(true);

      // Act
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.connectionRootUrl());

      // Assert
      await expect(umbracoAutomateUi.page.getByRole('link', { name, exact: true })).toBeVisible();
    } finally {
      await umbracoAutomateApi.connections.ensureNameNotExists(name);
    }
  });
});
