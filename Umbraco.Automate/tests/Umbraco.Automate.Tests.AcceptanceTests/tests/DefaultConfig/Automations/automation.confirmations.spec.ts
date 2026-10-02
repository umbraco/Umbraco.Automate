import { expect, Locator } from '@playwright/test';
import {
  test,
  ConstantHelper,
  automationConnection,
  automationStep,
  manualTrigger,
  uniqueName
} from '../../../lib/index';

/**
 * Destructive removals in the automation editor, and when they ask first.
 *
 * Each list asks through the CMS confirm dialog (`umb-confirm-modal`, confirm label "Remove") only
 * when there is something to lose: a condition group that has conditions, a switch case that has a
 * name or conditions, and — always — a notification channel. For each, the dialog appears (or
 * does not), Cancel keeps the item, and confirming removes it.
 *
 * Nothing here is saved: the assertions are on the open editor, and the workspace fixture deletes
 * the automation afterwards.
 */
const { actions } = ConstantHelper;

/* A condition as the API stores it on a connection filter (ConditionSetModel). */
function condition(leftOperand: string) {
  return { leftOperand, operator: 'Equals', rightOperand: 'x' };
}

test.describe('Removal confirmations', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.describe('condition groups', () => {
    /* Seeds trigger → first → second with `groups` as the filter on first → second, opens the
     * automation and that edge's filter modal, and returns the modal's condition groups. */
    async function openEdgeFilter(
      umbracoAutomateUi: any,
      umbracoAutomateApi: any,
      workspaceId: string,
      groups: unknown[]
    ) {
      const first = automationStep(actions.delay, 'first', { duration: '00:00:01' }, { x: 250, y: 200 });
      const second = automationStep(actions.delay, 'second', { duration: '00:00:01' }, { x: 250, y: 450 });
      const id = await umbracoAutomateApi.automations.create(uniqueName('Confirm Groups'), workspaceId, {
        trigger: manualTrigger(),
        steps: [first, second],
        connections: [
          automationConnection('trigger', first),
          { ...automationConnection(first, second), filter: { groups } }
        ]
      });

      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.openActiveEdgeFilter();
      return umbracoAutomateUi.automate.edgeFilterModal.locator('ua-condition-builder .group');
    }

    function removeGroupButton(group: Locator): Locator {
      return group.locator('.group-header uui-button');
    }

    test('removes an empty group without asking', async ({
      automateWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — one group with a condition, plus a new empty one.
      const groups = await openEdgeFilter(umbracoAutomateUi, umbracoAutomateApi, automateWorkspace.id, [
        { conditions: [condition('a')] }
      ]);
      await expect(groups).toHaveCount(1);
      await umbracoAutomateUi.automate.clickInModal(
        umbracoAutomateUi.automate.edgeFilterModal.locator('ua-condition-builder .condition-builder > uui-button')
      );
      await expect(groups).toHaveCount(2);

      // Act
      await umbracoAutomateUi.automate.clickInModal(removeGroupButton(groups.nth(1)));

      // Assert — gone at once, the group with a condition kept, and no dialog.
      await expect(groups).toHaveCount(1);
      await expect(groups.first().locator('uui-ref-node')).toHaveCount(1);
      await expect(umbracoAutomateUi.automate.confirmModal).toHaveCount(0);
    });

    test('asks before removing a group that has conditions', async ({
      automateWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — two groups, each with a condition, so both show their remove button.
      const groups = await openEdgeFilter(umbracoAutomateUi, umbracoAutomateApi, automateWorkspace.id, [
        { conditions: [condition('a')] },
        { conditions: [condition('b')] }
      ]);
      await expect(groups).toHaveCount(2);

      // Act & Assert — Cancel keeps it.
      await umbracoAutomateUi.automate.clickInModal(removeGroupButton(groups.first()));
      await expect(umbracoAutomateUi.automate.confirmModal).toBeVisible();
      await umbracoAutomateUi.automate.cancelDialog();
      await expect(groups).toHaveCount(2);

      // Act & Assert — confirming removes it, leaving the other group.
      await umbracoAutomateUi.automate.clickInModal(removeGroupButton(groups.first()));
      await umbracoAutomateUi.automate.confirmDialog('Remove');
      await expect(groups).toHaveCount(1);
      await expect(groups.first().locator('uui-ref-node')).toHaveAttribute('detail', 'b');
    });
  });

  test.describe('switch cases', () => {
    /* Seeds trigger → Switch with no cases and opens its settings. Returns the case builder. */
    async function openSwitchSettings(umbracoAutomateUi: any, umbracoAutomateApi: any, workspaceId: string) {
      const switchStep = automationStep(actions.switch, 'router', { cases: [] }, { x: 250, y: 200 });
      const id = await umbracoAutomateApi.automations.create(uniqueName('Confirm Cases'), workspaceId, {
        trigger: manualTrigger(),
        steps: [switchStep],
        connections: [automationConnection('trigger', switchStep)]
      });
      const seeded = await umbracoAutomateApi.automations.getById(id);
      const switchId = umbracoAutomateApi.automations.stepByAlias(seeded, 'router').id;

      await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
      await umbracoAutomateUi.automate.waitForCanvas();
      await umbracoAutomateUi.automate.openStepSettings(switchId);
      const builder = umbracoAutomateUi.automate.nodeSettingsModal.locator('ua-switch-case-builder');
      await builder.waitFor({ state: 'visible' });
      return builder;
    }

    async function addCase(umbracoAutomateUi: any, builder: Locator) {
      await umbracoAutomateUi.automate.clickInModal(builder.locator('uui-button.add-case-btn'));
    }

    function removeCaseButton(caseBox: Locator): Locator {
      return caseBox.locator('.case-header-actions uui-button');
    }

    // A new case starts with one blank condition (createEmptyCase), which must not count as
    // something to lose.
    test('removes an untouched case without asking', async ({
      automateWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange
      const builder = await openSwitchSettings(umbracoAutomateUi, umbracoAutomateApi, automateWorkspace.id);
      const cases = builder.locator('uui-box');
      await addCase(umbracoAutomateUi, builder);
      await expect(cases).toHaveCount(1);

      // Act
      await umbracoAutomateUi.automate.clickInModal(removeCaseButton(cases.first()));

      // Assert
      await expect(cases).toHaveCount(0);
      await expect(umbracoAutomateUi.automate.confirmModal).toHaveCount(0);
    });

    test('asks before removing a named case', async ({
      automateWorkspace,
      umbracoAutomateUi,
      umbracoAutomateApi
    }) => {
      // Arrange — one case, given a name. The name commits on the input's change event.
      const builder = await openSwitchSettings(umbracoAutomateUi, umbracoAutomateApi, automateWorkspace.id);
      const cases = builder.locator('uui-box');
      await addCase(umbracoAutomateUi, builder);
      await expect(cases).toHaveCount(1);
      const nameInput = cases.first().locator('uui-input.case-name-input input');
      await nameInput.fill('high-priority');
      await nameInput.press('Tab');

      // Act & Assert — Cancel keeps it, name and all.
      await umbracoAutomateUi.automate.clickInModal(removeCaseButton(cases.first()));
      await expect(umbracoAutomateUi.automate.confirmModal).toBeVisible();
      await umbracoAutomateUi.automate.cancelDialog();
      await expect(cases).toHaveCount(1);
      await expect(nameInput).toHaveValue('high-priority');

      // Act & Assert — confirming removes it.
      await umbracoAutomateUi.automate.clickInModal(removeCaseButton(cases.first()));
      await umbracoAutomateUi.automate.confirmDialog('Remove');
      await expect(cases).toHaveCount(0);
    });
  });

  test('always asks before removing a notification channel', async ({
    page,
    automateWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — an automation with one Webhook channel, the built-in channel needing no provider.
    const id = await umbracoAutomateApi.automations.create(uniqueName('Confirm Channels'), automateWorkspace.id, {
      trigger: manualTrigger(),
      notificationSettings: {
        channels: [
          {
            channelAlias: 'umbracoAutomate.webhook',
            settings: { url: 'https://example.com/automate-acceptance' },
            isEnabled: true,
            notifyOn: 'Failed'
          }
        ]
      }
    });
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationNotificationsUrl(id));
    const channels = page.locator('ua-automation-notifications-workspace-view uui-ref-node');
    await expect(channels).toHaveCount(1);
    // The action bar holds Edit then Delete.
    const deleteButton = channels.first().locator('uui-action-bar uui-button').last();

    // Act & Assert — Cancel keeps it.
    await deleteButton.click({ force: true });
    await expect(umbracoAutomateUi.automate.confirmModal).toBeVisible();
    await umbracoAutomateUi.automate.cancelDialog();
    await expect(channels).toHaveCount(1);

    // Act & Assert — confirming removes it.
    await deleteButton.click({ force: true });
    await umbracoAutomateUi.automate.confirmDialog('Remove');
    await expect(channels).toHaveCount(0);
  });
});
