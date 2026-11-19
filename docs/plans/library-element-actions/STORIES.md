# Stories: Library element actions

**10 stories, 125 criteria.** `PLAN.md` and the spec files use these story ids, so never renumber them.

**Access**

- [STORY-1](#story-1-element-steps-only-touch-what-the-service-account-could-touch-in-the-backoffice) Element steps only touch what the service account could touch in the backoffice, 14 criteria

**Read**

- [STORY-2](#story-2-read-a-library-element) Read a Library element, 10 criteria
- [STORY-3](#story-3-read-one-property-of-a-library-element) Read one property of a Library element, 8 criteria

**Write**

- [STORY-4](#story-4-change-one-property-of-a-library-element) Change one property of a Library element, 18 criteria
- [STORY-5](#story-5-publish-a-library-element) Publish a Library element, 11 criteria
- [STORY-6](#story-6-unpublish-a-library-element) Unpublish a Library element, 8 criteria
- [STORY-7](#story-7-create-a-library-element) Create a Library element, 17 criteria

**Backoffice**

- [STORY-8](#story-8-find-the-library-steps-and-pick-an-element) Find the Library steps and pick an element, 13 criteria

**Triggers**

- [STORY-9](#story-9-start-an-automation-when-a-library-element-changes) Start an automation when a Library element changes, 21 criteria
- [STORY-10](#story-10-find-and-set-up-the-element-triggers) Find and set up the element triggers, 5 criteria

<details>
<summary>Ready and Done</summary>

**Ready:** role, capability and value are all stated; Given/When/Then criteria cover the happy path; out of scope is explicit; passes INVEST.

**Done:** every criterion passes as an executable spec, sad paths included; `dotnet test Umbraco.Automate/Umbraco.Automate.slnx` and the frontend build pass; the acceptance specs pass against a v18 demo site; reviewed. No port: v18 only (BRIEF).

Same agreement as `bindable-settings`, minus the v17 port.

</details>

**Roles**

- **Automation author:** a backoffice user who builds automations on the canvas.
- **Site administrator:** sets up a workspace's service account and its user groups.
- **Action developer:** registers their own `IAutomationActionAuthorizer` decorator on a site.

**Specs**

- Unit: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Actions/BuiltIn/<Action>ActionTests.cs` (one per action), `Actions/BuiltIn/BuiltInActionOutcomeTests.cs`, and `Security/AutomationActionAuthorizerElementTests.cs`.
- Acceptance: `Umbraco.Automate/tests/Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/Outcomes/outcomes.built-in.spec.ts`, `outcomes.recycle-bin.spec.ts`, and `Automations/automation.library-elements.spec.ts`.

"A permission error" below means the step fails with the same error category Update Content Property uses when the authorizer refuses it.

---

## STORY-1: Element steps only touch what the service account could touch in the backoffice

As a **site administrator** who sets up a workspace's service account,
I want element steps to follow the account's Library start node and element permissions, folder permissions included,
so that an automation can't read or change an element the account couldn't in the backoffice.

Specs: `AutomationActionAuthorizerElementTests.cs`, `automation.library-elements.spec.ts` ("Folder permissions").

Out of scope: culture-level permissions (`AuthorizeCultureAccessAsync`); the content actions don't check them either.

**Happy path**

- **AC1: Element in reach.** Given a service account whose Library start node contains the element and whose group has `Umb.Element.Update`, when Update access is checked for that element, then the result is success.
- **AC2: Folder in reach.** Given a service account with `Umb.Element.Create` on folder "Reviews", when Create access is checked for "Reviews", then the result is success.
- **AC3: Library root.** Given a service account with root Library access and `Umb.Element.Create`, when Create access is checked with no folder, then the result is success.

**Sad path**

- **AC4: Permission missing.** Given a service account whose group lacks `Umb.Element.Publish`, when Publish access is checked for an element, then the result is a failure naming the missing permission.
- **AC5: Element outside the start node.** Given a service account whose Library start node is folder "Campaigns", when Update access is checked for an element in "Reviews", then the result is a failure.
- **AC6: Folder permission takes it away.** Given a group with `Umb.Element.Update` by default and a granular permission on folder "Reviews" without it, when Update access is checked for an element in "Reviews", then the result is a failure.
- **AC7: Folder outside the start node.** Given a service account whose Library start node is folder "Campaigns", when Create access is checked for folder "Reviews", then the result is a failure.
- **AC8: No root access.** Given a service account whose Library start node is a folder, when Create access is checked with no folder, then the result is a failure.
- **AC9: Element missing.** Given an element key that doesn't exist, when access is checked, then the result is not found, not a failure.
- **AC10: Trashed element, scoped account.** Given a service account whose Library start node is a folder, and an element from that folder now in the Library recycle bin, when access is checked for it, then the result is not found, not a failure.
- **AC11: Trashed folder, scoped account.** Given a service account whose Library start node is a folder, and a folder from it now in the Library recycle bin, when Create access is checked for that folder, then the result is not found, not a failure.
- **AC12: Old decorator, element check.** Given a site decorator built before this change that doesn't implement the element members, when element access is checked through it, then the result is a failure saying element authorization isn't supported.
- **AC13: Old decorator, folder check.** Given the same decorator, when folder access is checked through it, then the result is a failure saying element authorization isn't supported.
- **AC14: Real folder permission.** Given a workspace whose service account has no `Umb.Element.Create` on folder "Reviews", when an automation with a Create Element step targeting "Reviews" runs, then the run view shows the step failed with a permission error.

## STORY-2: Read a Library element

As an **automation author**,
I want a step that reads a published Library element's name and properties,
so that later steps can use the element's values.

Specs: `GetElementActionTests.cs`, `outcomes.built-in.spec.ts` ("Get Element").

Out of scope: URL, path, level and folder key in the output (SPEC).

**Happy path**

- **AC1: Found.** Given a published element "G2 review rating", when Get Element runs with its key, then the step takes the `success` outcome.
- **AC2: Output.** Given the same element, when Get Element runs, then the output holds `ElementKey`, `Name` "G2 review rating", `ContentTypeAlias`, `ContentTypeKey`, `Culture`, `AvailableCultures`, `CreateDate`, `UpdateDate`, `CreatorKey`, `WriterKey` and `Properties`.
- **AC3: Culture given.** Given an element published in en-US and da-DK, when Get Element runs with Culture "da-DK", then `Properties` holds the da-DK values.
- **AC4: Blank culture, variant element.** Given an element published in en-US and da-DK, when Get Element runs with Culture blank, then `Culture` is the element's first culture.
- **AC5: Blank culture, invariant element.** Given an invariant element, when Get Element runs with Culture blank, then `Properties` holds its invariant values.
- **AC6: No success line in the run log.** Given a published element, when Get Element runs, then the run log has no success line, as with Get Content. (Changed on #574: Rick dropped the success line.)

**Sad path**

- **AC7: Not a GUID.** Given Element "abc" after bindings resolve, when Get Element runs, then the step fails with a validation error.
- **AC8: Missing.** Given an element key that doesn't exist, when Get Element runs, then the step takes the `notFound` outcome.
- **AC9: Trashed.** Given a published element moved to the Library recycle bin, when Get Element runs for a service account with root Library access, then the step takes the `notFound` outcome.
- **AC10: Permission refused.** Given a service account without `Umb.Element.Read` on the element, when Get Element runs, then the step fails with a permission error.

## STORY-3: Read one property of a Library element

As an **automation author**,
I want a step that reads one property of a published Library element,
so that I can branch on or reuse one value without handling the whole element.

Specs: `GetElementPropertyActionTests.cs`, `outcomes.built-in.spec.ts` ("Get Element Property").

**Happy path**

- **AC1: Value present.** Given a published element whose `customSchema` holds text, when Get Element Property runs for `customSchema`, then the step takes the `success` outcome.
- **AC2: Output.** Given the same element, when Get Element Property runs, then the output holds `ElementKey`, `PropertyAlias` "customSchema", `Culture`, `Value` (the text) and `HasValue` true.
- **AC3: Value empty.** Given a published element whose `customSchema` is empty, when Get Element Property runs for it, then `HasValue` is false.
- **AC4: Culture given.** Given a culture-variant `customSchema` published in en-US and da-DK, when Get Element Property runs with Culture "da-DK", then `Value` is the da-DK value.

**Sad path**

- **AC5: Not a GUID.** Given Element "abc" after bindings resolve, when Get Element Property runs, then the step fails with a validation error.
- **AC6: Missing element.** Given an element key that doesn't exist, when Get Element Property runs, then the step takes the `notFound` outcome.
- **AC7: Trashed element.** Given a published element moved to the Library recycle bin, when Get Element Property runs, then the step takes the `notFound` outcome.
- **AC8: Missing property.** Given an element whose type has no `rating` property, when Get Element Property runs for `rating`, then the step takes the `propertyNotFound` outcome.

## STORY-4: Change one property of a Library element

As an **automation author** keeping a shared snippet up to date,
I want a step that writes one property of a Library element,
so that every page picking that element can show the new value without anyone editing it by hand.

Specs: `UpdateElementPropertyActionTests.cs`, `outcomes.built-in.spec.ts` ("Update Element Property"), `outcomes.recycle-bin.spec.ts`.

Out of scope: publishing. The write stays a draft until a Publish Element step (STORY-5).

**Happy path**

- **AC1: Value written.** Given element "G2 review rating" whose `customSchema` is "old", when Update Element Property runs with Value "new", then the element's saved `customSchema` is "new".
- **AC2: Outcome.** Given the same element, when the write succeeds, then the step takes the `success` outcome.
- **AC3: Previous value.** Given the same element, when the write succeeds, then the output's `PreviousValue` is "old".
- **AC4: Other properties untouched.** Given the element also has `ratingCount` "120", when Update Element Property writes `customSchema`, then `ratingCount` is still "120".
- **AC5: Published version untouched.** Given the element is published with `customSchema` "old", when Update Element Property writes "new", then the published element cache still returns "old".
- **AC6: Culture and segment.** Given a property that varies by culture and segment, when Update Element Property runs with Culture "da-DK" and Segment "members", then only the da-DK "members" value changes.
- **AC7: Blank culture, variant element.** Given a culture-variant property, when Update Element Property runs with Culture blank, then the value is written in the element's first culture.
- **AC8: Bound key.** Given Element bound to an earlier step's `ElementKey` output, when Update Element Property runs, then the value is written to that element.
- **AC9: Audit entry.** Given a successful write, when the audit log is read, then it has an entry for the element under the service account.
- **AC10: Run log.** Given a successful write, when the run log is read, then it has one line naming the element's name and key, the culture and the segment.

**Sad path**

- **AC11: Not a GUID.** Given Element "abc" after bindings resolve, when Update Element Property runs, then the step fails with a validation error.
- **AC12: Missing element, outcome.** Given an element key that doesn't exist, when Update Element Property runs, then the step takes the `notFound` outcome.
- **AC13: Missing element, nothing saved.** Given an element key that doesn't exist, when Update Element Property runs, then nothing is saved.
- **AC14: Trashed element, outcome.** Given an element in the Library recycle bin, when Update Element Property runs for a service account with root Library access, then the step takes the `notFound` outcome.
- **AC15: Trashed element, nothing saved.** Given the same trashed element, when Update Element Property runs, then nothing is saved.
- **AC16: Missing property, outcome.** Given an element whose type has no `rating` property, when Update Element Property runs for `rating`, then the step takes the `propertyNotFound` outcome.
- **AC17: Missing property, nothing saved.** Given the same element, when Update Element Property runs for `rating`, then nothing is saved.
- **AC18: Save cancelled.** Given a site notification handler that cancels the save, when Update Element Property runs, then the step fails with the Cancelled error category.

## STORY-5: Publish a Library element

As an **automation author**,
I want a step that publishes a Library element,
so that a value written by an earlier step reaches the pages that pick the element.

Specs: `PublishElementActionTests.cs`, `automation.library-elements.spec.ts` ("Publish reaches the page"), `outcomes.recycle-bin.spec.ts`.

Out of scope: scheduled publishing (the content action has none either).

**Happy path**

- **AC1: Invariant publish.** Given an invariant element whose draft `customSchema` is "new", when Publish Element runs with Cultures empty, then the published element cache returns "new".
- **AC2: Some cultures.** Given an element with drafts in en-US and da-DK, when Publish Element runs with Cultures "en-US,da-DK", then both cultures are published.
- **AC3: Output.** Given a successful publish, when the step's output is read, then it holds `ElementKey` and `Cultures`.
- **AC4: The page shows it.** Given a published page whose element picker picks "G2 review rating", and an automation that runs Update Element Property then Publish Element on it, when the automation runs, then the page's next render shows the new value.
- **AC5: Audit entry.** Given a successful publish, when the audit log is read, then it has an entry for the element under the service account.
- **AC6: Run log.** Given a successful publish, when the run log is read, then it has one line naming the element's name and key.

**Sad path**

- **AC7: Not a GUID.** Given Element "abc" after bindings resolve, when Publish Element runs, then the step fails with a validation error.
- **AC8: Missing element.** Given an element key that doesn't exist, when Publish Element runs, then the step fails.
- **AC9: Trashed element.** Given an element in the Library recycle bin, when Publish Element runs, then the step fails and the element stays unpublished.
- **AC10: CMS refuses.** Given an element with a mandatory property left empty, when Publish Element runs, then the step fails with a validation error carrying the CMS's reason.
- **AC11: Permission refused.** Given a service account without `Umb.Element.Publish` on the element, when Publish Element runs, then the step fails with a permission error.

## STORY-6: Unpublish a Library element

As an **automation author**,
I want a step that unpublishes a Library element,
so that an automation can withdraw a shared snippet from every page that picks it.

Specs: `UnpublishElementActionTests.cs`, `outcomes.recycle-bin.spec.ts`.

**Happy path**

- **AC1: All cultures.** Given a published element, when Unpublish Element runs with Cultures empty, then the published element cache no longer returns it.
- **AC2: One culture.** Given an element published in en-US and da-DK, when Unpublish Element runs with Cultures "da-DK", then en-US is still published.
- **AC3: Audit entry.** Given a successful unpublish, when the audit log is read, then it has an entry for the element under the service account.
- **AC4: Run log.** Given a successful unpublish, when the run log is read, then it has one line naming the element's name and key.

**Sad path**

- **AC5: Not a GUID.** Given Element "abc" after bindings resolve, when Unpublish Element runs, then the step fails with a validation error.
- **AC6: Missing element.** Given an element key that doesn't exist, when Unpublish Element runs, then the step fails.
- **AC7: Trashed element.** Given an element in the Library recycle bin, when Unpublish Element runs, then the step fails and the CMS's unpublish is never called.
- **AC8: Permission refused.** Given a service account without `Umb.Element.Unpublish` on the element, when Unpublish Element runs, then the step fails with a permission error.

## STORY-7: Create a Library element

As an **automation author**,
I want a step that creates a Library element of a chosen type in a chosen folder,
so that an automation can add a new shared snippet, for example one per product.

Specs: `CreateElementActionTests.cs`, `outcomes.built-in.spec.ts` ("Create Element"), `outcomes.recycle-bin.spec.ts`.

Out of scope: publishing the new element (a Publish Element step does that).

**Happy path**

- **AC1: In a folder.** Given folder "Reviews" and element type "Schema snippet", when Create Element runs with both and Name "Acme – AggregateRating", then an element of that type and name exists in "Reviews" and the step takes the `success` outcome.
- **AC2: Output.** Given the same run, when the step's output is read, then it holds `ElementKey`, `Name`, `ContentTypeKey`, `ContentTypeAlias` and `FolderKey` (the "Reviews" key).
- **AC3: At the root.** Given Folder empty, when Create Element runs for a service account with root Library access, then the element is created at the Library root and `FolderKey` is null.
- **AC4: Property values.** Given Property Values `{"customSchema": "{\"@type\":\"AggregateRating\"}"}`, when Create Element runs, then the new element's `customSchema` holds that text.
- **AC5: Culture.** Given a culture-variant element type, when Create Element runs with Culture "da-DK", then the name and the variant property values are set in da-DK.
- **AC6: Bound folder.** Given Folder bound to an earlier step's output that resolves to the "Reviews" key, when Create Element runs, then the element is created in "Reviews".
- **AC7: Audit entry.** Given a successful create, when the audit log is read, then it has an entry for the element under the service account.
- **AC8: Run log.** Given a successful create, when the run log is read, then it has one line naming the new element's name and key.

**Sad path**

- **AC9: Folder not a GUID.** Given Folder "abc" after bindings resolve, when Create Element runs, then the step fails with a validation error.
- **AC10: Folder missing.** Given a folder key that doesn't exist, when Create Element runs, then the step takes the `parentNotFound` outcome.
- **AC11: Folder trashed, root account.** Given folder "Reviews" in the Library recycle bin, when Create Element runs for a service account with root Library access, then the step takes the `parentNotFound` outcome.
- **AC12: Folder trashed, scoped account.** Given the same trashed folder, when Create Element runs for a service account whose Library start node is a folder, then the step takes the `parentNotFound` outcome.
- **AC13: Bound folder empty.** Given Folder bound to an expression that resolves to empty, when Create Element runs, then the step fails and nothing is created at the root.
- **AC14: Element type missing.** Given an element type key that doesn't exist, when Create Element runs, then the step takes the `elementTypeNotFound` outcome.
- **AC15: Not allowed in the Library.** Given an element type without "Allow in Library", when Create Element runs, then the step fails with a validation error naming the type.
- **AC16: Not allowed in the folder.** Given folder "Reviews" that only allows "Schema snippet", when Create Element runs with type "Promo banner", then the step fails with a validation error naming the type and the folder.
- **AC17: Invalid property values.** Given a value that would fail the `customSchema` property's validation, when Create Element runs, then the element is saved as a draft holding that value, as Create Content does.

## STORY-8: Find the Library steps and pick an element

As an **automation author**,
I want the element steps grouped under Library, with a picker for the element, folder and element type,
so that I can set them up the same way I set up content steps.

Specs: `automation.library-elements.spec.ts` ("Step picker", "Element picker", "Create Element settings", "Section access"), `outcomes.built-in.spec.ts` ("exits on the canvas"), `BuiltInActionOutcomeTests.cs`.

**Happy path**

- **AC1: Library group.** Given the step picker is open, when the author scrolls to Library, then it lists Get Element, Get Element Property, Create Element, Update Element Property, Publish Element and Unpublish Element, between Content and Media.
- **AC2: Search.** Given the step picker is open, when the author types "element", then the six element actions are listed.
- **AC3: Pick an element.** Given Update Element Property's settings are open, when the author picks "G2 review rating" in the element picker, then the step saves that element's key as one GUID string.
- **AC4: Reopen.** Given a saved step with a picked element, when the author reopens its settings, then the Element field shows "G2 review rating".
- **AC5: Bind an element.** Given Update Element Property's settings with a trigger in scope, when the author turns on the binding switch and enters an expression, then the step saves the expression.
- **AC6: Folders only.** Given Create Element's settings are open, when the author clicks Choose on Folder, then the picker lists Library folders and no elements.
- **AC7: Folder can be empty.** Given Create Element's settings with Folder empty and the other required fields set, when the author saves, then the step saves.
- **AC8: Element types only.** Given Create Element's settings are open, when the author clicks Choose on Element Type, then only element types can be picked. Page types are listed greyed out, as the CMS picker shows them.
- **AC9: Exits on the canvas.** Given a Create Element step on the canvas, when the canvas draws it, then it shows Created (default), Folder not found and Element type not found.
- **AC10: Section declared.** Given the six element actions, when their required sections are read, then each is the Library section.

**Sad path**

- **AC11: Element empty.** Given Update Element Property's settings with Element empty, when the author saves, then the Element field shows "This field is required".
- **AC12: Element type empty.** Given Create Element's settings with Element Type empty, when the author saves, then the Element Type field shows "This field is required".
- **AC13: No Library section.** Given a workspace whose service account lacks the Library section, when the author publishes an automation with an Update Element Property step, then publishing is refused with the "requires section access" error.

## STORY-9: Start an automation when a Library element changes

As an **automation author**,
I want an automation to start when an editor saves, publishes or unpublishes a Library element,
so that I can react to a shared snippet changing, as I can to content changing.

Specs: `Umbraco.Automate.Tests.Unit/Triggers/BuiltIn/Element*TriggerTests.cs`, `Dispatch/Authorization/NodeScopedTriggerDispatchAuthorizerTests.cs` (element cases), `automation.library-element-triggers.spec.ts` ("Runs").

Out of scope: Trashed and Deleted triggers; a folder filter; property values in the output (BRIEF).

**Happy path**

- **AC1: Saved starts a run.** Given a published automation with an Element Saved trigger, when an editor saves element "G2 review rating", then one run starts whose trigger output has that element's `elementKey`.
- **AC2: Output.** Given the same run, when its trigger output is read, then it holds `elementKey`, `elementName`, `contentTypeKey`, `contentTypeAlias`, `cultures` and `isNew`.
- **AC3: New element.** Given an Element Saved trigger, when an editor creates and saves a new element, then `isNew` is true.
- **AC4: Edited element.** Given an Element Saved trigger, when an editor saves an existing element again, then `isNew` is false.
- **AC5: Published.** Given a published automation with an Element Published trigger, when an editor publishes an element, then one run starts with that element's key.
- **AC6: Unpublished.** Given a published automation with an Element Unpublished trigger, when an editor unpublishes an element, then one run starts with that element's key.
- **AC7: Batch.** Given an Element Batch Saved trigger, when three elements are saved in one bulk save, then one run starts whose `items` hold the three elements.
- **AC8: Type filter matches.** Given Element Types set to "Schema Snippet", when an element of that type is saved, then the trigger fires.
- **AC9: Invariant cultures.** Given an invariant element, when it's published, then `cultures` is null, as Content Published reports it.
- **AC10: Variant cultures.** Given an element published in en-US only, when it's published, then `cultures` is `["en-US"]`.

**Sad path and edge cases**

- **AC11: Type filter doesn't match.** Given Element Types set to "Schema Snippet", when an element of another type is saved, then no run starts.
- **AC12: Batch with one matching type.** Given an Element Batch Saved trigger filtered to "Schema Snippet", when a snippet and an element of another type are saved together, then one run starts with both elements in `items`, as Content Batch Saved does.
- **AC13: Moved to another folder.** Given an Element Saved trigger, when an editor moves an element to another Library folder, then no run starts.
- **AC14: Sent to the recycle bin.** Given an Element Saved trigger, when an editor moves an element to the recycle bin, then no run starts.
- **AC15: Restored.** Given an Element Saved trigger, when an editor restores an element from the recycle bin, then no run starts.
- **AC16: Restore unpublishes.** Given an Element Unpublished trigger and a published element in the recycle bin, when an editor restores it, then one run starts for it.
- **AC17: No Read on the folder.** Given a workspace whose service account has a permission set on folder "Reviews" without Read, when an element in "Reviews" is saved, then no run starts in that workspace.
- **AC18: Outside the start node.** Given a service account whose Library start node is folder "Campaigns", when an element in "Reviews" is saved, then no run starts in that workspace.
- **AC19: Batch leaves out unreadable elements.** Given the account from AC17 and an Element Batch Saved trigger, when an element in "Reviews" and one in "Campaigns" are saved together, then the run's `items` hold only the "Campaigns" element.
- **AC20: Batch with nothing readable.** Given the same account, when only elements in "Reviews" are saved together, then no run starts.
- **AC21: No loop.** Given an automation with an Element Saved trigger whose step runs Update Element Property on the same element, and the default "Skip if this would loop", when an editor saves the element, then exactly one run starts.

## STORY-10: Find and set up the element triggers

As an **automation author**,
I want the element triggers in a Library group with an element type filter,
so that I set them up the way I set up the content triggers.

Specs: `automation.library-element-triggers.spec.ts` ("Trigger picker", "Trigger settings"), `Element*TriggerTests.cs` (settings attributes).

**Happy path**

- **AC1: Library group.** Given the trigger picker is open, when the author scrolls to Library, then it lists Element Saved, Element Published, Element Unpublished and Element Batch Saved.
- **AC2: Element types only.** Given an element trigger's settings are open, when the author clicks Choose on Element Types, then only element types can be picked.
- **AC3: Empty filter.** Given an element trigger's settings with Element Types empty, when the author saves, then the trigger saves.
- **AC4: Loop default.** Given a new element trigger, when its settings open, then "When triggered by another automation" is "Skip if this would loop".

**Sad path**

- **AC5: No Library section.** Given a workspace whose service account lacks the Library section, when the author publishes an automation with an Element Saved trigger, then publishing is refused with the "requires section access" error.
