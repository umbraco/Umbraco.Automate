// Story AN-8: docs/plans/analytics/STORIES.md
// Pending until T11 (AC1, AC2, AC4), T18 (AC3, AC5) and T9 (AC6). The builder adds
// `analyticsUrl()` and `analytics` to AutomateUiHelper, mirroring the client's paths.
import { expect } from '@playwright/test';
import { test, ConstantHelper } from '../../../lib/index';

test.describe('Feature: open Analytics', () => {
  test.describe('Scenario: a site with a workspace', () => {
    test.skip('shows Analytics under a Monitoring heading in the sidebar', async ({ automateWorkspace, umbracoUi, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToAutomateSection();

      await expect(umbracoAutomateUi.automate.analytics.menuItem).toBeVisible();
    });

    test.skip('opens the Analytics page on its Overview view', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(page.locator('ua-analytics-overview-view')).toBeVisible();
    });
  });

  test.describe('Scenario: the Overview dashboard on a site with automations', () => {
    test.skip('links to Analytics under the status cards', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      await umbracoAutomateApi.automations.create('Analytics link check', automateWorkspace.id);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      await umbracoAutomateUi.automate.waitForDashboard();
      await umbracoAutomateUi.automate.analytics.overviewLink.click({ force: true });

      await expect(page.locator('ua-analytics-workspace')).toBeVisible();
    });
  });

  test.describe('Sad path', () => {
    test.skip('hides the Monitoring heading on a site with no workspaces', async ({ umbracoUi, umbracoAutomateUi }) => {
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToAutomateSection();

      await expect(umbracoAutomateUi.automate.analytics.monitoringMenu).toHaveCount(0);
    });

    test.skip('shows no Analytics link on the welcome screen', async ({ page, umbracoAutomateUi }) => {
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      await umbracoAutomateUi.automate.waitForDashboard();

      await expect(umbracoAutomateUi.automate.analytics.overviewLink).toHaveCount(0);
    });

    test.skip('answers 403 to a user without the Automate section', async ({ umbracoApi }) => {
      // The builder signs in as a user whose groups lack the Automate section, as the T9 wire task describes.
      const response = await umbracoApi.get(
        umbracoApi.baseUrl + ConstantHelper.api.basePath + 'metrics/analytics/summary?from=2026-10-01&to=2026-10-31&timeZone=UTC'
      );

      expect(response.status()).toBe(403);
    });
  });
});
