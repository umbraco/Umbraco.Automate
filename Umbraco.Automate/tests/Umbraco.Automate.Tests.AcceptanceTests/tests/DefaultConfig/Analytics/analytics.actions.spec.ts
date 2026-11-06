// Story AN-5: docs/plans/analytics/STORIES.md
// Pending until T17. The builder seeds step runs by running automations with the named actions;
// the removed-package case needs a step run whose alias isn't in the catalogue.
import { expect } from '@playwright/test';
import { test } from '../../../lib/index';

test.describe('Feature: actions table', () => {
  test.describe('Scenario: an HTTP Request step has run', () => {
    test.skip('reads the action\'s name from the catalogue', async ({ automateWorkspace, umbracoAutomateApi, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      const name = await umbracoAutomateApi.catalogue.getStepTypeName('umbracoAutomate.httpRequest');
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionRow('umbracoAutomate.httpRequest')).toContainText(name);
    });
  });

  test.describe('Sad path', () => {
    test.skip('reads "None" for an action with no failures', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionRow('umbracoAutomate.httpRequest')).toHaveAttribute('data-top-error', 'none');
    });

    test.skip('reads "Other" for failures of category Unknown', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionRow('umbracoAutomate.runScript')).toHaveAttribute('data-top-error', 'other');
    });

    test.skip('shows the alias of an action that is no longer installed', async ({ automateWorkspace, page, umbracoAutomateUi }) => {
      expect(automateWorkspace.id).toBeTruthy();
      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.analyticsUrl());

      await expect(umbracoAutomateUi.automate.analytics.actionRow('acme.removedAction')).toContainText('acme.removedAction');
    });
  });
});
