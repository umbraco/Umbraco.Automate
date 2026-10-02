# Stories

Source: [SPEC.md](./SPEC.md) and [ARCHITECTURE.md](./ARCHITECTURE.md). Each story becomes one
Feature in `bdd-specs`, and each AC becomes one Scenario.

## Definition of Ready / Done

> ASSUMPTION: Starting points for the team to confirm, not house rules.

- **Ready:** role, capability and value stated; Given/When/Then covers the happy path;
  out-of-scope stated; passes INVEST (Small and Testable especially).
- **Done:** every AC passes as an executable spec, sad paths included; `dotnet build` and the
  Umbraco.Automate tests are green; the OpenAPI client is regenerated if the API changed;
  the change is reviewed; v17 port is tracked (see PLAN.md).

## Roles

- **Action developer**: writes actions in a package (core, Slack, Umbraco.AI.Automate, third party).
- **Automation author**: backoffice user who builds automations on the canvas.

---

## Epic A: Actions can branch on their own outcomes (slice 1)

### S1: Declare fixed outcomes on an action

As an **action developer**,
I want to declare a fixed list of named outcomes on my action,
so that authors can branch on my action's result without adding an If or Switch.

**Happy path**

- **AC1: Declared outcomes are exposed**
  Given an action that overrides `GetOutcomes()` to return `yes`, `no` (default)
  When the step type's outcomes are read
  Then they are returned as `yes`, `no`, in declaration order
- **AC2: Default is flagged**
  Given the same action
  When its outcomes are read
  Then only `no` has `IsDefault = true`
- **AC3: Nothing declared means no outcomes**
  Given an action that doesn't override any outcome member
  When its outcomes are read
  Then the list is empty and `HasDynamicOutcomes` is false
- **AC4: Harness shows declared outcomes**
  Given an `ActionTestHarness` for the yes/no action
  When the test asks for the action's outcomes
  Then it gets `yes`, `no`

**Sad path**

- **AC5: Duplicate key rejected**
  Given a declaration with two outcomes keyed `yes`
  When it is validated by `StepOutcomeValidator`
  Then validation fails naming the duplicate key
- **AC6: Empty key rejected**
  Given a declaration with an outcome whose key is empty
  When it is validated
  Then validation fails naming the empty key
- **AC7: Reserved prefix rejected**
  Given a declaration with an outcome keyed `__any__`
  When it is validated
  Then validation fails saying keys can't start with `__`
- **AC8: No default rejected**
  Given a non-empty declaration with no default
  When it is validated
  Then validation fails saying exactly one default is required
- **AC9: Two defaults rejected**
  Given a declaration with two defaults
  When it is validated
  Then validation fails saying exactly one default is required

Out of scope: triggers and control flows declaring outcomes.

### S2: The run follows the exit the action picked

As an **automation author**,
I want the run to follow the exit that matches what the action returned,
so that each result leads to the steps I connected to it.

**Happy path**

- **AC1: Named outcome is followed**
  Given a published automation where the yes/no step's `yes` exit leads to step A and `no` leads to step B
  When the action returns outcome `yes`
  Then step A runs
- **AC2: The other exit is not followed**
  Given the same automation
  When the action returns outcome `yes`
  Then step B does not run
- **AC3: No outcome uses the default**
  Given the same automation
  When the action returns success with no outcome
  Then step B (the `no` default exit) runs
- **AC4: Branch is recorded**
  Given the same automation
  When the action returns outcome `yes`
  Then the step run's `BranchOutcome` is `yes`
- **AC5: Default branch is recorded**
  Given the same automation
  When the action returns success with no outcome
  Then the step run's `BranchOutcome` is `no`
- **AC6: Actions without outcomes unchanged**
  Given an action that declares no outcomes, connected by an unnamed line to step C
  When it returns success
  Then step C runs

**Sad path**

- **AC7: Undeclared outcome is warned**
  Given the yes/no step
  When the action returns outcome `maybe`
  Then the step run log has a warning `Action returned outcome 'maybe', which it does not declare.`
- **AC8: Undeclared outcome still routes**
  Given the yes/no step with an unnamed line to step C
  When the action returns outcome `maybe`
  Then step C runs and steps A and B don't
- **AC9: Broken declaration fails the step**
  Given an action that declares two defaults
  When it runs
  Then the step fails with an error naming the action and the broken rule
- **AC10: Broken declaration is not retried**
  Given the same action with error behaviour Retry
  When it runs
  Then the step is not retried
- **AC11: Failure is not routed**
  Given the yes/no step
  When the action fails
  Then neither A nor B runs, and the configured error behaviour applies

### S3: See and connect one exit per outcome

As an **automation author**,
I want a step to show one labelled exit per outcome,
so that I can connect each result to what should happen next.

**Happy path**

- **AC1: Catalogue lists outcomes**
  Given the yes/no action is registered
  When the actions catalogue is requested
  Then its item has `outcomes` `[yes, no(default)]` and `hasDynamicOutcomes: false`
- **AC2: Catalogue unchanged for others**
  Given an action that declares nothing
  When the catalogue is requested
  Then its item has `outcomes: []` and `hasDynamicOutcomes: false`
- **AC3: Node draws one exit per outcome**
  Given a yes/no step on the canvas
  When the canvas renders
  Then the node shows exits "yes" and "no (default)", each with a "+" button
- **AC4: Plain action node unchanged**
  Given a step whose action declares nothing
  When the canvas renders
  Then the node shows a single unnamed exit
- **AC5: Connecting saves the outcome**
  Given a yes/no step
  When the author draws a line from the "yes" exit and saves
  Then the saved connection has `outcome: "yes"` and `sourceHandle: "yes"`
- **AC6: Insert-between uses the default**
  Given a line leaving a yes/no step
  When the author inserts a step onto that line from the yes/no step's continuation
  Then the yes/no step continues through its default (`no`) exit
- **AC7: Run view hides add buttons**
  Given a completed run of the automation
  When the run canvas renders
  Then the yes/no node's exits show no "+" buttons

### S4: Outcomes driven by step settings

As an **action developer**,
I want my action's outcomes to come from the step's settings,
so that, for example, each option an author types becomes its own exit.

**Happy path**

- **AC1: Outcomes follow settings**
  Given a dynamic-outcome test action whose settings list options `a`, `b`
  When `GetOutcomesAsync` runs with those settings
  Then it returns `a`, `b` plus its default `other`
- **AC2: Resolve endpoint returns them**
  Given the same action
  When `POST /step-types/{alias}/outcomes` is called with options `a`, `b`
  Then the response is `[a, b, other(default)]`
- **AC3: Endpoint works for static actions**
  Given the yes/no action
  When the endpoint is called with any settings
  Then the response is `[yes, no(default)]`
- **AC4: Canvas resolves on load**
  Given a saved dynamic step with options `a`, `b`
  When the canvas loads
  Then the node shows exits `a`, `b`, `other (default)`
- **AC5: Exits update on settings save**
  Given that step on the canvas
  When the author adds option `c` in the settings modal and submits
  Then the node shows exits `a`, `b`, `c`, `other (default)`
- **AC6: Existing lines stay attached**
  Given a line from exit `a`
  When the author adds option `c` and submits
  Then the line still leaves exit `a`
- **AC7: Bound values add no exits**
  Given the dynamic action whose options setting is `${ steps.x.output.options }`
  When its outcomes are resolved
  Then only `other` is returned
- **AC8: Run time uses saved settings**
  Given that bound step published with only an `other` exit
  When it runs and the binding resolves to `a`, `b`
  Then the run's resolved outcome list is still only `other`
- **AC9: Binding helper**
  Given the string `"${ steps.x.output }"`
  When `ContainsBinding()` is called on it
  Then it returns true

**Sad path**

- **AC10: Unknown alias**
  Given no step type with alias `nope`
  When `POST /step-types/nope/outcomes` is called
  Then the response is 404 "Step type not found"
- **AC11: Invalid settings**
  Given settings the action's settings type rejects
  When the outcomes endpoint is called
  Then the response is 400 "Invalid settings" with the resolver's message
- **AC12: Resolve failure keeps exits**
  Given the outcomes request fails after a settings save
  When the canvas handles the failure
  Then the node keeps its previous exits and an error notification is shown
- **AC13: Plain helper input**
  Given the string `"plain text"`
  When `ContainsBinding()` is called on it
  Then it returns false

### S5: Stale lines are caught before publish

As an **automation author**,
I want to see when a line points at an outcome that no longer exists,
so that I don't publish an automation with a path that can never run.

**Happy path**

- **AC1: Missing exit is shown**
  Given a line from exit `b`, and the author removes option `b` and submits settings
  When the canvas re-renders
  Then the node shows a "Missing outcome: b" exit in error styling with the line attached
- **AC2: Line is never dropped silently**
  Given the same change
  When the author saves the draft
  Then the connection from `b` is still in the saved automation
- **AC3: Draft save allowed**
  Given an automation with a stale `b` line
  When the author saves it as a draft
  Then the save succeeds
- **AC4: Fixing it allows publish**
  Given the stale line is moved to exit `a`
  When the author publishes
  Then publish succeeds

**Sad path**

- **AC5: Publish blocked**
  Given an automation with a stale `b` line on step "Decide"
  When the author publishes
  Then publish fails with `Step 'Decide' has a connection from outcome 'b', which the step no longer has. Reconnect or remove it.`
- **AC6: One error per stale line**
  Given two stale lines
  When the author publishes
  Then the error list has two entries
- **AC7: If/Switch/Approval not checked**
  Given a Switch step with a line from a case outcome
  When the author publishes
  Then no stale-outcome error is raised for it
- **AC8: Unnamed lines not checked**
  Given an unnamed line from a step that declares outcomes
  When the author publishes
  Then no stale-outcome error is raised for it

### S6: Old automations keep working ("Any result")

As an **automation author** with automations saved before an action declared outcomes,
I want my existing line to keep running whatever the step returns,
so that upgrading doesn't change what my automations do.

**Happy path**

- **AC1: Any result exit shown**
  Given a saved unnamed line from a step whose action now declares outcomes
  When the canvas renders
  Then the node shows an "Any result" exit, after the declared ones, with the line attached
- **AC2: No Any result without a line**
  Given the same step with no unnamed line
  When the canvas renders
  Then no "Any result" exit is shown
- **AC3: No new Any result lines**
  Given the "Any result" exit
  When the canvas renders
  Then it has no "+" button
- **AC4: Round trip is unchanged**
  Given an automation loaded with an unnamed line
  When it is saved without edits
  Then the saved connection still has `sourceHandle: null` and `outcome: null`
- **AC5: Any result fires on every outcome**
  Given the unnamed line leads to step C
  When the action returns its declared outcome `notFound`
  Then step C runs
- **AC6: Any result fires on default too**
  Given the same automation
  When the action returns success with no outcome
  Then step C runs
- **AC7: Moving the line**
  Given the "Any result" line
  When the author reconnects it from the default exit and saves
  Then the saved connection has `outcome` equal to the default key

### S7: Outcome labels are translatable

As an **automation author** using the backoffice in another language,
I want built-in exit labels in my language,
so that the canvas reads naturally.

- **AC1: Key label is translated**
  Given an outcome labelled `#uaOutcomes_found` and an English term "Found"
  When the canvas renders the exit
  Then it shows "Found"
- **AC2: Literal label shown as-is**
  Given an outcome labelled `Breaking news`
  When the canvas renders the exit
  Then it shows "Breaking news"
- **AC3: API returns raw labels**
  Given the same outcomes
  When the catalogue or resolve endpoint returns them
  Then the labels are `#uaOutcomes_found` and `Breaking news`, untranslated
- **AC4: Labels are text, not HTML** (sad path)
  Given an outcome labelled `<img src=x onerror=alert(1)>`
  When the canvas renders the exit
  Then the label is shown as literal text and no element is created

### S8: Invalid settings return a clear error from output schema too

As an **automation author**,
I want the editor to get a clear error when a step's settings are invalid,
so that a bad setting doesn't surface as an unhandled server error.

- **AC1: Output schema returns 400** (sad path)
  Given settings the action's settings type rejects
  When `POST /step-types/{alias}/output-schema` is called
  Then the response is 400 "Invalid settings" with the resolver's message
- **AC2: Valid settings unchanged**
  Given valid settings
  When the output schema endpoint is called
  Then the response is the same 200 schema as today

---

## Epic B: Built-in actions offer real exits (slice 2)

### S9: Content and media actions offer named exits

As an **automation author**,
I want built-in content and media actions to have exits like "Found" and "Not found",
so that I can handle missing content without a separate If.

Applies to all 11: `GetContent`, `GetContentProperty`, `FindContent`, `CreateContent`,
`UpdateContentProperty`, `GetMedia`, `GetMediaProperty`, `FindMedia`, `CreateMedia`,
`UpdateMediaProperty`, `NotifyEditor`.

**Happy path**

- **AC1: Declares success plus existing outcomes** (one spec per action)
  Given the action
  When its outcomes are read
  Then they are `success` (default) followed by its existing `Outcome*` constants
- **AC2: Keys unchanged**
  Given the action
  When it returns a not-found result
  Then the returned key equals the constant's current value (e.g. `notFound`)
- **AC3: Labels are keys with English terms** (one spec per action)
  Given the action's outcomes
  When their labels are read
  Then each is a `#uaOutcomes_…` key with a term in `lang/en.ts`
- **AC4: Found routes to success**
  Given Get Content with `success` → step A and `notFound` → step B
  When the content exists
  Then step A runs and B doesn't
- **AC5: Not found routes to notFound**
  Given the same automation
  When the content doesn't exist
  Then step B runs and A doesn't

**Sad path**

- **AC6: Existing automations unchanged**
  Given an automation saved before this change with an unnamed line from Get Content to step C
  When the content doesn't exist
  Then step C still runs

### Later (placeholders, not planned)

- If, Switch and Request Approval move onto declared outcomes.
- Batch outcome resolution, if large automations show per-step requests are slow.
