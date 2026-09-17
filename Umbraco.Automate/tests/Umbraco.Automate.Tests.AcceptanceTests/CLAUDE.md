# Umbraco.Automate.Tests.AcceptanceTests

Playwright end-to-end tests that drive a **running** demo site
(`demos/vN/Umbraco.Automate.DemoSite`) through the backoffice, using
`@umbraco-cms/acceptance-test-helpers`. Specs live under `tests/DefaultConfig/`; auth is
bootstrapped by `tests/auth.setup.ts`. Commands are in [README.md](README.md).

Runs locally and in CI. The `AcceptanceTests` stage in `azure-pipelines.yml` depends on
`Build`, so it gates pull requests too — see "How the CI stage works" below.

---

## The demo site is not committed — that is deliberate

Forms has `examples/Umbraco.Forms.TestSite` checked in. Automate does not, and should not: the
whole `demos/` tree is gitignored and generated per-developer by `scripts/install-demo-site.*`.
Consequences for this suite:

- There is **no fixed URL**. The demo site binds a dynamic port and publishes its https address
  on a named pipe (`umbraco.demosite.<identifier>`), where the identifier is the worktree folder
  name in a worktree and the branch name otherwise. `config.js` reads that pipe.
- A demo site started from a **different branch or worktree** uses a different pipe name, so
  `npm run config` will not find it. Pass `--pipe <identifier>` in that case.
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

## The workflow canvas needs no drag and drop

The canvas is React plus xyflow, which looks unautomatable and is not. Every node renders as a
`group` with named buttons — `Settings`, `Delete`, `Add action`, and branch-specific variants
like `Add action — approved` or `Add action — body`. Edges carry accessible names
(`Edge from X to Y`) and their own `Insert action` / `Add filter` / `Delete connection` toolbar.
`Add action` opens `ua-node-picker-modal`, an ordinary searchable list of buttons.

So building a workflow is a sequence of clicks. `addActionFromNode()` covers the common case.

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

- **Do not assert on user-facing labels.** They get localised and break selectors. The one
  deliberate exception is the section tab in `UiHelpers.goToAutomateSection`, which has no
  stable alternative; its label lives in `ConstantHelper.sections.automate`.
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

## Known product bug: collection create buttons are dead

`UmbracoAutomate.CollectionAction.Connection.Create` and `...Workspace.Create` both fail at
runtime with "did not succeed creating an api class instance". Both manifests declare only
`element`, with no `api` and no `kind`; every CMS `collectionAction` supplies one or the other.
The visible symptom is that the Connections and Workspaces **collection views have no create
button** — you have to use the sidebar tree. Do not write a spec that clicks a create button in
a collection view until this is fixed.

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

- **Depends on `Build`, not `Pack`.** `Pack` is restricted to pushes on `vN/main`, `vN/dev`,
  `vN/hotfix/*` and `vN/release/*`, so depending on it would mean the suite never ran on a pull
  request — useless as a gate. The trade-off is that CI exercises **project references**, not a
  published package.
- **Scaffolds the site with `scripts/install-demo-site.sh`**, the same script developers run, so
  the CI leg and the local workflow cannot drift apart. No test site is committed.
- **Builds the frontend first.** Without `wwwroot` the Automate section silently fails to
  register and every UI spec fails with element-not-found.
- **Stays on `ubuntu-latest`.** The demo uses SQLite, so there is no LocalDB leg to add. Forms
  needs Windows for that; Automate does not.
- **Sets `CI: true` explicitly.** Azure does not set it, and the Playwright config keys its
  junit reporter, retries and timeouts off it — as does `postinstall.js`, which would otherwise
  try to run the interactive config prompt.
- On failure it publishes `results/` (traces, screenshots, video) and the demo site log. A UI
  failure is rarely diagnosable from the error text alone.

Two things to know if you edit that stage. Azure macro-expands `$(name)` before bash sees the
script, so use backticks for command substitution and `expr` rather than `$((...))`. And
`config.js` is deliberately **not** used in CI: it discovers a dynamic port from a named pipe,
which is a local-development affordance, whereas CI fixes the URL via `ASPNETCORE_URLS` and
writes `.env` directly.

`Pack` does **not** depend on this stage. Acceptance failures therefore do not block packaging;
wire that up only if you want UI flakes to be able to hold a release.
