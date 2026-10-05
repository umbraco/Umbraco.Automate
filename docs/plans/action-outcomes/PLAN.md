# Plan

Execute top to bottom. A task may start once everything in its `depends-on` is checked.
Tasks that share a `parallel-group` and whose dependencies are met can be built side by side,
because they touch different files.

Paths are relative to `Umbraco.Automate/src/` unless shown otherwise. Client paths are under
`Umbraco.Automate.Web.StaticAssets/Client/src/`.

**Proving action.** Slice 1 needs one real action in the deployed demo site that declares
outcomes, so the canvas and runtime can be proven through the real artifact. That action is
**Get Content** (T11). The other 10 built-ins follow in slice 2. Unit and integration specs use
test-only actions (a static yes/no action, and a dynamic options action) in
`Umbraco.Automate.Tests.Common`/the test projects.

---

## Slice 1: contract, runtime, API, canvas

- [x] **T1** Add `StepOutcome` and the outcome members on `IStepType` (default interface members) and `StepTypeBase` (virtuals, plus the typed `GetOutcomesAsync(TSettings?)` and the explicit-interface bridge that mirrors `GetOutputSchemaAsync`) · story: S1 · depends-on: none · parallel-group: A
  - Files: `Core/StepTypes/StepOutcome.cs` (new), `Core/StepTypes/IStepType.cs`, `Core/StepTypes/StepTypeBase.cs`
  - Done when: S1 AC1–AC3 specs pass; an existing action compiles unchanged.
- [x] **T2** Add a `ContainsBinding()` string extension in `Umbraco.Automate.Extensions`, backed by `BindingTokenizer.FindBindings` · story: S4 · depends-on: none · parallel-group: A
  - Done when: S4 AC9 and AC13 specs pass.
- [x] **T3** Return a 400 `ProblemDetails` "Invalid settings" from `ResolveStepTypeOutputSchemaController` when `ResolveSettings` throws `InvalidOperationException` · story: S8 · depends-on: none · parallel-group: A
  - Done when: S8 AC1–AC2 specs pass (extend `ResolveStepTypeOutputSchemaControllerTests`).
- [x] **T4** Add the internal `StepOutcomeValidator` (unique non-empty keys, no `__` prefix, at most one default) · story: S1 · depends-on: T1 · parallel-group: B
  - Done when: S1 AC5–AC9 and AC8 (no default allowed) specs pass.
- [x] **T5** Add `AddDanglingOutcomeErrors` to `AutomationService.ValidateForPublishAsync`, resolving each declaring step's outcomes (`HasDynamicOutcomes || GetOutcomes().Count > 0`) from its saved settings, and reporting a resolution failure as its own error · story: S5 · depends-on: T1 · parallel-group: B
  - Done when: S5 AC5–AC9 and AC11 specs pass, and S5 AC3 (draft save allowed) passes.
- [x] **T6** Add `outcomes` and `hasDynamicOutcomes` to `StepTypeItemResponseModel`, mapped in `CatalogueMapDefinition` (all three map methods) · story: S3, S7 · depends-on: T1 · parallel-group: B
  - Done when: S3 AC1–AC2 and S7 AC3 (catalogue part) specs pass (extend `CatalogueMapDefinitionTests`).
- [x] **T7** Add `ResolveStepTypeOutcomesController`, `POST step-types/{alias}/outcomes`, with `ResolveOutcomesRequestModel` / `StepOutcomeResponseModel`, using the same 404 and 400 handling as output schema · story: S4, S7 · depends-on: T1, T3 · parallel-group: B
  - Done when: S4 AC2, AC3, AC10, AC11 and S7 AC3 (endpoint part) specs pass.
- [x] **T8** Make Get Content declare `success` (default, `#uaOutcomes_found`) and `notFound` (`#uaOutcomes_notFound`), and add English terms to `lang/en.ts` · story: S9, S7 · depends-on: T1 · parallel-group: B
  - Done when: S9 AC1–AC3 specs pass for Get Content.
- [x] **T9** In `ActionStepBody`: resolve outcomes lazily from `_stepConfig.Settings` when the action declares any; validate them with `StepOutcomeValidator` (a failure, or `GetOutcomesAsync` throwing, becomes a step failure with category `Validation`); map "no outcome" to the default, or fail the step when there's no default; set `StepRun.BranchOutcome` for every returned outcome; log a warning for an undeclared outcome · story: S2, S4 · depends-on: T1, T4 · parallel-group: C
  - Confirm while building: `IStepErrorClassifier` treats `Validation` as terminal (SPEC assumption).
  - Done when: S2 AC1–AC13 and S4 AC1, AC7, AC8 specs pass, as integration tests modelled on `ApprovalOutcomeTests`.
- [x] **T10** Extend `ActionTestHarness<TAction>` with the resolved outcomes for given settings and the effective branch outcome of an execution · story: S1 · depends-on: T1, T9 · parallel-group: D
  - Done when: S1 AC4 spec passes, plus one harness spec for the default mapping.
- [x] **T24** Persist `StepRun.BranchOutcome`: add `BranchOutcome` to `StepRunEntity`, map it both ways in `StepRunFactory`, configure it in the DbContext like the other short strings, and add the `UmbracoAutomate_AddStepRunBranchOutcome` migration for SQL Server and SQLite (same shape as `UmbracoAutomate_AddStepRunLogEntries`) · story: S10 · depends-on: T9 · parallel-group: D
  - Done when: S10 AC1–AC2 and AC7 pass as integration tests against a real database, and both migrations apply cleanly to an existing database.
- [x] **T25** Return `branchOutcome` on step runs from the run API (the step run response model and its mapping) · story: S10 · depends-on: T24 · parallel-group: E
  - Done when: S10 AC3 passes, plus a spec that a step run with no exit returns `null`.
- [x] **T11** **wire: outcomes into the Management API.** Regenerate the OpenAPI client (`api/types.gen.ts`, `sdk.gen.ts`) · story: S3, S4, S10 · depends-on: T3, T6, T7, T8, T25 · parallel-group: F
  - Done when: against the running demo site, a real run of an automation with a Get Content step, reloaded through the run API, returns `branchOutcome` for that step; a real `GET` of the actions catalogue returns Get Content with its two outcomes; a real `POST .../step-types/<getContent alias>/outcomes` returns them; a real `POST .../step-types/nope/outcomes` returns 404; and the regenerated client builds.
- [ ] **T12** Map `outcomes`/`hasDynamicOutcomes` in `catalogue/type-mapper.ts` and add `resolveOutcomes(alias, settings)` to `catalogue.repository.ts` · story: S3, S4 · depends-on: T11 · parallel-group: E
  - Done when: the client builds and the repository call works against the demo site (checked in T16).
- [ ] **T13** Canvas data: add `outcomes` to `CatalogueLookupEntry`/`ActionNodeData`; fill it in `#buildCatalogueLookup`/`modelToNodes` (resolving dynamic steps on load); return the default key (else the first key) from `getContinuationSourceHandle`; map unnamed lines on declaring steps to `__any__` and back in `flow-to-model.ts` · story: S3, S4, S6 · depends-on: T12 · parallel-group: F
  - Done when: S3 AC6, AC8, S4 AC4 and S6 AC4 pass in T16.
- [ ] **T14** `ActionNode.tsx`: exits on the right edge whenever the action declares outcomes (Switch layout, regardless of count) with "+" buttons, a warning while "Any result" and a named line both leave the node, "(default)" mark, "Any result" exit only when an unnamed line exists (no "+"), red "Missing outcome" exits for stale lines (including an empty dynamic list), the Found tooltip on the two property actions, labels through `localize.string()` as text, add buttons hidden in run view · story: S3, S5, S6, S7 · depends-on: T13 · parallel-group: G
  - Done when: S3 AC3, AC4, AC7, AC9, AC10, S5 AC1, AC10, S6 AC1–AC3, AC8, AC9 and S7 AC1, AC2, AC4 pass in T16.
- [ ] **T15** Workspace view: after the settings modal submits for a dynamic-outcome step, call `resolveOutcomes` and update that node; on failure keep the old exits and show the standard error notification; never drop lines · story: S4, S5 · depends-on: T13 · parallel-group: G
  - Done when: S4 AC5, AC6, AC12 and S5 AC2 pass in T16.
- [ ] **T26** Run view: style the taken / not-taken lines on the run canvas and show "Exit taken: <label>" in the step run detail, for actions, If and Switch · story: S10 · depends-on: T11, T13 · parallel-group: G
  - Done when: S10 AC4–AC6 and AC8 pass in T16.
- [ ] **T16** **wire: outcome exits into the canvas.** Playwright acceptance specs in `Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/`, against the running demo site · story: S3, S4, S5, S6, S7, S9, S10 · depends-on: T5, T9, T14, T15, T26 · parallel-group: H
  - Get Content (real): its exits render; draw `notFound` → step, save, and the saved connection has `outcome: "notFound"` (S3 AC5); publish, then run with missing content and the `notFound` path runs (S9 AC4–AC5 for Get Content); an old unnamed line shows as "Any result" and still fires (S6 AC5–AC7); drawing "Found" next to it shows the warning (S6 AC8–AC9); auto-layout doesn't overlap the right-edge exits (S3 AC11).
  - Dynamic behaviour (no built-in has dynamic outcomes): stub the catalogue and `/outcomes` responses with Playwright `page.route`, then check the exits on load, after a settings save, after a resolve failure, and for a stale line (S4 AC4–AC6, AC12; S5 AC1–AC2).
  - Stale publish: publish an automation whose line uses an outcome Get Content doesn't declare, and the UI shows the publish error from S5 AC5.
  - Run view: run the Get Content automation, open the run, and the `notFound` line is styled as taken and the `success` line as not taken; the step detail shows "Exit taken: Not found" (S10 AC4–AC6).
- [ ] **T17** Document outcomes for action developers: add **Outcome** to `docs/vocabulary.md`, and an outcomes section (static, dynamic, default, keys vs labels, the unbound-settings rule, `ContainsBinding()`) next to the output-schema guidance in `docs/engineering-spec.md` · story: S1, S4 · depends-on: T1, T2, T9 · parallel-group: H
  - Done when: both docs describe the shipped contract, and an AI decision example matches ARCHITECTURE's consumer contract.

## Slice 2: built-in actions

- [ ] **T18** Content actions declare outcomes: `GetContentProperty`, `FindContent`, `CreateContent`, `UpdateContentProperty`, `NotifyEditor`, with English terms in `lang/en.ts` · story: S9 · depends-on: T8, T16 · parallel-group: I
  - Done when: S9 AC1–AC3 and AC6 specs pass for each, plus S9 AC7 for Get Content Property.
- [ ] **T19** Media actions declare outcomes: `GetMedia`, `GetMediaProperty`, `FindMedia`, `CreateMedia`, `UpdateMediaProperty`, with English terms in `lang/en.ts` · story: S9 · depends-on: T18 · parallel-group: J
  - Runs after T18, not alongside it, because both edit `lang/en.ts`.
  - Done when: S9 AC1–AC3 and AC6 specs pass for each, plus S9 AC7 for Get Media Property.
- [ ] **T20** **wire: built-in exits in the demo site.** One Playwright spec per group (Get Media and Find Content) proving the not-found exit routes through the real site · story: S9 · depends-on: T19 · parallel-group: K

## Port to v17

- [ ] **T21** Port slice 1 to `v17/dev` (including T24's migrations, regenerated on the v17 line) via the Backport Workflow (`CONTRIBUTING.md`): a `v17/feature/*` branch from `v17/dev`, then build, unit, integration and acceptance tests on v17 · story: all of Epic A · depends-on: T16, T17 · parallel-group: L
  - Done when: the v17 PR is green and the same acceptance specs pass against a v17 demo site.
- [ ] **T22** Port slice 2 to `v17/dev` · story: S9 · depends-on: T20, T21 · parallel-group: M

## Consumers

- [ ] **T23** Tell Umbraco.AI the minimum Automate version once slice 1 is released on each line: open an issue (or PR) on Umbraco.AI to raise `Umbraco.Automate.Core` in `Directory.Packages.props` from `[18.0.0, …)` to the release that ships outcomes, and the matching v17 floor. Include the consumer contract from ARCHITECTURE (Yes/No and Pick-One with no default, Score keyed by level name plus a duplicate-level check in Core, Ask Questions declaring none) · story: none (external dependency) · depends-on: T21 · parallel-group: N
  - Done when: the Umbraco.AI issue/PR exists and links this plan. The version bump itself happens in Umbraco.AI.
