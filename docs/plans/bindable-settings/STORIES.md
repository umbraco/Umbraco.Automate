# Stories

Derived from `SPEC.md`. Each story's acceptance criteria become the scenarios in the pending
spec files listed under it.

**Definition of Ready** (proposed, confirm): role, capability and value are all stated; Given/When/Then
criteria cover the happy path; out of scope is explicit; passes INVEST.

**Definition of Done** (proposed, confirm): every criterion passes as an executable spec; sad-path
criteria are covered; `dotnet test` and the frontend build pass; reviewed; ported to `v17/dev`
(STORY-6) before the feature counts as shipped.

> ASSUMPTION: Acceptance specs follow the existing Playwright suite's convention: one test per
> criterion, with Arrange / Act / Assert comments, and possibly several `expect`s on one
> observable outcome. That isn't strictly one assertion per test, but it matches
> `automation.pickers.spec.ts`. Unit specs keep to one assertion each.

---

## Epic: Pick or bind one value

### STORY-1: The server says whether a field holds a string, another value or a collection (S)

As an **action developer** declaring `[Field(SupportsBindings = true)]` on any property,
I want the settings schema to say whether the field holds a string, another single value or a collection,
so that the settings form never stores a binding in a shape my settings class can't read.

Specs: `Umbraco.Automate.Tests.Unit/Settings/EditableModelValueKindTests.cs`,
`automation.bindable-settings.spec.ts` ("Field value kind").

**Happy path**

- **AC1: String property.** Given a settings class with a `string` property, when its schema
  is built, then the field's value kind is `String`.
- **AC2: Nullable value type.** Given a `Guid?` property, when its schema is built, then the
  field's value kind is `Scalar`.
- **AC3: Enum property.** Given an enum property, when its schema is built, then the field's
  value kind is `Scalar`.
- **AC3b: Object property.** Given a `ConditionSet` property, when its schema is built, then the
  field's value kind is `Scalar`.
- **AC4: List of strings.** Given a `List<string>` property, when its schema is built, then
  the field's value kind is `Collection`.
- **AC5: Array.** Given a `string[]` property, when its schema is built, then the field's value
  kind is `Collection`.
- **AC6: List of rows.** Given a `List<HttpRequestKeyValue>` property, when its schema is
  built, then the field's value kind is `Collection`.
- **AC7: Real endpoint.** Given the Publish Content action, when the catalogue API returns its
  settings schema, then the `contentKey` field has `valueKind` `"string"`.

### STORY-2: Pick or bind any single-value picker field (M)

As an **automation author** editing a step whose field uses a picker,
I want to choose between picking a value and binding one on the same field,
so that I can pick a fixed node in one automation and bind a node from an earlier step in another.

Specs: `automation.bindable-settings.spec.ts` ("Pick or bind").

**Happy path**

- **AC1: Switch shown.** Given a step with a trigger in scope and a bindable string picker
  field, when the step settings open, then the field shows its picker with a "Use a binding
  expression" switch above it.
- **AC2: Picked value stored plain.** Given picker mode, when the author picks a node and
  saves, then the stored setting is the node's GUID.
- **AC3: Binding inserted.** Given picker mode, when the author uses Insert binding and picks
  `trigger.contentKey`, then the field is in binding mode showing `${ trigger.contentKey }`.
- **AC4: Reopens bound.** Given a stored `${ }` value, when the step settings open, then the
  field is in binding mode showing that expression.
- **AC5: Reopens picked.** Given a stored GUID, when the step settings open, then the field is
  in picker mode showing that node's name.

**Sad path / edges**

- **AC6: Nothing to bind to.** Given a step with no trigger outputs or earlier steps in scope,
  and an empty field, when the settings open, then no switch is shown.
- **AC7: Existing binding with nothing in scope.** Given no bindings in scope but a stored
  `${ }` value, when the settings open, then the switch is shown and the field is in binding
  mode.
- **AC8: Required in binding mode.** Given a required field in binding mode, when the
  expression box is emptied, then "This field is required" is shown.
- **AC9: Read-only.** Given the step is read-only, when the settings open, then no switch is
  shown.
- **AC10: Collection field unwrapped.** Given a bindable `Collection` field with a non-text
  editor, when the settings open, then its editor renders with no switch.
- **AC10b: Non-string field unwrapped.** Given a bindable `Guid` or enum field with a non-text
  editor, when the settings open, then its editor renders with no switch.
- **AC11: Self-binding editor unwrapped.** Given a bindable field using the key/value editor,
  when the settings open, then it renders with no switch.
- **AC12: Missing editor.** Given bindings in scope and a field whose declared editor isn't
  registered, when the settings open, then the field is in binding mode, shows the stored value
  and has no switch.
- **AC13: Routing holds while editing.** Given nothing in scope and a stored binding, when the
  author switches off and back on, then the switch is still shown and the expression is back.

### STORY-3: Switching modes doesn't lose work (S)

As an **automation author** flipping the binding switch,
I want my expression and my picked node to survive switching back and forth,
so that one accidental click doesn't throw work away.

Specs: `automation.bindable-settings.spec.ts` ("Switching modes").

**Happy path**

- **AC1: Expression restored.** Given binding mode with `${ trigger.contentKey }`, when the
  author switches off and back on, then the expression box shows `${ trigger.contentKey }`.
- **AC2: Picked node restored.** Given a picked node, when the author switches on, inserts a
  binding and switches off, then the picker shows the originally picked node.
- **AC3: GUID carried to text.** Given a picked node and no earlier expression, when the author
  switches on, then the expression box shows the node's GUID.
- **AC4: GUID carried to picker.** Given binding mode holding a GUID as text, when the author
  switches off, then the picker shows that node by name.
- **AC4b: The GUID in the box wins.** Given node A was picked, and the author switches on and
  pastes node B's GUID, when they switch off, then the picker shows node B.
- **AC4c: Insert binding remembers the pick.** Given a picked node, when the author uses Insert
  binding from picker mode and then switches off, then the picker shows the picked node.

**Sad path / edges**

- **AC5: Only the visible mode is saved.** Given an expression was entered and the author then
  switched off, when they save, then the stored setting holds no binding.
- **AC6: Memory ends with the panel.** Given an expression was entered and the author switched
  off, when they close and reopen the step settings and switch on, then the expression box is
  empty.

- **AC7: Other text is dropped.** Given binding mode holding text that is neither a binding nor
  a GUID, when the author switches off, then the picker is empty.

> ASSUMPTION: Still to capture in T9 and then decide: what the picker shows for a deleted or
> trashed node, and for an old upper-case or braced GUID. They become criteria here once
> decided.

---

## Epic: Core fields adopt it

### STORY-4: Content key fields pick from the tree or bind (M). Closes #83.

As an **automation author** using a content action,
I want to pick the content item from the tree, or bind it from the trigger or an earlier step,
so that I don't have to copy a GUID out of the content section.

Fields: Publish, Unpublish, Get Content, Get Content Property, Update Content Property and
Notify Editor `ContentKey`; Move Content `ContentKey` and `TargetParentKey`; Create Content
`ParentKey`.

Specs: `automation.bindable-settings.spec.ts` ("Content key fields").

**Happy path**

- **AC1: Picked node runs.** Given Publish Content with a picked node, when the automation
  runs, then that node is published.
- **AC2: Bound node runs.** Given Publish Content bound to `${ trigger.contentKey }` on a
  content trigger, when content is saved, then the triggering node is published.
- **AC3: Find → Get chain.** Given Get Content Property bound to the key from an earlier Get
  Content step, when the automation runs, then the step output is that node's property value.
- **AC4: Old automations open in picker mode.** Given an automation saved before this change
  with a GUID in Publish Content's Content Key, when the step opens, then it's in picker mode
  showing that node.
- **AC5: Move Content gains bindings.** Given Move Content with bindings in scope, when its
  settings open, then both picker fields show the switch.

**Sad path / edges**

- **AC6: A parent binding that resolves to nothing fails.** Given Create Content with
  `ParentKey` bound to a path that doesn't exist, when the automation runs, then the step fails.
- **AC7: Nothing created at the root.** Given the run in AC6, when it finishes, then no content
  was created at the root.
- **AC8: An empty parent still means root.** Given Create Content with `ParentKey` left empty,
  when the automation runs, then the content is created at the root.

### STORY-5: Media fields pick the right kind of media, or bind (M)

As an **automation author** using a media action,
I want to pick a media file (or a folder, where the action can work on one), or bind one,
so that media actions work like content ones, and I can't pick something the action will fail on.

Fields: Get Media, Get Media Property and Update Media Property `MediaKey` use
`Umb.Automate.MediaKeyPicker`, files only. Move Media `MediaKey` uses it with files and folders.
Create Media `ParentKey` and Move Media `TargetParentKey` keep the folder picker and gain bindings
(with `BindingMustResolve`).

Specs: `automation.bindable-settings.spec.ts` ("Media fields").

**Happy path**

- **AC1: File selectable.** Given Move Media's Media field, when the author opens the picker
  in a folder of images, then an image can be selected.
- **AC1b: Move Media can pick a folder.** Given Move Media's Media field, when the author opens
  the picker at the media root, then a folder can be selected.
- **AC2: Picked file runs.** Given Get Media with a picked image, when the automation runs,
  then the step output is that image.
- **AC3: Stored plain.** Given a picked media item, when the step is saved, then the stored
  setting is the item's GUID.
- **AC4: Parent stays folders-only.** Given Create Media's Parent field, when the author opens
  its picker, then images can't be selected.

**Sad path / edges**

- **AC5b: Folders not selectable for property actions.** Given Get Media Property's Media field,
  when the author opens the picker at the media root, then a folder can't be selected.
- **AC5: Read-only picker.** Given a read-only step, when the media key picker renders, then it
  can't be changed.

---

## Epic: Ship

### STORY-6: Port to v17 (S)

Placeholder. Port STORY-1 to STORY-5 to `v17/dev` with the Backport Workflow, after v18 merges.
Its criteria are the same specs, green on `v17/dev`.
