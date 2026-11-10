// Stories AN-6, AN-7: docs/plans/analytics/STORIES.md
// AN-7 AC7 stubs the summary answer (`analytics.stubSummary`) so it doesn't depend on the site's run cleanup setting.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: date range, comparison and workspace', () => {
  test.describe('Scenario: the first visit in this browser', () => {
    test('shows the last 30 days compared with the 30 before', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await page.evaluate(() => localStorage.clear());
      await page.reload();

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-30-days');
    });

    test('offers Last 7 days, Last 30 days, Last 90 days and Custom', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset');

      expect(await umbracoAutomateUi.automate.analytics.presetOptionValues()).toEqual(['last-7-days', 'last-30-days', 'last-90-days', 'custom']);
    });
  });

  test.describe('Scenario: the user changes the range', () => {
    test('requests the summary, automations and actions again', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      // Let the first load of all three boxes answer, so only the range change is counted.
      const firstLoad = Promise.all(
        ['summary', 'automations', 'actions'].map((name) => page.waitForResponse((r: { url(): string }) => new URL(r.url()).pathname.endsWith(`/metrics/analytics/${name}`)))
      );
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await firstLoad;
      const requests: string[] = [];
      page.on('request', (r: { url(): string }) => r.url().includes('/metrics/analytics/') && requests.push(new URL(r.url()).pathname.split('/').pop()!));
      await umbracoAutomateUi.automate.analytics.pickPreset('last-7-days');

      await expect.poll(() => requests.sort()).toEqual(['actions', 'automations', 'summary']);
    });

    test('remembers the range next time', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-90-days');
    });

    test('remembers the workspace next time', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      // A second workspace, so the select is shown even on a site with only the fixture's.
      const second = await umbracoAutomateApi.workspaces.createForTest('Analytics remembered workspace');
      try {
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
        await umbracoAutomateUi.automate.analytics.workspaceSelect.selectOption(automateWorkspace.id);
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

        await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toHaveValue(automateWorkspace.id);
      } finally {
        await umbracoAutomateApi.workspaces.cleanUp(second.id);
      }
    });

    test('ignores a remembered range that ends after today', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      // Overwrite what was just remembered (under this user's key) with a range in the future.
      await page.evaluate(() => {
        const key = Object.keys(localStorage).find((k) => k.startsWith('Ua.Analytics.Filters'))!;
        const stored = JSON.parse(localStorage.getItem(key)!);
        stored.range = { from: '2999-01-01', to: '2999-01-31', preset: 'custom' };
        localStorage.setItem(key, JSON.stringify(stored));
      });
      await page.reload();

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-30-days');
    });

    test('shows no change badges with the comparison off', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary({ current: { runs: 110, completed: 110, successRate: 1 }, compare: { runs: 100, completed: 100, successRate: 1 } });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      // With the comparison on, the Runs tile shows its badge, so the count below can only reach 0 by the toggle.
      await expect(umbracoAutomateUi.automate.analytics.tile('runs').locator('uaui-delta-badge')).toBeVisible();
      await umbracoAutomateUi.automate.analytics.turnComparisonOff();

      await expect(page.locator('uaui-delta-badge')).toHaveCount(0);
    });

    test('says runs were deleted when the range starts before the oldest kept day', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubSummary({ current: { runs: 3, completed: 3, successRate: 1 }, retainedFromDaysAgo: 89 });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickCustomRange({ daysAgoFrom: 120, daysAgoTo: 0 });

      await expect(umbracoAutomateUi.automate.analytics.retentionNotice).toBeVisible();
    });
  });

  // The server refuses a range over 366 days and a comparison of another length, so the page must never send one.
  test.describe('Scenario: ranges the server would refuse', () => {
    const summaryUrl = (request: { url(): string }) => new URL(request.url()).pathname.endsWith('/metrics/analytics/summary');
    const days = (from: string, to: string) => (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1;

    test('shortens a custom range of 400 days to 366 days', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset');
      const request = page.waitForRequest((r: { url(): string }) => {
        const params = new URL(r.url()).searchParams;
        return summaryUrl(r) && days(params.get('from')!, params.get('to')!) > 30;
      });
      await umbracoAutomateUi.automate.analytics.pickCustomRange({ daysAgoFrom: 399, daysAgoTo: 0 });

      const params = new URL((await request).url()).searchParams;
      expect(days(params.get('from')!, params.get('to')!)).toBe(366);
    });

    test('asks for a year-back comparison as long as a range that crosses 29 February', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset');
      const request = page.waitForRequest((r: { url(): string }) => summaryUrl(r) && new URL(r.url()).searchParams.get('from') === '2024-02-15');
      await umbracoAutomateUi.automate.analytics.pickRangeWithComparison({ from: '2024-02-15', to: '2024-03-15', compareMode: 'previous-year' });

      const params = new URL((await request).url()).searchParams;
      expect(days(params.get('compareFrom')!, params.get('compareTo')!)).toBe(days(params.get('from')!, params.get('to')!));
    });

    test('ignores a remembered range over 366 days', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await umbracoAutomateUi.automate.analytics.pickPreset('last-90-days');
      await page.evaluate(() => {
        const key = Object.keys(localStorage).find((k) => k.startsWith('Ua.Analytics.Filters'))!;
        const stored = JSON.parse(localStorage.getItem(key)!);
        stored.range = { from: '2020-01-01', to: '2022-01-01', preset: 'custom' };
        stored.compare = null;
        localStorage.setItem(key, JSON.stringify(stored));
      });
      await page.reload();

      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset', 'last-30-days');
    });
  });

  test.describe('Scenario: a user with two workspaces', () => {
    test('offers All workspaces plus each of their workspaces', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      const second = await umbracoAutomateApi.workspaces.createForTest('Analytics second workspace');
      try {
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
        await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toBeVisible();
        // uui-select adds a hidden empty placeholder option of its own, which an editor can't see or pick.
        const values = await umbracoAutomateUi.automate.analytics.workspaceSelect.evaluate((el: HTMLSelectElement) =>
          [...el.options].filter((option) => !option.hidden).map((option) => option.value)
        );
        // The site may hold other workspaces, so keep "All workspaces" (empty value) and the two created here.
        const ids = [automateWorkspace.id, second.id];
        const offered = values.filter((value, index) => index === 0 || ids.includes(value));

        expect(offered.sort()).toEqual(['', ...ids].sort());
      } finally {
        await umbracoAutomateApi.workspaces.cleanUp(second.id);
      }
    });
  });

  test.describe('Sad path', () => {
    test('hides the workspace select for a user with one workspace', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      // The test user is an admin who sees every workspace on the site, so the list is cut down to the
      // fixture's workspace to stand in for a user with access to just one.
      await page.route(/\/automate\/management\/api\/v1\/workspaces\?(.*&)?take=100(&.*)?$/, async (route) => {
        const response = await route.fetch();
        const body = await response.json();
        const items = body.items.filter((item: { id: string }) => item.id === automateWorkspace.id);
        await route.fulfill({ response, json: { ...body, items, total: items.length } });
      });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
      await expect(umbracoAutomateUi.automate.analytics.dateRange).toHaveAttribute('data-preset');

      await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toBeHidden();
    });

    test('falls back to all workspaces when the remembered one is gone', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const gone = await umbracoAutomateApi.workspaces.createForTest('Analytics workspace to delete');
      try {
        await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());
        await umbracoAutomateUi.automate.analytics.workspaceSelect.selectOption(gone.id);
        await umbracoAutomateApi.workspaces.cleanUp(gone.id);
        await page.reload();

        await expect(umbracoAutomateUi.automate.analytics.workspaceSelect).toHaveValue('');
      } finally {
        // Already gone on a passing run; this removes it when the test fails before the delete above.
        await umbracoAutomateApi.workspaces.deleteById(gone.id);
      }
    });
  });
});
