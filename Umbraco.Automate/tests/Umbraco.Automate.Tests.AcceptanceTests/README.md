# Umbraco.Automate Acceptance Tests

Playwright end-to-end tests that drive a **running** Umbraco site with Umbraco.Automate
installed, through the backoffice.

Runs locally, and in CI via the `AcceptanceTests` stage in `azure-pipelines.yml`. That stage
depends on `Build`, so it gates pull requests as well as pushes.

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
| `lib/helpers/AutomationApiHelper.ts` | Automate management API — automations |
| `lib/helpers/WorkspaceApiHelper.ts` | Automate management API — workspaces |
| `lib/helpers/ConnectionApiHelper.ts` | Automate management API — connections |
| `lib/helpers/ServiceAccountApiHelper.ts` | Service accounts (CMS users of kind Api) |
| `lib/helpers/testExtension.ts` | The Playwright fixtures |

## What is covered

| Spec | Covers |
| --- | --- |
| `Smoke/automate.section.spec.ts` | Section loads, dashboard renders, management API answers |
| `Workspaces/workspace.fixtures.spec.ts` | Both workspace fixture tiers |
| `Workspaces/workspace.management.spec.ts` | Workspace list, rename, delete, service account display |
| `Automations/automation.lifecycle.spec.ts` | Automation create, rename, delete |
| `Connections/connection.crud.spec.ts` | Connection create, rename, collection listing |

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
