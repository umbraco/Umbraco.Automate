// Story AN-4: docs/plans/analytics/STORIES.md
// Pending until T16.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: automations table', () => {
  test.describe('Scenario: two automations have run', () => {
    test.skip('opens an automation from its name', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const automationId = await umbracoAutomateApi.automations.create('Sync new orders', automateWorkspace.id);
      await umbracoAutomateApi.automations.publishAndRunUntil(automationId, 'Completed');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.automationsTable.getByRole('link', { name: 'Sync new orders' }).click({ force: true });

      await expect(page).toHaveURL(umbracoAutomateUi.automate.automationEditUrl(automationId));
    });

    test.skip('shows the change in runs', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const automationId = await umbracoAutomateApi.automations.create('Sync new orders', automateWorkspace.id);
      await umbracoAutomateApi.automations.publishAndRunUntil(automationId, 'Completed');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-table-compare-badge').first()).toBeVisible();
    });

    test.skip('filters rows by the search box', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      for (const name of ['Sync new orders', 'Weekly report']) {
        await umbracoAutomateApi.automations.publishAndRunUntil(await umbracoAutomateApi.automations.create(name, automateWorkspace.id), 'Completed');
      }
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.automationsSearch.fill('orders');

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.getByRole('link')).toHaveText(['Sync new orders']);
    });
  });

  test.describe('Sad path', () => {
    test.skip('shows the empty state when nothing ran', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-empty-state')).toBeVisible();
    });
  });
});
