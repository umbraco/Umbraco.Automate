// Story AN-5: docs/plans/analytics/STORIES.md
// The actions answer is stubbed (`analytics.stubActions`), so these don't depend on the runs a site holds.
// Names come from the real catalogue; `acme.removedAction` is an alias it doesn't know.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: actions table', () => {
  test.describe('Scenario: an HTTP Request step has run', () => {
    test('reads the action\'s name from the catalogue', async ({ automateWorkspace, umbracoAutomateApi, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const name = await umbracoAutomateApi.catalogue.getStepTypeName('umbracoAutomate.httpRequest');
      await umbracoAutomateUi.automate.analytics.stubActions({ items: [{ actionAlias: 'umbracoAutomate.httpRequest', runs: 10 }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionNameCell('umbracoAutomate.httpRequest')).toContainText(name);
    });

    test('shows the change in failure rate', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const actionAlias = 'umbracoAutomate.httpRequest';
      await umbracoAutomateUi.automate.analytics.stubActions({
        items: [{ actionAlias, runs: 10, failureRate: 0.2 }],
        compareItems: [{ actionAlias, runs: 10, failureRate: 0.1 }]
      });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionRow(actionAlias).locator('uaui-percent-cell uaui-table-compare-badge')).toBeVisible();
    });
  });

  test.describe('Sad path', () => {
    test('reads "None" for an action with no failures', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubActions({ items: [{ actionAlias: 'umbracoAutomate.httpRequest', runs: 10, topErrorCategory: null }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionNameCell('umbracoAutomate.httpRequest')).toHaveAttribute('data-top-error', 'none');
    });

    test('reads "Other" for failures of category Unknown', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubActions({ items: [{ actionAlias: 'umbracoAutomate.runScript', runs: 10, failed: 2, topErrorCategory: 'Unknown' }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionNameCell('umbracoAutomate.runScript')).toHaveAttribute('data-top-error', 'other');
    });

    test('reads the category for any other failure', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubActions({ items: [{ actionAlias: 'umbracoAutomate.httpRequest', runs: 10, failed: 2, topErrorCategory: 'Authentication' }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionNameCell('umbracoAutomate.httpRequest')).toHaveAttribute('data-top-error', 'Authentication');
    });

    test('shows the alias of an action that is no longer installed', async ({ automateWorkspace, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.automate.analytics.stubActions({ items: [{ actionAlias: 'acme.removedAction', runs: 3 }] });
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionNameCell('acme.removedAction')).toContainText('acme.removedAction');
    });
  });
});
