import { expect } from '@playwright/test';
import { test, ConstantHelper, automationConnection, automationStep, manualTrigger, uniqueName } from '../../../lib/index';
import type { AutomationApiHelper } from '../../../lib/helpers/AutomationApiHelper';

/**
 * Send Email's address fields (To, Reply-To, CC, BCC), which edit their addresses as tags the
 * way an email client does but still store them as one comma-separated string.
 */
const { actions } = ConstantHelper;

test.describe('Email address input', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test('turns typed addresses into tags and saves them comma-separated', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id, mailId } = await seedSendEmail(umbracoAutomateApi.automations, automateServiceAccountWorkspace.id);

    // Act — a semicolon and a comma both end an address, but not inside a quoted display name.
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(mailId);
    await umbracoAutomateUi.automate.typeEmailAddresses('cc', 'team@example.com; boss@example.com,"Doe, John" <j@example.com>');
    await umbracoAutomateUi.automate.emailAddressInput('cc').press('Enter');

    // Assert — three tags, saved as the comma-separated list Send Email reads.
    await expect(umbracoAutomateUi.automate.emailAddressTags('cc')).toHaveText([
      'team@example.com',
      'boss@example.com',
      '"Doe, John" <j@example.com>'
    ]);
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();
    await expect
      .poll(async () => (await savedSettings(umbracoAutomateApi.automations, id)).cc)
      .toBe('team@example.com, boss@example.com, "Doe, John" <j@example.com>');
  });

  test('marks an entry that is not an address and puts the last tag back for editing on Backspace', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange
    const { id, mailId } = await seedSendEmail(umbracoAutomateApi.automations, automateServiceAccountWorkspace.id, {
      cc: 'team@example.com, archive'
    });
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(mailId);
    const tags = umbracoAutomateUi.automate.emailAddressTags('cc');
    await expect(tags).toHaveText(['team@example.com', 'archive']);
    await expect(tags.nth(1)).toHaveAttribute('color', 'danger');
    await expect(tags.nth(0)).toHaveAttribute('color', 'default');

    // Act — Backspace in the empty input reopens "archive", which is then completed.
    await umbracoAutomateUi.automate.emailAddressInput('cc').press('Backspace');
    await expect(umbracoAutomateUi.automate.emailAddressInput('cc')).toHaveValue('archive');
    await umbracoAutomateUi.automate.emailAddressInput('cc').press('End');
    await umbracoAutomateUi.automate.emailAddressInput('cc').pressSequentially('@example.com');
    await umbracoAutomateUi.automate.emailAddressInput('cc').press('Enter');

    // Assert
    await expect(tags).toHaveText(['team@example.com', 'archive@example.com']);
    await expect(tags.nth(1)).toHaveAttribute('color', 'default');
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();
    await expect
      .poll(async () => (await savedSettings(umbracoAutomateApi.automations, id)).cc)
      .toBe('team@example.com, archive@example.com');
  });

  test('adds a picked binding as a tag of its own', async ({
    automateServiceAccountWorkspace,
    umbracoAutomateUi,
    umbracoAutomateApi
  }) => {
    // Arrange — Set Variable before Send Email gives the picker something to offer.
    const { id, mailId } = await seedSendEmail(umbracoAutomateApi.automations, automateServiceAccountWorkspace.id, {}, true);

    // Act
    await umbracoAutomateUi.goToUrl(umbracoAutomateUi.automate.automationEditUrl(id));
    await umbracoAutomateUi.automate.waitForCanvas();
    await umbracoAutomateUi.automate.openStepSettings(mailId);
    await umbracoAutomateUi.automate.openEmailAddressBindingPicker('replyTo');
    await umbracoAutomateUi.automate.chooseBindingLeaf('upstream', 'value');

    // Assert — the binding is one tag, with an icon to tell it from an address, and is not
    // marked as invalid even though it is no address until the step runs.
    const tag = umbracoAutomateUi.automate.emailAddressTags('replyTo');
    await expect(tag).toHaveCount(1);
    await expect(tag).toHaveText(/^\s*\$\{\s*steps\.upstream\.value\s*\}\s*$/);
    await expect(tag.locator('uui-icon[name="icon-code"]')).toHaveCount(1);
    await expect(tag).toHaveAttribute('color', 'default');
    await umbracoAutomateUi.automate.submitNodeSettings();
    await umbracoAutomateUi.automate.clickSave();
    await expect
      .poll(async () => (await savedSettings(umbracoAutomateApi.automations, id)).replyTo)
      .toMatch(/^\$\{\s*steps\.upstream\.value\s*\}$/);
  });
});

/**
 * An automation with one Send Email step, aliased `mail`. With `withUpstream`, a Set Variable step
 * named `upstream` runs first, so its outputs are offered as bindings.
 */
async function seedSendEmail(
  api: AutomationApiHelper,
  workspaceId: string,
  settings: Record<string, unknown> = {},
  withUpstream = false
) {
  const mail = automationStep(
    actions.sendEmail,
    'mail',
    { to: 'owner@example.com', subject: 'Hello', body: '<p>Hello</p>', isHtml: true, ...settings },
    { x: 250, y: withUpstream ? 380 : 200 }
  );
  const upstream = automationStep(actions.setVariable, 'upstream', { name: 'n', value: 'v' }, { x: 250, y: 200 });
  const id = await api.create(uniqueName('Email Address Input'), workspaceId, {
    trigger: manualTrigger(),
    steps: withUpstream ? [upstream, mail] : [mail],
    connections: withUpstream
      ? [automationConnection('trigger', upstream), automationConnection(upstream, mail)]
      : [automationConnection('trigger', mail)]
  });
  const seeded = await api.getById(id);
  return { id, mailId: api.stepByAlias(seeded, 'mail').id };
}

async function savedSettings(api: AutomationApiHelper, id: string): Promise<Record<string, unknown>> {
  const saved = await api.getById(id);
  return api.stepByAlias(saved, 'mail').settings;
}
