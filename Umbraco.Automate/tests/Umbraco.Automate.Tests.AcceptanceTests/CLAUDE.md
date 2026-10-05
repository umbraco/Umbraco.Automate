# Umbraco.Automate.Tests.AcceptanceTests

Playwright end-to-end tests that drive a **running** demo site
(`demos/vN/Umbraco.Automate.DemoSite`) through the backoffice, using
`@umbraco-cms/acceptance-test-helpers`. Specs live under `tests/DefaultConfig/`; auth is
bootstrapped by `tests/auth.setup.ts`. Commands are in [README.md](README.md).

Runs locally and in CI. The `AcceptanceTests` stage in `azure-pipelines.yml` runs
alongside `Build` and gates pull requests too — see "How the CI stage works" below.

---

## The demo site is not committed — that is deliberate

Forms has `examples/Umbraco.Forms.TestSite` checked in. Automate does not, and should not: the
whole `demos/` tree is gitignored and generated per-developer by `scripts/install-demo-site.*`.
Consequences for this suite:

- There is **no fixed URL**. `Umbraco.Community.WorktreeDevPort` assigns each worktree its own
  stable port the first time its demo site starts, and stores it in that worktree's git config
  under `wdp.port`. The main checkout gets `44380` when free. `config.js` reads it with
  `git config --worktree --get wdp.port`.
- A demo site started from a **different worktree** has a different port, so `npm run config`
  will not find it. Pass `--worktree <path>` or `--port <number>` in that case. If `wdp.port` is
  unset, the site has never started in this worktree.
- If `demos/vN/` does not exist, generate it with `scripts/install-demo-site.{sh,ps1}` or
  `/repo-setup`. Never create demo files by hand.

## Navigate by URL, never by clicking the sidebar

The sidebar's create buttons sit under the resizable `umb-split-panel` divider, which
intermittently **intercepts pointer events** — a normal `.click()` silently no-ops and the next
wait hangs with no useful error. This is confirmed in Automate, not just inherited from Forms.

`AutomateUiHelper` therefore exposes route builders (`automationCreateUrl`, `connectionEditUrl`,
`workspaceRootUrl`, …) mirroring `paths.ts` in the client, and every click it makes uses
`{ force: true }`. Add routes there rather than clicking through the tree.

One route trap: creating an automation inside a workspace takes `ua:workspace` as the parent
entity type. Passing `ua:automation-root` silently produces an automation whose `workspaceId` is
the empty Guid.

## Automate elements do not carry `data-mark`

`playwright.config.ts` sets `testIdAttribute: 'data-mark'`, which is the CMS backoffice
convention, so `getByTestId` reaches CMS chrome (`section-links`,
`workspace:action-menu-button`) but **not** any Automate component. Target Automate's own
elements by custom element name instead: `ua-automate-dashboard`, `ua-node-picker-modal`, and
the rest of the `ua-*` elements under `Umbraco.Automate.Web.StaticAssets/Client/src`. Grep the
client source to confirm a name before relying on it.

This also bites on the canvas: xyflow tags nodes `data-testid="rf__node-<id>"`, which
`getByTestId` will **not** find. `AutomateUiHelper.canvasNode()` matches the attribute directly.

The exception is anything the CMS renders **from a manifest**: workspace and entity actions get
`data-mark="workspace-action:<alias>"` / `"entity-action:<alias>"`, and property actions sit
behind `data-mark="open-property-actions"`. Those are the most stable handles there are — they
do not change with a label or a translation — so `clickEntityAction`, `workspaceAction` and
`openBindingPicker` use them, and the aliases live in `ConstantHelper.extensions`.

In Playwright MCP the test id attribute is the default `data-testid`, not `data-mark`, so
`getByTestId('workspace:action-menu-button')` finds nothing there. Use
`locator('[data-mark="…"]')` when reproducing by hand.

## The workflow canvas needs (almost) no drag and drop

The canvas is React plus xyflow, which looks unautomatable and is not. Every node renders as a
`group` with named buttons — `Settings`, `Delete`, `Add action`, and branch-specific variants
like `Add action — approved` or `Add action — body`. Edges carry accessible names
(`Edge from X to Y`) and their own `Insert action` / `Add filter` / `Delete connection` toolbar.
`Add action` opens `ua-node-picker-modal`, an ordinary searchable list of buttons.

So building a workflow is a sequence of clicks. `addActionFromNode()` covers the common case.

The one gesture with no button is **connecting two steps that already exist** — every `+`
creates a new step, so a merge (a branch rejoining a shared step) needs a real drag.
`connectHandles()` does it with `page.mouse`: handles are `.react-flow__handle.source` /
`.target`, named ones carry `data-handleid` (`true`, `false`, `approved`, `body`, `done`), and
the pointer must move in steps before xyflow starts a connection.

### Things that bite on the canvas

- **Save the step settings modal, or the step vanishes.** Picking an action adds the step
  provisionally and opens its settings; closing that modal without saving rolls the add back.
  Call `submitNodeSettings()` after every `addActionFromNode()`, and pick step types whose
  required settings have defaults (Delay, Run Script, Request Approval, If, While, Parallel)
  unless the spec fills the fields in.
- **The server does not keep seeded step ids.** `POST automations` assigns its own ids and
  rewires the connections, so the id a spec generated is not the one it gets back. Read the
  automation after creating it and look steps up with `AutomationApiHelper.stepByAlias()`;
  `addedSteps(before, after)` finds what the UI added.
- **A tier-1 workspace shows no actions in the picker.** Actions are scoped to what the
  workspace's service account may do, so with the empty service account the picker lists control
  flows only. Canvas specs that add actions need `automateServiceAccountWorkspace` even though
  they never run anything.
- **Filter the picker before clicking.** The list is taller than the modal, and a forced click on
  an entry below the fold fails with "Element is outside of the viewport" (`force` skips the
  scroll). `chooseActionInPicker` searches first, and `searchPicker` waits for the list to load,
  because a query typed while it is loading leaves "No items found".
- **Sidebar modals slide in.** A forced click during the animation fails with the same
  "outside of the viewport" error. `clickInModal()` waits for the control to be in the viewport.
- **Leaving an automation can raise `beforeunload`**, even without an edit. Playwright's default
  is to dismiss it, which cancels the navigation and hangs the next wait; the `umbracoAutomateUi`
  fixture accepts it instead.
- Assert on the saved model (connections, `sourceHandle`, `outcome`, positions) through the API,
  and use the canvas only for what the model cannot show (e.g. rendered nodes overlapping).

## Scope Actions-menu clicks

An entry like "Delete" exists in several places at once (the workspace Actions menu, canvas
nodes, membership rows), so an unscoped `getByRole('button', { name: 'Delete' })` fails on a
strict-mode violation. `clickAction()` scopes to `workspace:action-menu-button`.

## Automations need a workspace — use a fixture, in two tiers

A workspace is the container every automation belongs to, and the Automate sidebar menu is
gated behind at least one existing (`UA_WORKSPACES_EXIST_CONDITION_ALIAS`).

Inject one of two fixtures rather than building a workspace by hand:

| Fixture | Gives you | Use when |
| --- | --- | --- |
| `automateWorkspace` | A workspace with **no** service account (empty Guid key) | The default. Sidebar, tree, automation CRUD |
| `automateServiceAccountWorkspace` | A workspace plus a real `UserKind.Api` user in Administrators, and user groups | The spec **runs** an automation, or **saves the workspace from the UI** |

The second case is easy to miss: the workspace settings view marks Service Account Key and User
Groups as required, so a tier-1 workspace cannot be saved from the UI at all even though the API
created it happily.

Both create a uniquely-named workspace and tear it down afterwards. Playwright only sets up a
fixture a test injects, so specs that need neither pay nothing.

Why two tiers: `serviceAccountKey` is marked `[Required]` but is a non-nullable `Guid`, so
`Guid.Empty` passes validation, and `WorkspaceService.CreateWorkspaceAsync` adds no check of its
own — the client itself scaffolds new workspaces that way. The workspace is then perfectly
usable, right up until something tries to execute: `WorkspaceServiceAccountResolver` returns
null for the empty key, so there is no execution identity.

A service account is a **CMS user of kind Api**, so it is created through the CMS user API, not
Automate's — `ServiceAccountApiHelper` wraps `umbracoApi.user.createDefaultUser(..., 'Api')`.
The user group it lands in decides what the automation may do; tests use Administrators so
permissions are never the reason a spec fails.

### Things that bite

- **Create and delete both require an admin backoffice user** (`RequireAdmin`). The test user is
  admin, so this works out of the box — but a spec that switches user starts getting 403s.
  `WorkspaceAccessHandler` also lets admins bypass workspace membership, which is why the
  fixtures leave `userGroups` empty.
- **Workspaces have no alias uniqueness check** on the server (only workspace *groups* validate
  unique names), so a fixed alias quietly piles up duplicates whenever a run dies before
  teardown. `createForTest` adds a random suffix for exactly this reason — do not remove it.
- **Teardown deletes automations before the workspace.** It is not confirmed that deleting a
  workspace cascades to its automations. Verify that, then simplify `WorkspaceApiHelper.cleanUp`.

## Route Automate UI interaction through `AutomateUiHelper`

`lib/helpers/AutomateUiHelper.ts` is the single Page Object Model for the Automate backoffice.
When you author or edit a spec, Automate UI interactions must go through it — do not inline raw
locators or `page.*` calls for anything it covers. If a flow is missing, **add a method (and its
locators) to the helper** rather than hand-rolling it in a spec, so hardening stays in one place.

This also applies when driving the live demo site via Playwright MCP. MCP cannot `import` the
helper, but its locators are still the source of truth: read `AutomateUiHelper.ts` first and
reuse its selectors, so a manual repro matches what the specs do.

## The stale-bundle trap

The running demo site serves the **compiled bundle**, not your `src/`. After editing
`Umbraco.Automate.Web.StaticAssets/Client/src`, the tests validate whatever was last built.
Mass element-not-found failures that look like product bugs are usually this. Run a clean build
(`npm run build` at the repo root) before trusting a run, and re-run the full suite after any
rebuild.

Frontend installs happen at the **repo root**, not in the `Client` directory — this is an npm
workspaces monorepo, and installing in `Client` produces a spurious root lockfile diff. This
acceptance suite is deliberately **outside** the root workspaces, with its own lockfile, so
`npm ci` here is safe.

## Keep the helper package aligned to the CMS range

`package.json` must track `Umbraco.Cms.Core` in `Directory.Packages.props` (currently 18.1.0):
`@umbraco-cms/acceptance-test-helpers` and the `@umbraco-cms/backoffice` devDep are both pinned
`^18.1.0` so they float within the major. A stale pin can drift helper behaviour against the
running site. Bumping affects the **whole** suite — re-run everything after a bump.

## Playwright version pin

`@playwright/test` is pinned to `1.60.0`. Forms hit a CI hang on `1.56.1`: its Chromium build is
fetched from a legacy CDN path that stalls on hosted agents. Keep this at 1.60.0 or later.

## Selector & assertion hygiene

- **Do not assert on user-facing labels.** They get localised and break selectors. The
  deliberate exceptions are the section tab in `UiHelpers.goToAutomateSection`, which has no
  stable alternative (its label lives in `ConstantHelper.sections.automate`), and the outcome-exit
  specs, whose acceptance criteria are about the English exit labels. Those live in
  `ConstantHelper.outcomeLabels` (use `withDefault()` for the "(default)" mark) and
  `ConstantHelper.staleOutcomeError()`; never spell them out in a spec.
- Prefer per-property `expect()` over opaque boolean helpers. `AutomationApiHelper.getFullByName`
  exists so a failure pinpoints the exact field.
- Expect pointer interception on elements under a resizable `umb-split-panel` divider and on
  icons nested in `uui-button`s. A normal `.click()` silently no-ops and a later wait hangs. Use
  `{ force: true }`. It shows up only in the click-action error log, never in a snapshot.

## Connection types come from installed packages

There is no built-in connection type. Whatever provider packages the site has installed is what
the type picker offers, so a spec must not assume. The demo site ships Umbraco.Automate.Slack,
and Slack is normally **unconfigured** (no client id/secret in appsettings), which leaves
"Authenticate with Slack" disabled. A connection record still creates, renames and deletes
without credentials — that is the boundary of what `connection.crud.spec.ts` covers.
Authenticating a connection needs real OAuth and is out of scope.

What can be covered without credentials, `connection.test.spec.ts` does:

- **Test connection saves first (#347)** is asserted on the saved record, not the test result,
  which is always a failure for an unauthenticated Slack connection.
- **The popup-blocked fallback (#348)** runs by stubbing its two inputs rather than configuring
  Slack: `stubOAuthProviderConfigured()` answers the status endpoint with `isConfigured: true`,
  `blockPopups()` makes `window.open` return null, and `interceptOAuthChallenge()` fulfils the
  challenge navigation on the site's own origin so the tab never leaves and its sessionStorage
  nonce stays readable. Set all three up **before** navigating to the connection.

## Known product gaps the suite pins with `test.fixme`

None at present. Both earlier ones are fixed and now covered in `automation.run.spec.ts`: Run
now offered on a draft (#366), and step outputs missing from the Runs view (#376).

The collection-view Create buttons that used to be dead (no `api` or `kind` on the
`collectionAction` manifests) were fixed in #297 and are now covered; `collectionCreateButton`
is the locator.

## The session dies mid-run — navigate through `goToUrl`

The CMS rotates refresh tokens. When the shared `umbracoApi` helper hits an expired token it
performs a **full re-login**, which invalidates the token the browser page is holding, and the
next UI navigation lands on the login screen. A spec that calls the API before driving the UI
never notices; a spec that only drives the UI fails with a bare `waitFor` timeout and a page
snapshot showing the login form. That is how the Overview dashboard smoke test failed while
every test around it passed.

`UiHelpers.goToUrl` handles it: navigate, detect the login form, re-authenticate, navigate
again. **Route every UI navigation through it** rather than calling `page.goto` or
`AutomateUiHelper.goToUrl` directly from a spec.

Two details that make the detection work, both of which caught me out:

- The backoffice answers with a redirect chain, so the outcome is not decided at
  `domcontentloaded`. Race the login form against `section-links` before deciding.
- `isVisible()` is immediate and **ignores** a `timeout` option. Checking it straight after
  navigating always reported "not on the login screen", so the repair silently never ran.

## Local vs CI config diverges

`playwright.config.ts`: locally 30s timeout + 0 retries; CI 60s + 2 retries. A flake that only
bites in one place is usually this divergence, not a product bug.

## How the CI stage works

The `AcceptanceTests` stage in `azure-pipelines.yml`:

- **Depends on nothing (`dependsOn: []`), so it runs alongside `Build`.** It uses nothing
  `Build` produces, so waiting for it only cost ~3.5 min of wall-clock; the price is a wasted
  agent on the rare PR whose Build fails. It cannot depend on `Pack`, which is restricted to
  pushes on `vN/main`, `vN/dev`, `vN/hotfix/*` and `vN/release/*`, so the suite would never run
  on a pull request. The trade-off is that CI exercises **project references**, not a published
  package.
- **Is sharded across four parallel agents** (`strategy: parallel: 4`), each running
  `--shard=$(System.JobPositionInPhase)/$(System.TotalJobsInPhase)` against its **own** demo site
  and LocalDB. Specs share site data and the suite runs a single worker, so shards must not share a
  site. `fullyParallel: true` (with one worker) makes Playwright split by test rather than by file, so shards finish together; every shard runs `auth.setup.ts` itself. Each shard pays
  the full setup (build, scaffold, first boot), so extra shards only help while the tests
  themselves dominate; results and artifacts are named per shard.
- **Scaffolds the site with `scripts/install-demo-site.ps1`**, the same script developers run, so
  the CI leg and the local workflow cannot drift apart. No test site is committed.
- **Builds the frontend first.** Without `wwwroot` the Automate section silently fails to
  register and every UI spec fails with element-not-found.
- **Runs on `windows-latest` against SQL Server LocalDB, not SQLite.** It overrides the
  scaffold's connection string with `CONNECTIONSTRINGS__UMBRACODBDSN` and its provider name.
  Automate shares that connection (`UseNamedConnectionString = umbracoDbDSN`), so both move.
  The reason is a CMS deadlock on SQLite. The CMS cache-instruction sync job and an
  end-of-request cache-instruction insert can deadlock on `umbracoCacheInstruction`. With the
  CMS default `Cache=Shared`, the stuck write blocks every connection in the process, so
  `SQLite Error 6: 'database table is locked'` hits unrelated Automate and OpenIddict tables.
  The CMS retry policy then retries for ~9.5 min, and every spec in that window fails in fixture
  setup (build 289321). Forms moved its acceptance and integration legs to LocalDB for the same
  reason (umbraco/Forms#1420, #1537). **Do not move this back to SQLite to save time.** Local
  development stays on SQLite, where the stall is rare.
- **Windows step shells matter.** `script:` is cmd on Windows, where the first `npm` (a `.cmd`)
  ends a multi-line step, so multi-line npm steps use `bash:`. `npx playwright install` stays on
  `script:`, because under `pwsh` it hangs after the download (seen in Forms).
- **Stops the site with `taskkill /T /F` on `always()`.** The recorded PID is the `dotnet run`
  launcher. Killing only that orphans the site process, which keeps the log open and fails the
  upload.
- **Sets `CI: true` explicitly.** Azure does not set it, and the Playwright config keys its
  junit reporter, retries and timeouts off it — as does `postinstall.js`, which would otherwise
  try to run the interactive config prompt.
- It always publishes `results/` (traces, screenshots, video) and the demo site logs, named with
  the job attempt. A UI failure is rarely diagnosable from the error text alone. The condition is
  `always()`, as in Forms, because a job killed by its timeout is *cancelled*, and a cancelled job
  skips any step whose condition lacks `always()`, so the run that most needs diagnosing would
  upload nothing.
- **`maxFailures` stops a run that is going nowhere.** If the site wedges, every remaining spec
  fails in setup with three attempts each, which used to run into the job timeout.

Two things to know if you edit that stage. Azure macro-expands `$(name)` before bash or pwsh
sees the script. PowerShell subexpressions such as `$($process.Id)` are safe, because Azure only
matches a variable name after `$(`, but bash `$(command)` substitution is not. And
`config.js` is deliberately **not** used in CI: it is interactive and reads the per-worktree
port from git config, which is a local-development affordance, whereas CI fixes the URL via
`ASPNETCORE_URLS` and writes `.env` directly. The checkout needs `fetchDepth: 0`, like the
Build stage: the demo site builds from project references, and Nerdbank.GitVersioning fails
on a shallow clone.

`Pack` does **not** depend on this stage. Acceptance failures therefore do not block packaging;
wire that up only if you want UI flakes to be able to hold a release.
