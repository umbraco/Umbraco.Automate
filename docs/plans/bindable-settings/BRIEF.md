# Brief

## Problem

**What.** A settings field is either *picked* with its own editor or *bound* with a `${ }`
expression, never both, unless its editor is a text box, text area or code editor.
`[Field(SupportsBindings = true)]` only takes effect for those three
(`settings-form.element.ts:185` `#resolveEditorAlias`). Any other editor, such as a content,
media or form picker, quietly ignores the flag. So the content and media key fields are plain
text boxes: a tree picker would lose `${ trigger.contentKey }`, which is how most automations
fill them.

**Where it came from.**

- **Issue #83** (Warren Buckley): "Having to get a GUID/Key to use in an action rather than use
  a content/entity picker is not great … in scenarios where I am not using a key from a
  previous step/action and would like to just pick a node."
- **PR #206** (`v18/feature/issue-83-content-key-picker`, open, **conflicting** with
  `v18/dev`): two editors with the switch built in, `Umb.Automate.ContentKeyPicker` and
  `Umb.Automate.MediaKeyPicker`, used on seven content/media key fields. Each has its own
  "Use a binding expression" switch. The mode comes from the value, so there's no migration.
- **PR #443** (`v18/feature/bindable-picker-settings`, **draft**, mergeable): the same switch,
  generalised as a wrapper around *any* editor (`ua-bindable-editor`). No core field uses it
  yet. Its open questions 1, 2, 5, 6 and 7 bear on this brief (see Riskiest unknowns).

**What people do today.**

- Authors copy a node's key from the content tree and paste it into a text box.
- Package authors (Umbraco Forms' form picker, for one) have to offer either a picker or
  bindings on each field.

**Who it's for.**

- *Automation authors* (backoffice users in the Automate section): pick a value, or bind it, on
  the same field.
- *Action developers* (core, Forms, third parties): mark a field `SupportsBindings = true` with
  any editor and get the switch.
- Not for: site visitors, or editors in the content section.

**Why now.** Two open PRs, #206 and #443, solve this in two overlapping ways. If both merge as
they are, the content/media fields show two switches. One approach has to win before either
merges.

**Success looks like.**

- #83 closes: content and media key fields show a tree picker and can still be bound.
- Any `SupportsBindings` field with a non-text editor offers the switch, with no code specific
  to that editor. No field ever shows two switches.
- Existing automations open in the mode they were saved in (a bound value opens bound, a GUID
  opens as a picked node), and they run and save unchanged with no migration.
- The switch only appears where something is in scope to bind to, or the field already holds a
  binding, so an author is never stuck in a mode they can't leave.

> ASSUMPTION: We can't measure adoption (there's no telemetry). Success means acceptance tests
> for the points above pass on the demo site, using the content/media key fields.

**Constraints.**

- Version lines: v18 and v17 are both active.

> ASSUMPTION: Build on `v18/dev`, then port to `v17/dev` using the Backport Workflow.

- No stored "mode" flag and no data migration. The stored value stays what the action already
  parses (for example a GUID string, or a `${ }` string).
- Changes to the public settings API (`EditableModel*`, `[Field]`) stay backwards compatible
  within a major. #206's editor aliases sit on `Constants.EditorUiAliases`, but they're
  unreleased, so they can still change.

**Riskiest unknowns.**

- **Editors we don't own.** A wrapper that works around any editor can't change third-party
  editors, Forms' form picker for one. It has to forward value, config, mandatory and readonly
  to them correctly as they are. #443 Q7 is part of this: a wrapped editor shows Insert binding
  instead of the property actions registered for its own alias.
- **Multi-value pickers.** A picker that stores an array (several documents) has no obvious
  single binding form. #443 stores `["${ expr }"]` and clears a multi-selection on switching
  (Q6).
- **Which editors handle bindings themselves.** The condition builder, switch-case builder and
  key/value editor already bind inside themselves and must not be wrapped. Today they're in a
  hard-coded list (#443 Q5).
- **Which core fields adopt it** (#443 Q1). The seven content/media key fields are the obvious
  first set. Others, such as Move Content's target, are open.

> ASSUMPTION: Slice 1 covers single-value fields. A multi-value picker either stays picker-only
> or takes one bound value. That's for `umb-design` to settle.

**Smallest shippable slice.**

> ASSUMPTION: The generic pick-or-bind switch, adopted by the seven content/media key fields
> from #206. That closes #83 and needs no server change.

**Replacing or extending.** This extends Automate's own settings form. It replaces #206's
per-editor switches. It isn't a new CMS extension point.

> ASSUMPTION: Of #206 and #443, only one approach survives. Which one is for `umb-design`.

**What would kill it.** Nothing realistic. Authors will always need both ways to fill a
content field: pick a node from the tree, or bind one from an earlier step (Find Content →
Get Content Property, for example). The open question is how it's built, not whether.

## Non-goals

- **Binding a whole list** ("use the list step X produced"). That's a separate, later plan. It
  needs server work that this slice doesn't.
- **A manifest flag that lets third-party editors say "I handle bindings myself"** (#443 Q5).
  The hard-coded list is enough until a package asks for it.
- **Changes to the binding expression language or the Insert binding modal.** Slice 1 reuses
  them as they are.
- **Binding sensitive fields' masked editor differently.** Sensitive text fields already swap to
  the binding text box (`settings-form.element.ts:205`). That stays as it is.
