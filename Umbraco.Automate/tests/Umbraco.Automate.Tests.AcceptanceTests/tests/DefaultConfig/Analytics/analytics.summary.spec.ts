// Stories AN-2, AN-3: docs/plans/analytics/STORIES.md
// The summary answer is stubbed (`analytics.stubSummary`), so these don't depend on the runs a site holds.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: headline numbers and runs over time', () => {
  test.describe('Scenario: runs in the range and the comparison', () => {
    test('shows the change on the Runs tile', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary({ current: { runs: 110, completed: 110, successRate: 1 }, compare: { runs: 100, completed: 100, successRate: 1 } });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.tile('runs').locator('uaui-delta-badge')).toBeVisible();
    });

    test('opens the Approvals dashboard from the Waiting for approval tile', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.tile('waiting-for-approval').click({ force: true });

      await expect(page).toHaveURL(umbracoAutomateUi.automate.approvalDashboardUrl());
    });

    test('draws the comparison behind each day', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary({ current: { runs: 5, completed: 5, successRate: 1 }, compare: { runs: 4, completed: 4, successRate: 1 } });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect.poll(() => umbracoAutomateUi.automate.analytics.chart.evaluate((el: any) => el.compareSeries.length)).toBe(5);
    });
  });

  test.describe('Scenario: switching between Day, Week and Month', () => {
    test('Week puts each week in one bar, from the user\'s first day of the week', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickCustomRange({ daysAgoFrom: 13, daysAgoTo: 0 });
      await umbracoAutomateUi.automate.analytics.groupBy('week');

      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('groupBy', 'week');
    });

    test('Month puts each calendar month in one bar', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      await umbracoAutomateUi.automate.analytics.groupBy('month');

      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('groupBy', 'month');
    });

    test('sends no request to the server', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const loaded = page.waitForResponse((r: { url(): string }) => r.url().includes('/metrics/analytics/summary'));
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await loaded;
      let requests = 0;
      page.on('request', (r: { url(): string }) => r.url().includes('/metrics/analytics/') && requests++);
      await umbracoAutomateUi.automate.analytics.groupBy('week');
      await umbracoAutomateUi.automate.analytics.groupBy('month');
      await umbracoAutomateUi.automate.analytics.groupBy('day');
      await expect(umbracoAutomateUi.automate.analytics.chart).toHaveJSProperty('groupBy', 'day');
      // Let any late request land before counting.
      await page.waitForLoadState('networkidle');

      expect(requests).toBe(0);
    });
  });

  test.describe('Sad path', () => {
    test('shows a dash with no badge when no run has finished', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary({ current: { runs: 1, inProgress: 1 }, compare: { runs: 1, completed: 1, successRate: 1 } });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      const tile = umbracoAutomateUi.automate.analytics.tile('success-rate');

      await expect(tile).toHaveJSProperty('noData', true);
    });

    test('says "No runs in this period" when nothing ran', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary();
      const answered = page.waitForResponse((r: { url(): string }) => r.url().includes('/metrics/analytics/summary'));
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await answered;

      await expect(umbracoAutomateUi.automate.analytics.chartBox.locator('uaui-empty-state')).toBeVisible();
    });
  });
});
