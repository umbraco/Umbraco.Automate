// Story AN-4: docs/plans/analytics/STORIES.md
// The automations answer is stubbed (`analytics.stubAutomations`), so these don't depend on the runs a site holds.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: automations table', () => {
  test.describe('Scenario: two automations have run', () => {
    test('opens an automation from its name', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const automationId = await umbracoAutomateApi.automations.create('Sync new orders', automateWorkspace.id);
      await umbracoAutomateUi.automate.analytics.stubAutomations({ items: [{ automationId, name: 'Sync new orders', workspaceName: 'Commerce', runs: 10, successRate: 1 }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.automationsTable.getByRole('link', { name: 'Sync new orders' }).click({ force: true });

      await expect(page).toHaveURL(umbracoAutomateUi.automate.automationEditUrl(automationId));
    });

    test('shows the change in runs', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const automationId = '11111111-1111-1111-1111-111111111111';
      await umbracoAutomateUi.automate.analytics.stubAutomations({
        items: [{ automationId, name: 'Sync new orders', runs: 110, successRate: 1 }],
        compareItems: [{ automationId, name: 'Sync new orders', runs: 100, successRate: 1 }]
      });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-share-cell uaui-table-compare-badge')).toBeVisible();
    });

    test.describe('Scenario: a run just after midnight UTC, viewed in New York', () => {
      test.use({ timezoneId: 'America/New_York' });

      test('puts the last run on the editor\'s local day', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
        expect(automateWorkspace.id).toBeTruthy();
        await umbracoAutomateUi.automate.analytics.stubAutomations({
          items: [{ automationId: '11111111-1111-1111-1111-111111111111', name: 'Sync new orders', runs: 1, successRate: 1, lastRunUtc: '2026-11-09T01:30:00Z' }]
        });
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

        await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('ua-analytics-date-time-cell')).toContainText('Nov 8, 2026');
      });
    });

    test('filters rows by the search box', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubAutomations({
        items: [
          { automationId: '11111111-1111-1111-1111-111111111111', name: 'Sync new orders', runs: 5, successRate: 1 },
          { automationId: '22222222-2222-2222-2222-222222222222', name: 'Weekly report', runs: 3, successRate: 1 }
        ]
      });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.automationsSearch.fill('orders');

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.getByRole('link')).toHaveText(['Sync new orders']);
    });
  });

  test.describe('Sad path', () => {
    test('shows the empty state when nothing ran', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubAutomations();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-empty-state')).toBeVisible();
    });

    test('shows the error state when the automations fail to load', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubAutomations({ failFirst: true });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-error-state')).toBeVisible();
    });

    test('lists the rows after Retry', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubAutomations({
        failFirst: true,
        items: [{ automationId: '11111111-1111-1111-1111-111111111111', name: 'Sync new orders', runs: 5, successRate: 1 }]
      });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.automationsTable.locator('uaui-error-state uui-button').click({ force: true });

      await expect(umbracoAutomateUi.automate.analytics.automationsTable.getByRole('link')).toHaveText(['Sync new orders']);
    });
  });
});
