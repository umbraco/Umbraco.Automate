import { expect } from '@playwright/test';
import { test, ConstantHelper } from '../../../lib/index';

test.describe('Workspace fixtures', () => {
  test('@smoke creates a tier-1 workspace with no service account', async ({
    automateWorkspace,
    umbracoAutomateApi
  }) => {
    // Assert
    expect(automateWorkspace.serviceAccountKey).toBe(ConstantHelper.emptyGuid);

    const created = await umbracoAutomateApi.workspaces.getByName(automateWorkspace.name);
    expect(created).not.toBeNull();
    expect(created.id).toBe(automateWorkspace.id);
    expect(created.alias).toBe(automateWorkspace.alias);
  });

  test('@smoke surfaces the Automate sidebar once a workspace exists', async ({
    automateWorkspace,
    umbracoUi,
    umbracoAutomateUi
  }) => {
    // The sidebar menu is gated behind the workspaces-exist condition, so this proves the
    // tier-1 fixture is enough to unblock UI specs.
    expect(automateWorkspace.id).toBeTruthy();

    // Act
    await umbracoUi.goToBackOffice();
    await umbracoAutomateUi.goToAutomateSection();
    await umbracoAutomateUi.automate.waitForDashboard();

    // Assert
    await expect(umbracoAutomateUi.automate.sectionSidebar).toBeVisible();
  });

  test('creates a tier-2 workspace backed by a real service account', async ({
    automateServiceAccountWorkspace,
    umbracoApi,
    umbracoAutomateApi
  }) => {
    // Assert
    expect(automateServiceAccountWorkspace.serviceAccountKey).not.toBe(ConstantHelper.emptyGuid);

    const created = await umbracoAutomateApi.workspaces.getByName(automateServiceAccountWorkspace.name);
    expect(created).not.toBeNull();
    expect(created.serviceAccountKey).toBe(automateServiceAccountWorkspace.serviceAccountKey);

    // The key must resolve to a real user, or automations in this workspace have no execution
    // identity — the exact failure tier 2 exists to avoid.
    const response = await umbracoApi.get(
      umbracoApi.baseUrl + '/umbraco/management/api/v1/user/' + automateServiceAccountWorkspace.serviceAccountKey
    );
    expect(response.status()).toBe(200);

    const serviceAccount = await response.json();
    expect(serviceAccount.kind).toBe('Api');
  });
});
