// Story AN-8: docs/plans/analytics/STORIES.md
import { expect } from '@playwright/test';
import { test, ConstantHelper } from '../../../lib/index';

test.describe('Feature: open Analytics', () => {
  test.describe('Scenario: a site with a workspace', () => {
    test('shows Analytics under a Monitoring heading in the sidebar', async ({ automateWorkspace, umbracoUi, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToAutomateSection();

      await expect(umbracoAutomateUi.automate.analytics.menuItem).toBeVisible();
    });

    test('opens the Analytics page on its Overview view', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(page.locator('ua-analytics-overview-view')).toBeVisible();
    });

    test('highlights the Analytics menu item on the Analytics page', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.activeMenuItem).toHaveCount(1);
    });
  });

  test.describe('Scenario: the Overview dashboard on a site with automations', () => {
    test('links to Analytics under the status cards', async ({ automateWorkspace, page, umbracoAutomateApi, umbracoAutomateUi }) => {
      await umbracoAutomateApi.automations.create('Analytics link check', automateWorkspace.id);
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      await umbracoAutomateUi.automate.waitForDashboard();
      await umbracoAutomateUi.automate.analytics.overviewLink.click({ force: true });

      await expect(page.locator('ua-analytics-workspace')).toBeVisible();
    });
  });

  test.describe('Sad path', () => {
    test('hides the Monitoring heading on a site with no workspaces', async ({ umbracoUi, umbracoAutomateUi }) => {
      await umbracoAutomateUi.automate.stubNoWorkspaces();
      const stubbed = umbracoAutomateUi.automate.waitForWorkspacesResponse();
      await umbracoUi.goToBackOffice();
      await umbracoAutomateUi.goToAutomateSection();
      // Waits, not assertions: the stubbed answer has arrived and the sidebar has rendered.
      await stubbed;
      await umbracoAutomateUi.automate.analytics.settingsHeading.waitFor();

      await expect(umbracoAutomateUi.automate.analytics.monitoringMenu).toHaveCount(0);
    });

    test('shows no Analytics link on the welcome screen', async ({ umbracoAutomateUi }) => {
      await umbracoAutomateUi.automate.stubNoAutomations();
      await umbracoAutomateUi.automate.stubNoWorkspaces();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      // Waits, not assertions: the welcome content has rendered.
      await umbracoAutomateUi.automate.waitForDashboard();
      await umbracoAutomateUi.automate.waitForDashboardWelcome();

      await expect(umbracoAutomateUi.automate.analytics.overviewLink).toHaveCount(0);
    });

    test('shows no Analytics link on the welcome screen of a workspace with no automations', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.stubNoAutomations();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.sectionUrl());
      // Waits, not assertions: the welcome content has rendered.
      await umbracoAutomateUi.automate.waitForDashboard();
      await umbracoAutomateUi.automate.waitForDashboardWelcome();

      await expect(umbracoAutomateUi.automate.analytics.overviewLink).toHaveCount(0);
    });

    test('answers 403 to a user without the Automate section', async ({ umbracoApi, umbracoAutomateApi }) => {
      const url = umbracoApi.baseUrl + ConstantHelper.api.basePath + 'metrics/analytics/summary?from=2026-10-01&to=2026-10-31&timeZone=UTC';

      const status = await umbracoAutomateApi.limitedUsers.getStatusAs('Editors', url);

      expect(status).toBe(403);
    });
  });
});
