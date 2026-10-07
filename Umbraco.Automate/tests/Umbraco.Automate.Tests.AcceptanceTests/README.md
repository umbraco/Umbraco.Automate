# Umbraco.Automate Acceptance Tests

Playwright end-to-end tests that drive a **running** Umbraco site with Umbraco.Automate
installed, through the backoffice.

Runs locally, and in CI via the `AcceptanceTests` stage in `azure-pipelines.yml`. That stage
runs alongside `Build` and gates pull requests as well as pushes.

## Prerequisites

- Node 24.13+ (the version in `.nvmrc`)
- A running demo site. Start one with the `/demo-site-management` skill, or create one first
  with `scripts/install-demo-site.ps1` / `.sh`.

## Getting started

From this folder:

```bash
npm ci
npx playwright install chromium
npm run config
```

`npm run config` finds the demo site automatically. Each worktree's demo site gets its own
stable port, stored in that worktree's git config (`git config --worktree --get wdp.port`), so
there is nothing to type once the site has started at least once. It writes `.env`, which is
gitignored.

If the demo site is running from a different worktree than the one you are in, point config at
it by path, or give the port directly:

```bash
npm run config -- --worktree <path-to-other-worktree>
npm run config -- --port 44381
```

The default backoffice credentials for a generated demo site are `admin@example.com` /
`password1234`.

## Running tests

```bash
npm test          # headless
npm run ui        # Playwright UI mode
npm run smokeTest # only the @smoke tagged tests
```

Single file or single test:

```bash
npx playwright test tests/DefaultConfig/Smoke/automate.section.spec.ts --headed
npx playwright test -g "loads the Automate section"
```

## Layout

| Path | Purpose |
| --- | --- |
| `tests/auth.setup.ts` | Logs in once and saves storage state for every spec |
| `tests/DefaultConfig/` | The specs |
| `lib/helpers/AutomateUiHelper.ts` | Page Object Model for the Automate backoffice |
| `lib/helpers/AutomationApiHelper.ts` | Automate management API — automations, publish, runs |
| `lib/helpers/AutomationBuilder.ts` | `automationStep` / `automationConnection` / `manualTrigger` for seeding a graph |
| `lib/helpers/CatalogueApiHelper.ts` | The installed step types — names and output descriptions specs read instead of hard-coding |
| `lib/helpers/WorkspaceApiHelper.ts` | Automate management API — workspaces |
| `lib/helpers/ConnectionApiHelper.ts` | Automate management API — connections |
| `lib/helpers/ServiceAccountApiHelper.ts` | Service accounts (CMS users of kind Api) |
| `lib/helpers/testExtension.ts` | The Playwright fixtures |

## What is covered

| Spec | Covers |
| --- | --- |
| `Smoke/automate.section.spec.ts` | Section loads, dashboard renders, management API answers |
| `Workspaces/workspace.fixtures.spec.ts` | Both workspace fixture tiers |
| `Workspaces/workspace.management.spec.ts` | Workspace list, rename, delete, service account display, collection Create button (#297), editor back arrow |
| `Automations/automation.lifecycle.spec.ts` | Automation create, rename, delete |
| `Automations/automation.canvas.spec.ts` | Branches rejoining and publishing (#323), Approval rejected branch, placement beside the clicked output (#322), insert between on a connected output (#346), inserting a container (#324), unavailable actions (#325) |
| `Automations/automation.binding-picker.spec.ts` | Predecessors in flow order (#299), expression descriptions (#307), readable value types, inserting an expression |
| `Automations/automation.run-view.spec.ts` | Keyboard access to runs, steps and the trigger row, the Logs tab, the failing step's error as the run's error, whole-millisecond step durations, Replay disabled once unpublished |
| `Automations/automation.run.spec.ts` | Publish, Run now, Runs view, Run Script reading upstream `data` (#345), Run now hidden on a draft (#366), step output in the run view (#376) |
| `Connections/connection.crud.spec.ts` | Connection create, rename, collection listing, collection Create button (#297) |
| `Connections/connection.test.spec.ts` | Test connection saves unsaved edits (#347), popup-blocked same-tab OAuth fallback (#348, stubbed) |

## Fixtures

Inject these into a test and they set themselves up and tear themselves down.

| Fixture | What you get |
| --- | --- |
| `umbracoAutomateApi` | Automate's own management API helpers |
| `umbracoAutomateUi` | The Automate page object, plus `goToAutomateSection()` |
| `automateWorkspace` | A workspace with no service account. The default choice |
| `automateServiceAccountWorkspace` | A workspace plus a real service account, for specs that run an automation |

```ts
test('does something in a workspace', async ({ automateWorkspace, umbracoAutomateApi }) => {
  // automateWorkspace.id is ready to use, and is deleted after the test
});
```

CMS-side setup (documents, doc types, users, data types) goes through the `umbracoApi` and
`umbracoUi` fixtures from `@umbraco-cms/acceptance-test-helpers`. Only Automate's own endpoints
and elements live in `lib/`.

See [CLAUDE.md](CLAUDE.md) for the traps, and the Playwright
[documentation](https://playwright.dev/docs/intro) for everything else.
