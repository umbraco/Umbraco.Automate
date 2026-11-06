# Build log: Library element actions

- **T1** `340c0048` (05-11-2026): branch `v18/feature/library-element-actions` off `v18/dev` at `5d329ce4`, worktree `.claude/worktrees/v18-library-element-actions`. Plan folder and pending specs moved off `v18/dev` and committed here. `dotnet build Umbraco.Automate.slnx`: 0 errors. Unit tests: 2391 passed, with the `LIBRARY_ELEMENT_ACTIONS` spec files compiled out. Frontend not built: T1 changes no frontend file. Playwright listed the 32 `test.fixme` tests before the move (same files, main checkout); this worktree has no acceptance `node_modules`.
- **T2** `96a24260` (05-11-2026): element and folder checks on `IAutomationActionAuthorizer`, with fail-closed defaults. Reviewer PASS on the first round. Unit tests: 2408 passed (17 element authorizer specs active, 245 Security). Nothing calls the new members yet, so there's no entry point to exercise. The reviewer confirmed CMS 18.1.0 registers both `IElementPermissionService` and `IElementContainerPermissionService` (`UmbracoBuilder.cs:320-321`), so the new constructor can't break startup. The site check is in T11.
- **T3** `f6c90b6a` (05-11-2026): `IPublishedElement` overloads on `IContentValueNormaliser`. Reviewer FAIL in round 1: the content reads had moved onto the element `Value` overload, which loses the CMS's overridable content fallback and per-content segment. The `TODO (V19)` markers were also missing. Round 2 PASS: content reads go through the content overload again, and a Verify spec proves it (the reviewer traced that it fails against round 1). Unit tests: 2419 passed. Get Content / Get Media go through this code, so the site check runs `outcomes.get-content.spec.ts` with T4, which is the first task that starts the demo site.
- **T4** `b90e28ba` (05-11-2026): `ua-element-key-picker` (`Umb.Automate.ElementKeyPicker`), wrapping `umb-input-element` with max 1 and a `folderOnly` config. Reviewer PASS on the first round. `dotnet build` and `npm run build` pass. On the worktree demo site (port 44300, listener checked as this worktree's exe), `/App_Plugins/UmbracoAutomate/umbraco-automate-manifests.js` registers the alias and its chunk serves 200. No settings field uses it until T5, so checking it on screen is in T12. **T3 site check, done here:** `outcomes.get-content.spec.ts` against this site, 26/26 (two flaky canvas tests passed on rerun). Runs went through Get Content on the real site. **Demo setup:** the first run hit `SQLite Error 6: 'database table is locked'` (the known CMS cache-instruction deadlock with `Cache=Shared`). Dropping `Cache=Shared` from this worktree's `demos/v18/.../appsettings.Development.json` cleared it. The acceptance `.env` points at `https://127.0.0.1:44300` and this worktree's storage state. Both files are gitignored.
- **T5** `c4dfabdc` (05-11-2026): Get Element. Reviewer FAIL in round 1 because the field was labelled "Element Key", not "Element" as SPEC and the approved mockup have it. The `notFound` comment also claimed a culture check the code doesn't make. Round 2 PASS. Unit tests: 2430 passed (11 Get Element specs). The site check for T5–T10 is together in T11: the actions are found through `[Action]` with no wiring of their own, so restarting the site after each one adds nothing.
- **T6** `0306ef5b` (05-11-2026): Get Element Property. Reviewer PASS on the first round. Unit tests: 2438 passed (8 Get Element Property specs). `ResolveName`/`NormaliseCulture` are private copies, as in every content and media read action. Site check in T11.
- **T7** `63bb19f1` (05-11-2026): Update Element Property, saving through `IElementService.Save`. Reviewer FAIL in round 1 (critical): the Element field still used the content picker (`Umb.PropertyEditorUi.DocumentPicker`, "Content Key"), so Choose would have opened the document tree. Round 2 PASS: the field uses `ElementKeyPicker` and a new spec (`ElementKeyField_UsesTheElementPicker`) pins it. Unit tests: 2464 passed (26 Update Element Property specs). The reviewer confirmed at 18.1.0 that `Save` is the same draft-only path content uses, and that a null segment is safe on `IElement` reads and writes. Site check in T11.
- **T8** `f1065071` (05-11-2026): Publish Element through `IElementPublishingService.PublishAsync`, which returns the same `ContentPublishingOperationStatus` as content, so `MapErrorCategory` is a straight copy. Reviewer PASS on the first round. Unit tests: 2482 passed (18 Publish Element specs, including the picker guard). Carried to the whole-feature review: the guard spec's comment says STORY-5 AC3 where it means STORY-8 AC3, and `ParseCultures`/`MapErrorCategory` are copied from Publish Content. Site check in T11. R14 (pages show the new value) is T14.
- **T9** `3e08298e` (05-11-2026): Unpublish Element. Empty Cultures sends null to an invariant element and `{"*"}` to a variant one (the reviewer confirmed against 18.1.0 `ContentPublishingServiceBase.UnpublishAsync`). Reviewer PASS on the first round. Unit tests: 2497 passed (15 Unpublish Element specs, including the picker guard). Site check in T11.
- **T10** `a1216813` (06-11-2026): Create Element. Took three review rounds.
  - **Round 1 FAIL:** property values went through `CreateAsync`'s backoffice-format conversion, so list and picker values in Create Content's format were silently dropped. The builder's extra branches also had no specs.
  - **Rick's call:** use the same format as Create Content: create with the name only, then `SetValue` and one `IElementService.Save`. SPEC R10 and STORY-7 AC17 were updated. The same round added specs for every branch, case-insensitive alias matching, the mockup's Element Type description, and a specific error for a non-element type.
  - **Round 2 FAIL:** one spec asserted something that was now always true, so the "no second save" rule had no spec.
  - **Round 3 PASS:** two never-saves specs, shown to fail when that rule is broken.
  - Unit tests: 2563 passed (67 Create Element specs). Frontend build passes (new `folderNotFound`/`elementTypeNotFound` labels).
  - Carried to the whole-feature review: putting `CreateAsync` and the second `Save` in one scope, so a failed save doesn't leave an element with only its name.
  - Site check in T11.
- **T11** `81794915` (06-11-2026): the six actions on the running demo site (44300, restarted with T2–T10 and a fresh frontend build). Reviewer PASS on the first round.
  - Unit tests: 2585 passed (element cases in `BuiltInActionOutcomeTests` active). `tsc` clean.
  - Acceptance, `automation.library-elements.spec.ts` + `outcomes.built-in.spec.ts`: 13 passed, 20 `fixme` (T12–T14). The step picker lists a Library group with exactly the six actions, searching "element" lists all six, publishing an Update Element Property step with a Content-only service account is refused with "requires section access", and Create Element draws Created (default), Folder not found and Element type not found.
  - Unchanged Find Content / Get Media tests timed out a few times in fixture setup and passed on rerun.
  - The step picker sorts groups by name, so it reads Content, Control Flow, Core, Library, Media. The approved mockup drew Library right after Content (Rick to confirm, see DECISION-LOG).
  - Screenshot: [step-picker.png](./screenshots/step-picker.png), cropped to the picker. Click path: Automation › any workspace with a service account › an automation › Add action on the trigger › search "element". No "before" shot was taken before the task started; the picker only gained a group.
- **T12** `7995c86e` (06-11-2026): the element picker in the step settings form. Two review rounds.
  - **Product bugs the real backoffice showed:**
    - Choose did nothing: `umb-input-element` needs a router context the settings panel lacks. `ua-element-key-picker` now opens the CMS element and folder picker modals itself.
    - An empty Element Type saved: the CMS document type picker ignores "required". New `ua-document-type-picker`.
  - **Round 1 FAIL:** a saved key that no longer resolved showed an endless loader with no Remove. The AC8 test title also contradicted its assertion.
  - **Round 2 PASS:** "Not found" with Remove, plus a spec for it.
  - Rick OK'd that the Element Type picker greys out page types rather than hiding them, as the CMS does. AC8 reworded.
  - **Acceptance** (`automation.library-elements.spec.ts`, Element picker + Create Element settings): all 10 pass on the restarted site (C# alias change). Earlier failures were in fixture setup, from `SQLite Error 5: database is locked` on the local SQLite demo, which a site restart cleared; "reopened" needed one rerun.
  - **Screenshots**, cropped to the settings panel:
    - [update-element-property.png](./screenshots/update-element-property.png). Click path: Automation › a workspace with a service account › an automation › Update Element Property step › Settings.
    - [create-element.png](./screenshots/create-element.png), shown with the Element Type "required" error after saving empty. Click path: the same, with a Create Element step.
  - **Compared with the mockups:** same fields and order. The picked element shows no icon where the mockup had one; the test element type has no icon.
- **T13** `fce62501` (06-11-2026): element outcomes and the recycle bin on a real site. Reviewer PASS on the first round.
  - **On SQLite:** each run stalled a different handful of tests, content and element alike, on `SQLite Error 5: 'database is locked'`, even straight after a restart.
  - **Demo setup changed to match CI:** the worktree site now runs on SQL Server LocalDB, through env vars only, the same ones `azure-pipelines.yml` sets (`CONNECTIONSTRINGS__UMBRACODBDSN=Server=(localdb)\MSSQLLocalDB;Database=AutomateLibraryElementsWt;…`, provider `Microsoft.Data.SqlClient`, `UMBRACO__CMS__GLOBAL__INSTALLMISSINGDATABASE=true`), giving a fresh unattended install.
  - It runs on `--urls https://127.0.0.1:44311`, because a Forms test site from another worktree took 44300. The acceptance `.env` points at 44311.
  - **On LocalDB,** `-g "Element|Library"` across both specs passed 20/20 in one run.
  - **Reviewer suggestion, not done:** a site-level test of a trashed element (not folder) for a folder-scoped account. The unit tests and the AC12 folder case already exercise that path.
- **T14** `760891f7` (06-11-2026): folder permissions and publishing through to the page, on the LocalDB demo site (44311). Two review rounds.
  - **Folder permissions:** at 18.1 the CMS stores a folder's granular permission through `ElementPermissionMapper` (keyed on the folder's key). The deepest explicit set on the path replaces the group defaults, so a "Reviews" set without Create refuses it. Automate's step fails with "…lacks the required permissions (Umb.Element.Create) on library folder '<key>'". No control run with Create granted was needed: the reviewer showed the test fails if Automate stops checking the folder.
  - **Round 1 FAIL:** the page test never rendered the page before the run, so it couldn't catch a stale page. The element picker converter is `PropertyCacheLevel.Elements`.
  - **Round 2 PASS:** the page renders a unique old value first; after Update + Publish Element, a fresh request shows the new value.
  - **R14 is answered:** publishing an element refreshes pages that already rendered it, and no cache refresh code is needed. (BRIEF unknown; recorded in DECISION-LOG.)
  - Acceptance: `automation.library-elements.spec.ts` 15/15 with no reruns.
- **T15** `3f7b4003` (06-11-2026): docs. Three review rounds.
  - **Round 1 FAIL:** a stale "content only" permission row, a trashed-rule sentence that was wrong for the Get actions, and Publish/Unpublish described as always a validation failure. The UmbracoDocs draft also had PascalCase outputs and didn't match the target page's structure.
  - **Round 2:** the repo docs passed; the draft still needed the linked workspaces.md updated, and said `cultures` was "empty" where it's null. The orchestrator fixed both in the draft (it's outside the repo).
  - **Repo changes:** `identity-ownership-permissions.md` gained a Library elements and folders section naming every T2 member. `functional-overview.md` got a Library row. The `IAutomationActionAuthorizer` summary names both element services.
  - **Human gate:** the UmbracoDocs draft is below, for Rick to open as a PR.
- **Finish** (06-11-2026):
  - **Full .NET suite:** 2586 unit + 272 integration passed.
  - **Full acceptance suite on the LocalDB demo site (44311):** 194 passed in one run.
  - **Whole-feature review round 1 FAIL:** a dead public `AuthorizeElementParentOrFailAsync`, and a SPEC target Create Element no longer meets.
  - **Refactor `8dff1ac9`:**
    - the dead method removed;
    - public element authorize overloads taking `IReadOnlyList<string>`;
    - Create Element's two writes in one scope (CMS 18.1.0 scope nesting checked), with a "not created" failure message;
    - test tidy.
    - SPEC Targets and ARCHITECTURE updated to match.
  - **Round 2 PASS.**
  - **After the refactor:** unit tests 2593 passed. The element acceptance specs (`automation.library-elements`, `outcomes.built-in`, `outcomes.recycle-bin`) passed 46/46 on the restarted site.
  - **Follow-ups, not this PR:**
    - a shared helper for the per-action private copies;
    - the culture-from-type rule in the three Update actions (T7);
    - Create Content's Content Type required gap (T12);
    - `CannotUnpublishWhenReferenced` → Unknown;
    - `docs/engineering-spec.md:648` names the wrong namespace for the authorizer extensions (predates this feature);
    - a spec pinning that Create Element's scope opens before `CreateAsync`.

- **T16** `f4e3b78c` (06-11-2026): element support in trigger dispatch. An internal `IElementScopedTriggerOutput`, and a `NodeScopedTriggerDispatchAuthorizer` case checking Umb.Element.Read for the workspace's service account. `ContentCultureHelpers` takes `IPublishableContentBase` (no behaviour change). Reviewer PASS on the first round. Unit tests: 2611 passed (17 element dispatch specs). No real output uses the marker until T17–T19.

## UmbracoDocs draft (for Rick)

> AI-assisted draft — pending review. Not published. Rick opens the UmbracoDocs PR.
>
> Probable target: `18/umbraco-automate/concepts/actions.md`, plus one edit to `18/umbraco-automate/concepts/workspaces.md`. Rick to confirm. Three changes, below. The page has no per-action output sections, so none are added. Outputs are mentioned in prose only, in camelCase as authors write bindings.

#### Insertion 1: new `### Library` table

Place it after the `### Media` table and before the line "Add-on packages contribute additional actions."

##### Library

| Action                       | Purpose                                                                                      |
| ---------------------------- | -------------------------------------------------------------------------------------------- |
| **Create Element**           | Create a Library element in a folder or at the Library root (draft save).                    |
| **Get Element**              | Fetch a published Library element and expose its properties for downstream steps.            |
| **Get Element Property**     | Read a single property value from a published Library element.                              |
| **Update Element Property**  | Write a single property value on a Library element (draft save).                             |
| **Publish Element**          | Publish a Library element.                                                                   |
| **Unpublish Element**        | Unpublish a Library element.                                                                 |

#### Insertion 2: new `## Library Elements` section

Place it after the `## Create and Move Items` section and before `## Step Behaviour`.

#### Library Elements

The Library actions need Umbraco 18, because earlier versions have no Library. The service account of the workspace needs access to the **Library** section, and the permission for the action on the element or folder it works on:

| Action                                           | Permission |
| ------------------------------------------------ | ---------- |
| **Get Element**, **Get Element Property**        | Read       |
| **Create Element**                               | Create     |
| **Update Element Property**                      | Update     |
| **Publish Element**                              | Publish    |
| **Unpublish Element**                            | Unpublish  |

**Create Element** saves a new draft. Add a **Publish Element** step after it to publish the element. Leave **Folder** empty to create the element at the Library root. The permission is then checked on the Library root, which needs root access for the service account. See [Service-Account Permissions](workspaces.md#service-account-permissions). You can only create element types that have **Allow in Library** turned on.

Set **Culture** when the element type varies by culture, for example `en-US`. Enter **Property Values** as a JSON object of property aliases and values, in the same format as **Create Content**. See [Create and Move Items](actions.md#create-and-move-items). The step outputs `elementKey`, `name`, `contentTypeKey`, `contentTypeAlias`, and `folderKey`. The **Folder not found** and **Element type not found** outcomes run when the folder or the element type no longer exists.

**Get Element** outputs `elementKey`, `name`, `contentTypeAlias`, `contentTypeKey`, `culture`, `availableCultures`, `createDate`, `updateDate`, `creatorKey`, `writerKey`, and `properties`. **Get Element Property** outputs `elementKey`, `propertyAlias`, `culture`, `value`, and `hasValue`. **Update Element Property** outputs `elementKey`, `propertyAlias`, `culture`, `segment`, and `previousValue`. **Publish Element** and **Unpublish Element** output `elementKey` and `cultures`. `cultures` has no value (null) when **Unpublish Element** unpublished all cultures.

Set **Culture** on the get and update actions for a variant element, and leave it empty for an invariant one. Set **Segment** on **Update Element Property** for a segmented element. For **Publish Element** and **Unpublish Element**, enter **Cultures** as comma-separated codes, for example `en-US,da-DK`. Leave it empty to publish an invariant element, or to unpublish all cultures.

An element or folder that is missing or in the recycle bin is treated as not found. This applies to an account confined to a start node, and to an account with root access. The get and update actions, and **Create Element** for its folder, run their **Not found** or **Folder not found** outcome. **Get Element** and **Get Element Property** also run it when the element is not published. **Get Element Property** and **Update Element Property** run **Property not found** when the element type has no property with that alias. **Publish Element** and **Unpublish Element** have no outcomes, so the step fails.

#### Change 3: `workspaces.md`, "Service-Account Permissions"

Insertion 2 links here for root access, and this section still says the granular checks are for content only. Make these three edits in the layer table and the sentence under it.

- **Start node / Browse permission** row, first cell: change `(content and media triggers and actions)` to `(content, media and Library triggers and actions)`.
- **Granular permission** row:
  - First cell: change `(content actions only)` to `(content and Library actions)`.
  - Second cell: change `(Create, Publish, Update, Move)` to `(Create, Publish, Update, Move, and for Library elements Read and Unpublish)`.
  - Third cell: append ` The Library actions check the same way on the element, or on the folder for **Create Element**.`
- The sentence under the table: change "at the content or media root" to "at the content, media or Library root".
