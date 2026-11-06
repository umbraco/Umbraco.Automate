// Stories AN-6, AN-7: docs/plans/analytics/STORIES.md
// Pending until T13 (AN-6 AC5, AC7; AN-7 AC1, AC2, AC4, AC13) and T15 (AN-7 AC3, AC5, AC7).
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: date range, comparison and workspace', () => {
  test.describe('Scenario: the first visit in this browser', () => {
    test.skip('shows the last 30 days compared with the 30 before', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await page.evaluate(() => localStorage.clear());
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-30-days');
    });

    test.skip('offers Last 7 days, Last 30 days, Last 90 days and Custom', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      const presetIds = await umbracoAutomateUi.automate.analytics.dateRange.evaluate((el: any) => el.presets.map((p: { id: string }) => p.id));

      expect(presetIds).toEqual(['last-7-days', 'last-30-days', 'last-90-days', 'custom']);
    });
  });

  test.describe('Scenario: the user changes the range', () => {
    test.skip('requests the summary, automations and actions again', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      const requests: string[] = [];
      page.on('request', (r: { url(): string }) => r.url().includes('/metrics/analytics/') && requests.push(new URL(r.url()).pathname.split('/').pop()!));
      await umbracoAutomateUi.automate.analytics.pickPreset('last-7-days');

      await expect.poll(() => requests.sort()).toEqual(['actions', 'automations', 'summary']);
    });

    test.skip('remembers the range and workspace next time', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      await umbracoAutomateUi.automate.analytics.workspaceSelect.selectOption({ label: automateWorkspace.name });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-90-days');
    });

    test.skip('shows no change badges with the comparison off', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.turnComparisonOff();

      await expect(page.locator('uaui-delta-badge')).toHaveCount(0);
    });

    test.skip('says runs were deleted when the range starts before the oldest kept day', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickCustomRange({ daysAgoFrom: 120, daysAgoTo: 0 });

      await expect(umbracoAutomateUi.automate.analytics.retentionNotice).toBeVisible();
    });
  });

  test.describe('Scenario: a user with two workspaces', () => {
    test.skip('offers All workspaces plus each of their workspaces', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const second = await umbracoAutomateApi.workspaces.create('Analytics second workspace');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.workspaceSelect.locator('option')).toHaveCount(3);
    });
  });

  test.describe('Sad path', () => {
    test.skip('hides the workspace select for a user with one workspace', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toBeHidden();
    });

    test.skip('falls back to all workspaces when the remembered one is gone', async ({ page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const gone = await umbracoAutomateApi.workspaces.create('Analytics workspace to delete');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.workspaceSelect.selectOption({ label: gone.name });
      await umbracoAutomateApi.workspaces.delete(gone.id);
      await page.reload();

      await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toHaveValue('');
    });
  });
});
