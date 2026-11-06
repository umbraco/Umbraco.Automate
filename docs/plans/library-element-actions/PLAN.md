# Plan: Library element actions

**21 tasks in 6 areas.** Build on a new branch `v18/feature/library-element-actions` off `v18/dev`. Each task is one reviewable, committable unit. Pending specs are listed under each story in [STORIES.md](./STORIES.md).

Pending C# specs sit inside `#if LIBRARY_ELEMENT_ACTIONS … #endif`, because they use types that don't exist yet, and each test is `[Fact(Skip = "Pending: T<n>")]`. The task that adds the types deletes the guard and the `Skip`, then makes the file compile and pass. Pending acceptance specs are `test.fixme`, as in `bindable-settings`.

Order: the authorizer (T2) comes first because every action calls it. The six actions then run in parallel (group B), each adding only its own files. The acceptance specs come last, split by spec file so no two tasks edit the same one at once.

## Setup

- [x] **T1** - story: n/a. Create `v18/feature/library-element-actions` off `v18/dev` and commit this plan folder and the pending spec files `bdd-specs` wrote.
  Acceptance: the branch builds with the guarded spec files left out; `dotnet test Umbraco.Automate/Umbraco.Automate.slnx` passes; the frontend build passes; Playwright lists the new `test.fixme` tests as skipped.
  depends-on: none

## Core

- [x] **T2** - story: STORY-1 (AC1–AC13). Element access on the authorizer (ARCHITECTURE "New members on `IAutomationActionAuthorizer`" and "The authorizer wraps…"):
  - `IAutomationActionAuthorizer`: add `AuthorizeElementAsync` (the same two overloads `AuthorizeContentAsync` has) and `AuthorizeElementParentAsync(Guid? folderKey, …)`. Give each a default that fails with an "element authorization isn't supported" message, marked `// TODO (V19): Remove the default implementation.` like the existing ones.
  - `AutomationActionAuthorizer`: elements go through `IElementPermissionService`, and a folder goes through `IElementContainerPermissionService`. No folder means root access, through `AuthorizeRootAccessAsync`. Map `ElementAuthorizationStatus` the same way the content statuses are mapped, and reuse `IsHiddenInRecycleBin` with `UmbracoObjectTypes.Element` / `ElementContainer` and `Constants.System.RecycleBinElementPathPrefix`.
  - `AutomationActionAuthorizerExtensions`: add `AuthorizeElementOrFailAsync` and `AuthorizeElementParentOrFailAsync`, matching the content pair.
  Acceptance: `Security/AutomationActionAuthorizerElementTests.cs` goes green, and the existing authorizer tests stay green.
  depends-on: T1. parallel-group: A

- [x] **T3** - story: STORY-2 (AC2), STORY-3 (AC2). `IContentValueNormaliser` overloads that take `IPublishedElement`, implemented in `ContentValueNormaliser` with the same output shape as content. The interface defaults hand an `IPublishedContent` to the existing method and throw for anything else (ARCHITECTURE).
  Acceptance: new unit specs in `Umbraco.Automate.Tests.Unit/Cms/ContentValueNormaliserTests.cs` show an element's text, number and picker values come out in the same shape as the same values on a page.
  depends-on: T1. parallel-group: A

- [x] **T4** - story: STORY-8 (AC3, AC4, AC6). The element key picker: `Constants.EditorUiAliases.ElementKeyPicker = "Umb.Automate.ElementKeyPicker"`, plus `core/components/element-key-picker/element-key-picker.element.ts` and `manifests.ts`, registered in `core/manifests.ts`. It wraps `umb-input-element` with max 1, stores one key as a string, and takes `folderOnly` from its config. Copy `ua-media-key-picker` for structure. Check that `umb-input-element` with `folderOnly` lists folders only on CMS 18.1.0.
  Acceptance: the frontend build passes; on the demo site, a test settings field using the alias picks an element and stores one GUID string (checked by hand, recorded in BUILD-LOG). The real field check is T12.
  depends-on: T1. parallel-group: A

- [x] **T5** - story: STORY-2 (AC1–AC10). `GetElementAction`, `GetElementSettings`, `GetElementOutput` in `Actions/BuiltIn/`, copying `GetContentAction`: `[Action]` with group "Library", icon `icon-plugin`, `RequiredSections = [Constants.Applications.Library]`, `Umb.Element.Read`. Element field uses `ElementKeyPicker` with `SupportsBindings`. Reads `IPublishedElementCache.GetByIdAsync`. `IPublishedElement` has no `Trashed`, and the cache drops trashed elements (`ElementCacheService.cs:331`), so a trashed element comes back null and takes `notFound`. Outcomes `success` "Found" (default) and `notFound`. Output fields as SPEC. Logs one line with `ActionLogFormat.Item`.
  Acceptance: `GetElementActionTests.cs` green.
  depends-on: T2, T3, T4. parallel-group: B

- [x] **T6** - story: STORY-3 (AC1–AC8). `GetElementPropertyAction` with its settings and output, copying `GetContentPropertyAction`. Same attributes as T5. Outcomes `success`, `notFound`, `propertyNotFound`.
  Acceptance: `GetElementPropertyActionTests.cs` green.
  depends-on: T2, T3, T4. parallel-group: B

- [x] **T7** - story: STORY-4 (AC1–AC18). `UpdateElementPropertyAction` with its settings and output, copying `UpdateContentPropertyAction`. Group "Library", icon `icon-edit`, `Umb.Element.Update`, `ICmsAction`. Loads through `IElementService`, checks `Trashed` before writing, writes one value in the culture and segment the way Update Content Property does, and saves with `IElementService.Save` (ARCHITECTURE). Outcomes `success` "Updated", `notFound`, `propertyNotFound`. Output includes `PreviousValue`. A save cancelled by a notification maps to the Cancelled category.
  Acceptance: `UpdateElementPropertyActionTests.cs` green.
  depends-on: T2, T4. parallel-group: B

- [x] **T8** - story: STORY-5 (AC1–AC3, AC5–AC11). `PublishElementAction` with its settings and output, copying `PublishContentAction`. Icon `icon-globe`, `Umb.Element.Publish`, `ICmsAction`, no outcomes. Loads the element and checks `Trashed` before calling `IElementPublishingService.PublishAsync`. Maps the CMS's publish statuses to error categories the way `PublishContentAction.MapErrorCategory` does.
  Acceptance: `PublishElementActionTests.cs` green.
  depends-on: T2, T4. parallel-group: B

- [x] **T9** - story: STORY-6 (AC1–AC8). `UnpublishElementAction` with its settings and output, copying `UnpublishContentAction`. Icon `icon-globe`, `Umb.Element.Unpublish`, `ICmsAction`, no outcomes. Checks `Trashed` before calling `IElementPublishingService.UnpublishAsync`. Empty Cultures means all cultures: pass `null` for an invariant element and `{"*"}` for a variant one, because CMS 18.1.0 refuses the other two combinations (`CannotPublishInvariantWhenVariant`, `CannotPublishVariantWhenNotVariant`). The run log names the element, not only its key.
  Acceptance: `UnpublishElementActionTests.cs` green.
  depends-on: T2, T4. parallel-group: B

- [x] **T10** - story: STORY-7 (AC1–AC17). `CreateElementAction` with its settings and output, copying `CreateContentAction`'s settings shape:
  - Icon `icon-add`, `Umb.Element.Create`, `ICmsAction`.
  - Folder uses `ElementKeyPicker` with `folderOnly`, `SupportsBindings` and `BindingMustResolve`. Element Type uses `Umb.PropertyEditorUi.DocumentTypePicker` with `onlyPickElementTypes`.
  - Authorizes with `AuthorizeElementParentOrFailAsync`. Loads the folder through `IElementContainerService` and checks `Trashed`, because `CreateAsync` doesn't refuse a trashed folder (ARCHITECTURE). A missing or trashed folder takes `parentNotFound`, and an element type that doesn't resolve takes `elementTypeNotFound`.
  - Creates through `IElementEditingService.CreateAsync`. Its `NotAllowed` status covers both "not Allow in Library" and "not allowed in this folder", so the action fails with a validation error naming the type, and the folder when there is one. `CreateAsync` returns a successful attempt with status `PropertyValidationError` for invalid values: check the status, then log a warning naming the aliases in `ValidationResult.ValidationErrors`.
  - Settings mirror `CreateContentSettings`' real names: `FolderKey`, `ElementType`, `Name`, `Culture`, `PropertiesJson`.
  - Adds `uaOutcomes_folderNotFound` "Folder not found" and `uaOutcomes_elementTypeNotFound` "Element type not found" to `lang/en.ts`.
  Acceptance: `CreateElementActionTests.cs` green; the frontend build passes.
  depends-on: T2, T4. parallel-group: B

## Wire

- [x] **T11** - **wire: the six actions into the step catalogue on a running site.** story: STORY-8 (AC1, AC2, AC9, AC10, AC13). The actions are found through `[Action]`, so nothing should need registering. The step picker sorts groups by name, so Library lands between Content and Media on its own (`node-picker-modal.element.ts:160`). Add the element cases to `BuiltInActionOutcomeTests.cs` (outcome keys, defaults and `RequiredSections`). Turn on the "Step picker" and "Section access" tests in `automation.library-elements.spec.ts` and the element exit tests in `outcomes.built-in.spec.ts`, Move what the specs left as `TODO(T11)` into `lib/`: the action aliases, settings keys and outcome keys into `ConstantHelper` (check the settings keys against the real settings classes), and the duplicated `LibraryItems` fixture into `lib/helpers` and `testExtension.ts`.
  Acceptance: those acceptance tests pass against a running v18 demo site (a real catalogue request and a real refused publish).
  depends-on: T5, T6, T7, T8, T9, T10

- [x] **T12** - **wire: the element picker into the settings form.** story: STORY-8 (AC3–AC8, AC11, AC12). Turn on the "Element picker" and "Create Element settings" tests in `automation.library-elements.spec.ts`. Fix whatever the real backoffice shows up in T4's picker inside `ua-bindable-editor` (binding switch, required message, reopening a saved step).
  Acceptance: those tests pass against a running demo site.
  depends-on: T11. parallel-group: C

- [x] **T13** - **wire: element outcomes and the recycle bin on a running site.** story: STORY-2 (AC8, AC9), STORY-3 (AC6–AC8), STORY-4 (AC1, AC12, AC14, AC16), STORY-5 (AC9), STORY-6 (AC7), STORY-7 (AC1, AC10–AC12, AC14). Turn on the element sections of `outcomes.built-in.spec.ts` and `outcomes.recycle-bin.spec.ts`, using the helpers' `createDefaultElementFolder`, `createDefaultElementWithParent` and `moveToRecycleBin`. Run the trashed cases for a root-start-node account and a folder-start-node account, as #514 does for content.
  Acceptance: those tests pass against a running demo site.
  depends-on: T11. parallel-group: C

- [x] **T14** - **wire: folder permissions and publishing through to the page.** story: STORY-1 (AC14), STORY-5 (AC4). Turn on "Folder permissions" and "Publish reaches the page" in `automation.library-elements.spec.ts`. Permissions: give the service account's group an element permission set on folder "Reviews" without Create (`userGroupElementPermissionBuilder`). Page: a document type with an element picker, a template from `createTemplateWithDisplayingValue` that prints the picked element's property (the element-picker templates only print names), and a page that picks the element. The spec uses `textstring`, the alias the CMS element helpers give, in place of `customSchema`. Check that `userGroupElementPermissionBuilder` really restricts a folder on 18.1: the backoffice sends an `ElementContainerPermissionPresentationModel` for folders. Run Update Element Property then Publish Element, then load the page. If the page still shows the old value, stop: record it in DECISION-LOG and ask Rick before adding any cache refresh (BRIEF unknown).
  Acceptance: both tests pass against a running demo site.
  depends-on: T12, T13

## Docs

- [x] **T15** - story: STORY-1. Update `docs/identity-ownership-permissions.md` with the element and folder members on the authorizer, the folder service, and the trashed-element rule. Add a Library row to the provider table in `docs/functional-overview.md`. Draft the UmbracoDocs change for the built-in actions page in BUILD-LOG. **Human gate:** Rick opens the UmbracoDocs PR.
  Acceptance: the docs name every member T2 added; the links resolve.
  depends-on: T2, T11

## Triggers

Added when the triggers were folded into #574 (DECISION-LOG, 06-11-2026). Same pending-spec convention as above: C# specs sit behind `#if LIBRARY_ELEMENT_TRIGGERS`, acceptance specs are `test.fixme`.

- [x] **T16** - story: STORY-9 (AC17–AC20). Dispatch for elements (ARCHITECTURE "Trigger decisions"): add an internal `Dispatch/Authorization/IElementScopedTriggerOutput` (`Guid? GetElementKey()`), and one case in `NodeScopedTriggerDispatchAuthorizer.AuthorizeNodeAsync` calling `AuthorizeElementAsync(user, key, {ActionElementBrowse.ActionLetter}, ct)`. Widen `Triggers/BuiltIn/ContentCultureHelpers` from `IContent` to `IPublishableContentBase` with no behaviour change for content.
  Acceptance: element cases in `NodeScopedTriggerDispatchAuthorizerTests.cs` (single allowed/denied, batch narrowed, batch all denied) go green; existing content trigger tests stay green.
  depends-on: T15. parallel-group: D

- [x] **T17** - story: STORY-9 (AC1–AC4, AC7, AC8, AC11–AC15), STORY-10 (AC4). `ElementSavedTrigger` and `ElementBatchSavedTrigger` with `ElementSavedTriggerSettings` / `ElementSavedTriggerOutput`, copying the content Saved pair: `[Trigger]` group "Library", `RequiredSections = [Library]`, icons per SPEC, Element Types via `Constants.EditorUiAliases.DocumentTypePicker` with `onlyPickElementTypes`, the loop setting. The output implements `IElementScopedTriggerOutput`. Skip saves of an existing element that moved it, trashed it or restored it (not new, and `WasPropertyDirty("ParentId")` or `("Trashed")` on the saved element; ARCHITECTURE guess, with the moving-notification fallback). A new element's ParentId reads as dirty, so new elements always fire.
  Acceptance: `ElementSavedTriggerTests.cs` and `ElementBatchSavedTriggerTests.cs` green. The move/trash/restore guess is proved on a real site in T20; if it fails there, T20 switches to the fallback.
  depends-on: T16. parallel-group: E

- [x] **T18** - story: STORY-9 (AC5, AC9, AC10). `ElementPublishedTrigger` with settings and output, copying Content Published, reporting cultures through `ContentCultureHelpers`. No batch variant (Rick's call: element publishes never batch).
  Acceptance: `ElementPublishedTriggerTests.cs` green.
  depends-on: T16. parallel-group: E

- [ ] **T19** - story: STORY-9 (AC6, AC16). `ElementUnpublishedTrigger` with settings and output, copying Content Unpublished. No batch variant. A restore that unpublishes fires it (Rick's call).
  Acceptance: `ElementUnpublishedTriggerTests.cs` green.
  depends-on: T16. parallel-group: E

- [ ] **T20** - **wire: the element triggers on a running site.** story: STORY-9 (AC1, AC5, AC6, AC13–AC17, AC21), STORY-10 (AC1–AC3, AC5). Turn on `automation.library-element-triggers.spec.ts` against the LocalDB demo site: the trigger picker's Library group (four triggers), the settings form, an editor's save/publish/unpublish (through the CMS Management API) starting a run, move/trash/restore not starting Element Saved, restore firing Element Unpublished, a folder permission without Read stopping dispatch, and no loop with Update Element Property. Add element trigger aliases and helpers to `ConstantHelper` / `LibraryApiHelper`. Screenshot the trigger picker and Element Saved settings into `screenshots/`.
  Acceptance: the spec passes against the running demo site.
  depends-on: T17, T18, T19

- [ ] **T21** - story: STORY-9. Docs: `docs/identity-ownership-permissions.md` (element triggers at dispatch: Read on the element, batch narrowing, trashed hidden from scoped accounts), `docs/functional-overview.md` (Library row lists the six triggers), and the UmbracoDocs draft in BUILD-LOG (triggers page; note that restoring a published element fires Element Unpublished).
  Acceptance: the docs name every trigger and match the code.
  depends-on: T20
