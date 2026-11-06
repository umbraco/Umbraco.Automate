// Stories AN-2, AN-3: docs/plans/analytics/STORIES.md
// Pending until T15. The builder adds `analytics` locators to AutomateUiHelper and seeds runs by
// publishing and running real automations (`publishAndRunUntil`), as the run-view specs do.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: headline numbers and runs over time', () => {
  test.describe('Scenario: runs in the range and the comparison', () => {
    test.skip('shows the change on the Runs tile', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const automationId = await umbracoAutomateApi.automations.create('Analytics tile check', automateWorkspace.id);
      await umbracoAutomateApi.automations.publishAndRunUntil(automationId, 'Completed');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.tile('runs').locator('uaui-delta-badge')).toBeVisible();
    });

    test.skip('opens the Approvals dashboard from the Waiting for approval tile', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.tile('waiting-for-approval').click({ force: true });

      await expect(page).toHaveURL(umbracoAutomateUi.automate.approvalDashboardUrl());
    });

    test.skip('draws the comparison behind each day', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('compareSeries.length', 5);
    });
  });

  test.describe('Scenario: switching between Day, Week and Month', () => {
    test.skip('Week puts each week in one bar, from the user\'s first day of the week', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickCustomRange({ daysAgoFrom: 13, daysAgoTo: 0 });
      await umbracoAutomateUi.automate.analytics.groupBy('week');

      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('groupBy', 'week');
    });

    test.skip('Month puts each calendar month in one bar', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      await umbracoAutomateUi.automate.analytics.groupBy('month');

      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('groupBy', 'month');
    });

    test.skip('sends no request to the server', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      let requests = 0;
      page.on('request', (r: { url(): string }) => r.url().includes('/metrics/analytics/') && requests++);
      await umbracoAutomateUi.automate.analytics.groupBy('week');
      await umbracoAutomateUi.automate.analytics.groupBy('month');
      await umbracoAutomateUi.automate.analytics.groupBy('day');

      expect(requests).toBe(0);
    });
  });

  test.describe('Sad path', () => {
    test.skip('shows a dash with no badge when no run has finished', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      await umbracoAutomateApi.automations.startWaitingRun(await umbracoAutomateApi.automations.create('Analytics waiting run', automateWorkspace.id));
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-7-days');

      await expect(umbracoAutomateUi.automate.analytics.tile('success-rate').locator('uaui-delta-badge')).toHaveCount(0);
    });

    test.skip('says "No runs in this period" when nothing ran', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.chartBox.locator('uaui-empty-state')).toBeVisible();
    });
  });
});
