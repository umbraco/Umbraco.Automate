### Prerequisites

- [x] Branch name follows the convention (`vN/feature/<anything>`, see [CONTRIBUTING.md](https://github.com/umbraco/Umbraco.Automate/blob/v18/dev/CONTRIBUTING.md#branch-naming-convention))
- [x] PR targets the correct `vN/dev` base branch
- [x] PR title follows [Conventional Commits](https://www.conventionalcommits.org/) (example: `fix(trigger): Resolve memory leak in event listener`)
- [x] I have added steps to test this contribution in the description below

Fixes #83. Supersedes #206 (closed).

Plan folder: [`docs/plans/bindable-settings/`](https://github.com/umbraco/Umbraco.Automate/tree/v18/feature/bindable-picker-settings/docs/plans/bindable-settings). It holds the brief, architecture, spec, stories, decision log and build log, plus screenshots.

### Description

#### Why the change

Settings fields could be picked with their own editor *or* bound with a `${ }` expression, never both, so content and media keys had to be pasted in as GUIDs. Now any single-value picker field offers both, starting with the content and media fields on the built-in actions.

#### Special things to note

- **Needs a decision:** the webhook trigger's settings dialog merges settings (`trigger-settings-modal.element.ts:36`), so the settings form reloads on every edit and re-decides which editor each field gets. This only matters for a third-party trigger field that uses the new switch; no built-in trigger has one. The behaviour predates this PR. Follow-up issue?
- **Needs a decision:** CMS's `UmbFormControlMixin` sets `#runValidatorsCallback = () => this._runValidators`, which returns the method instead of calling it, so an inner control's validity events never update its host. The wrapper works around it by re-checking validity after the inner editor's `updateComplete`. Worth an upstream Umbraco-CMS issue?
- **Field labels:** the six content key fields keep the label "Content Key" but are now node pickers. Move Content's field says "Content". This can be tidied up later if wanted.
- **Public API is additive only:** `EditableModelValueKind` and `EditableModelFieldDescriptor.ValueKind`; `[Field(BindingMustResolve = true)]`; `Constants.EditorUiAliases.MediaKeyPicker`. Settings properties keep their types, so stored automations need no migration.
- **Behaviour change:** Create Content / Create Media `ParentKey` and Move Content / Move Media `TargetParentKey` now accept bindings. A binding that resolves to nothing **fails the step** instead of using the root. A field that's really left empty still means the root.
- **Files only (deliberate):** Get Media, Get Media Property and Update Media Property only let you pick *files*. The actions also work on folders, and a folder can still be bound. Move Media allows both. Media parents stay folders-only.
- **GUIDs stored in another format** (upper case, braces) are converted for display only, so the picker finds the node. They're saved in canonical form once the author changes the field. The run accepts every format either way.
- **Not covered by tests:** the step settings form has no read-only mode, so the read-only paths in the wrapper and the media picker have no acceptance specs.
- **Local acceptance runs** used `--retries=2` because of the known SQLite cache-instruction stall. CI's LocalDB run is the real check.

#### Change outline

The settings schema now tells the client what shape each field holds. Only `String` fields can hold a `${ }` binding in place of their value.

```diff
 EditableModelFieldDescriptor
   Key, Label, Description, EditorUiAlias, EditorConfig, SupportsBindings, ...
+  ValueKind: String | Scalar | Collection    // "valueKind": "String" in the API
```

The settings form decides once, when a step's settings load, which editor each field gets. Editing never changes it.

```diff
 ua-settings-form #resolveEditorAlias(field)
   SupportsBindings + text box / text area / code editor / sensitive → binding text editor   (unchanged)
+  SupportsBindings + valueKind String + any other editor
+    (except condition builder, switch-case builder, key/value editor)
+    + (bindings in scope OR stored value is ${ })                     → ua-bindable-editor wrapping it
   everything else (incl. Scalar and Collection fields)                → declared editor   (unchanged)
```

`ua-bindable-editor` wraps the declared editor with a "Use a binding expression" switch. The mode comes from the stored value, so nothing extra is saved.

```diff
 <ua-bindable-editor>  (core/components/bindable-editor)
+  config: bindableEditorUiAlias, bindingSwitchAvailable   ← decided by the form
+  picker mode  → the declared editor, created from its manifest (value/config/mandatory/readonly forwarded)
+  binding mode → <ua-binding-text-box> + Insert binding property action
+  switch off   → remembers the expression; shows GUID in box > last pick > nothing
+  switch on    → restores the remembered expression, else carries a picked GUID as text
+  remembered values are never saved; only the visible mode's value is stored
```

The built-in fields that now use it:

```diff
 Publish, Unpublish, Get Content, Get Content Property, Update Content Property, Notify Editor
-  ContentKey: text box + bindings
+  ContentKey: Umb.PropertyEditorUi.DocumentPicker (min 1, max 1) + bindings
 Move Content: ContentKey, TargetParentKey / Create Content: ParentKey
-  DocumentPicker
+  DocumentPicker + bindings   (parents also BindingMustResolve)
 Get Media, Get Media Property, Update Media Property
-  MediaKey: text box + bindings
+  MediaKey: Umb.Automate.MediaKeyPicker (filesOnly) + bindings
 Move Media: MediaKey
-  Umb.PropertyEditorUi.MediaEntityPicker (folders only since CMS 17.3: files couldn't be picked)
+  Umb.Automate.MediaKeyPicker (filesAndFolders) + bindings
 Create Media: ParentKey / Move Media: TargetParentKey
-  MediaEntityPicker
+  MediaEntityPicker + bindings + BindingMustResolve
```

`Umb.Automate.MediaKeyPicker` is new: a thin wrapper around CMS's `umb-input-media` that stores a plain media key, with a `folderFilter` config. CMS has no editor that stores a single plain media item key.

A binding that must resolve is checked at run time, before the action runs:

```diff
 ActionStepBody: ResolveSettings → SettingsBindingResolver.ResolveBindings → action
+  [Field(BindingMustResolve = true)] string with ${ } that resolves to ""/whitespace
+    → SettingsBindingException (ConfigurationError, terminal) → step recorded as Failed
+      "Setting 'ParentKey' is bound to '${ trigger.missing }', which resolved to no value."
+      (value masked as ******** for IsSensitive fields)
```

#### How to test

1. Add a **Publish Content** step after a **Content Saved** trigger. Content Key shows CMS's document picker with **Use a binding expression** above it.
2. Pick a node, save and reopen. The node shows by name, and the stored value is its GUID.
3. Switch on. The GUID is carried over as text. Use **Insert binding** → `contentKey`, then switch off and on again. The expression comes back, and switching off brings the picked node back.
4. Save with the switch on: `${ trigger.contentKey }` is stored. Run it by saving a content node, and that node is published.
5. Add a **Move Media** step. The Media picker can select an image, which wasn't possible before. Add **Get Media Property**: folders aren't selectable there.
6. Add **Create Content** with Parent bound to `${ trigger.missing }`. The run fails that step with "resolved to no value", and nothing is created at the root.
7. A step with nothing to bind to, for example the first step after a Manual trigger, shows the picker with no switch.

Screenshots are in the plan folder's `SPEC.md`, including the edge cases (deleted, trashed, upper-case and braced GUIDs).

### Checks

- [x] `dotnet build` and `dotnet test` pass for the affected product(s): unit tests 2024/2024
- [x] Frontend builds (only if there are frontend changes): `tsc` and `vite build` are clean
- [x] Documentation updated (only if needed): plan folder, and the `SupportsBindings` / `BindingMustResolve` XML docs
- [ ] Database migrations added (only if the schema changed): no schema change

Acceptance: `automation.bindable-settings.spec.ts` covers the stories end to end. Locally, against the demo site, it had 0 failures with `--retries=2`. The neighbouring `binding-picker`, `pickers` and `canvas` specs passed.

### Other version lines

Umbraco.Automate maintains several version lines at once and they are never forward-merged, so each line needs its own PR. See the [backport workflow](https://github.com/umbraco/Umbraco.Automate/blob/v18/dev/CONTRIBUTING.md#development-workflow).

- [ ] This change only applies to the version line I am targeting
- [x] This change should be ported to another active line. Linked PR: to follow (`v17/dev`, plan task T11; CMS 17.6+ has everything it uses)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
