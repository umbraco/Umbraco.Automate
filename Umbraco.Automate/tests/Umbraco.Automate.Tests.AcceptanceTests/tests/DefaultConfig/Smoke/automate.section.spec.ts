import { expect } from '@playwright/test';
import { test, ConstantHelper } from '../../../lib/index';

test.describe('Automate section', () => {
  test('@smoke loads the Automate section and renders the Overview dashboard', async ({
    umbracoUi,
    umbracoAutomateUi
  }) => {
    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToAutomateSection();

    // Assert
    // The dashboard element only exists if the Automate client bundle loaded and registered
    // its extensions, so this is the cheapest end-to-end proof the frontend is wired up.
    await umbracoAutomateUi.automate.waitForDashboard();
    await expect(umbracoAutomateUi.automate.dashboard).toBeVisible();
  });

  test('@smoke serves the Automate management API to an authenticated user', async ({
    umbracoApi,
    umbracoAutomateApi
  }) => {
    // Act
    const requestUrl = umbracoApi.baseUrl + ConstantHelper.api.basePath + 'workspaces';
    const response = await umbracoApi.get(requestUrl);

    // Assert
    expect(response.status()).toBe(200);
    const body = await response.json();
    expect(Array.isArray(body.items)).toBe(true);

    // The automations endpoint is reachable too — it returns an empty page on a fresh site,
    // which is a valid result, so assert on the shape rather than the count.
    const automations = await umbracoAutomateApi.automations.getAll();
    expect(Array.isArray(automations)).toBe(true);
  });
});
