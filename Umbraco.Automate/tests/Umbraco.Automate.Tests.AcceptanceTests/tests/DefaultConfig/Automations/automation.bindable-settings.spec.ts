// STORY-1 to STORY-5: bindable settings, pick or bind (docs/plans/bindable-settings)
import { expect } from '@playwright/test';
import { test, ConstantHelper } from '../../../lib/index';

/**
 * Pending until bindable-settings T9. Every test is `test.fixme`, so the suite stays green.
 * T9 fills in each Arrange / Act / Assert with the suite's helpers. It adds a content-saved
 * trigger builder to AutomationBuilder and these aliases to ConstantHelper.actions:
 * publishContent, getContentProperty, moveContent, getMedia, moveMedia, createMedia.
 *
 * The step settings modal, the binding picker and the CMS tree/media pickers are reached through
 * AutomateUiHelper (openStepSettings, openBindingPicker, chooseBindingLeaf, nodeSettingsModal).
 * Add new locators there, such as the wrapper's switch (`ua-bindable-editor umb-input-toggle`).
 * Labels quoted in the comments below describe the expected UI. Per this suite's CLAUDE.md,
 * don't assert on them: target elements, roles, validity states and saved values instead.
 *
 * `test.fixme` here means "pending implementation", not a known product gap. T9 turns each one on.
 */
const { actions } = ConstantHelper;

test.describe('Field value kind', () => {
  test.fixme('reports contentKey on Publish Content as String', async ({ umbracoAutomateApi }) => {
    // Arrange / Act: read Publish Content from the catalogue.
    const action = await umbracoAutomateApi.catalogue.getActionByAlias('umbracoAutomate.publishContent');
    const field = action.settingsSchema.fields.find((f: any) => f.key === 'contentKey');

    // Assert
    expect(field.valueKind).toBe('String');
  });
});

test.describe('Pick or bind', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.fixme('shows the picker and the switch when bindings are in scope', async () => {
    // Arrange: content-saved trigger → Publish Content, Content Key empty.
    // Act: open the step settings.
    // Assert: the document picker's Choose button and the "Use a binding expression" switch are visible.
  });

  test.fixme('stores a picked node as its GUID', async () => {
    // Arrange: as above. Act: pick Home and save the step and automation.
    // Assert: the saved step's settings.contentKey equals Home's key.
  });

  test.fixme('Insert binding switches to binding mode with the expression', async () => {
    // Arrange: as above. Act: Insert binding → trigger → contentKey.
    // Assert: the switch is on and the expression box shows ${ trigger.contentKey }.
  });

  test.fixme('reopens a stored binding in binding mode', async () => {
    // Arrange: seed contentKey = '${ trigger.contentKey }' via the API. Act: open the step settings.
    // Assert: the switch is on and the box shows the expression.
  });

  test.fixme('reopens a stored GUID in picker mode showing the node', async () => {
    // Arrange: seed contentKey = Home's key. Act: open the step settings.
    // Assert: the switch is off and a node card named Home is shown.
  });

  test.describe('sad path', () => {
    test.fixme('shows no switch when nothing is in scope and the field is empty', async () => {
      // Arrange: manual trigger (no outputs) → Publish Content as the first step.
      // Assert: Choose is visible and the switch is not present.
    });

    test.fixme('shows the switch for a stored binding even with nothing in scope', async () => {
      // Arrange: as above, with contentKey seeded as a binding. Assert: the switch is on.
    });

    test.fixme('shows the required message for an empty binding', async () => {
      // Act: switch on and clear the box. Assert: "This field is required" is visible.
    });

    test.fixme('shows no switch on a read-only step', async () => {
      // Arrange: open a published automation as a user without update rights, or the run view's settings.
      // Assert: the switch is not present.
    });

    test.fixme('leaves a collection field unwrapped', async () => {
      // Arrange: a step with a bindable List<string> field and a non-text editor.
      // Assert: the editor renders with no switch.
    });

    test.fixme('leaves a non-string field unwrapped', async () => {
      // Arrange: a step with a bindable Guid or enum field and a non-text editor.
      // Assert: the editor renders with no switch.
    });

    test.fixme('keeps the switch while editing when nothing is in scope but a binding is stored', async () => {
      // Arrange: manual trigger → Publish Content with contentKey seeded as a binding.
      // Act: switch off, then on. Assert: the switch is still there and the expression is back.
    });

    test.fixme('leaves the key/value editor unwrapped', async () => {
      // Arrange: HTTP Request with body type Form. Assert: the Form fields editor has no switch.
    });

    test.fixme('falls back to binding mode when the declared editor is not registered', async () => {
      // Arrange: a field whose EditorUiAlias has no manifest.
      // Assert: binding mode with the stored value and no switch.
    });
  });
});

test.describe('Switching modes', () => {
  test.beforeEach(async ({ umbracoUi }) => {
    await umbracoUi.goToBackOffice();
  });

  test.fixme('restores the expression after switching off and on', async () => {
    // Act: insert ${ trigger.contentKey }, switch off, switch on. Assert: the box shows it again.
  });

  test.fixme('restores the picked node after binding and switching off', async () => {
    // Act: pick Home, switch on, insert a binding, switch off. Assert: the Home node card is shown.
  });

  test.fixme('carries a picked GUID into the expression box', async () => {
    // Act: pick Home, switch on. Assert: the box shows Home's key.
  });

  test.fixme('shows a GUID typed as text as a node in the picker', async () => {
    // Act: switch on, type Home's key, switch off. Assert: the Home node card is shown.
  });

  test.fixme('prefers the GUID in the box over the remembered pick', async () => {
    // Act: pick node A, switch on, paste node B's key, switch off. Assert: node B's card is shown.
  });

  test.fixme('Insert binding from picker mode remembers the pick', async () => {
    // Act: pick Home, Insert binding → contentKey, switch off. Assert: the Home node card is shown.
  });

  test.describe('sad path', () => {
    test.fixme('saves no binding when switched off', async () => {
      // Act: insert a binding, switch off, save. Assert: the saved contentKey has no "${".
    });

    test.fixme('drops text that is neither a binding nor a GUID on switch-off', async () => {
      // Act: switch on, type 'home', switch off. Assert: the picker is empty.
    });

    test.fixme('forgets remembered values when the panel is reopened', async () => {
      // Act: insert a binding, switch off, close, reopen, switch on. Assert: the box is empty.
    });
  });
});

test.describe('Content key fields', () => {
  test.fixme('publishes a picked node when the automation runs', async () => {
    // Arrange: manual trigger → Publish Content with Home picked. Act: publish and run.
    // Assert: the run completes and Home is published.
  });

  test.fixme('publishes the triggering node through a binding', async () => {
    // Arrange: content-saved trigger → Publish Content bound to ${ trigger.contentKey }.
    // Act: save a content node. Assert: that node is published.
  });

  test.fixme('Get Content Property reads the node bound from an earlier step', async () => {
    // Arrange: Get Content (Home picked) → Get Content Property bound to its key.
    // Act: run. Assert: the step output is Home's property value.
  });

  test.fixme('opens an automation saved with a plain GUID in picker mode', async () => {
    // Arrange: seed a GUID through the API, as automations saved before this change have.
    // Assert: picker mode showing the node.
  });

  test.fixme('Move Content shows the switch on both picker fields', async () => {
    // Arrange: content-saved trigger → Move Content. Assert: two switches.
  });

  test.describe('sad path', () => {
    test.fixme('fails the step when a parent binding resolves to nothing', async () => {
      // Arrange: manual trigger → Create Content with parentKey '${ trigger.missing }'. Act: run.
      // Assert: the run's Create Content step is Failed.
    });

    test.fixme('creates nothing at the root when a parent binding resolves to nothing', async () => {
      // Arrange / Act: as above. Assert: no new node exists at the content root.
    });

    test.fixme('still creates at the root when the parent is left empty', async () => {
      // Arrange: Create Content with parentKey empty. Act: run. Assert: the node is at the root.
    });
  });
});

test.describe('Media fields', () => {
  test.fixme('Move Media can select an image', async () => {
    // Act: open the Media field's picker, enter Sample Images. Assert: an image can be selected.
  });

  test.fixme('Move Media can select a folder', async () => {
    // Act: open the Media field's picker at the media root. Assert: a folder can be selected.
  });

  test.fixme('Get Media outputs the picked image', async () => {
    // Arrange: manual trigger → Get Media with an image picked. Act: run.
    // Assert: the step output's key is the image's key.
  });

  test.fixme('stores a picked media item as its GUID', async () => {
    // Act: pick an image and save. Assert: the saved mediaKey equals the image's key.
  });

  test.fixme('Create Media parent stays folders-only', async () => {
    // Act: open the Parent picker, enter Sample Images. Assert: images can't be selected.
  });

  test.describe('sad path', () => {
    test.fixme('Get Media Property cannot select a folder', async () => {
      // Act: open the Media field's picker at the media root. Assert: folders aren't selectable.
    });

    test.fixme('media key picker is read-only on a read-only step', async () => {
      // Assert: no Choose or remove controls.
    });
  });
});

// Referenced so the import isn't flagged unused while every test is pending.
void actions;
