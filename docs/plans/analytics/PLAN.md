# Plan: Automate Analytics

20 tasks in 6 areas. The speed test (T4) gates everything after it, as ARCHITECTURE decides: if it fails, stop and re-run `feature-design` for the daily summary table. Paths are relative to `Umbraco.Automate/src/` unless they start with `Umbraco.Automate/tests/`. Spec files are listed under each task's Acceptance, and every one starts pending:

- **C# specs** are `[Fact(Skip = "Pending")]`, and each file is wrapped in `#if AUTOMATE_ANALYTICS`, so the test projects compile before the types exist. The task that adds a file's types removes its `#if` and `#endif` lines, then removes `Skip` from each spec it makes pass. The specs are written against the planned names (`IAutomateAnalyticsRepository.GetRunCountGroupsAsync`, `AnalyticsQuery`, `AnalyticsQueryStatus` and the rest). A builder that picks a better name renames it in the spec as well and logs it in `BUILD-LOG.md`. The shared helpers are `Analytics/AnalyticsDatabase.cs` (integration) and `Analytics/AnalyticsServiceHarness.cs` (unit).
- **Acceptance specs** are `test.skip` in `Umbraco.Automate/tests/Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/Analytics/`. They reach the page through `AutomateUiHelper` (`analyticsUrl()` and an `analytics` page object), which the frontend tasks add as they go, following that suite's `CLAUDE.md`.

## Data

- [x] **T1** - story: AN-2 (AC6, AC7). Add `DurationMs` (`long?`) to `Umbraco.Automate.Persistence/Runs/AutomationRunEntity.cs`. `AutomationRunFactory` sets it to `CompletedUtc − StartedUtc` in whole milliseconds whenever both are set, and to null otherwise, so every place that sets `CompletedUtc` (`RunFinalizer`, `AutomationExecutor`, `AutomationRunService`) is covered without being touched. In `UmbracoAutomateDbContext`, map the column and add the two indexes from ARCHITECTURE's Data model: `umbracoAutomateRun (StartedUtc)` including `AutomationId, Status, DurationMs`, and `umbracoAutomateStepRun (StartedUtc)` including `RunId, ActionAlias, Status, RetryCount, DurationTicks, ErrorCategory` (`IncludeProperties`, SQL Server only). Add migration `UmbracoAutomate_AddRunAnalytics` to both `Umbraco.Automate.Persistence.SqlServer` and `Umbraco.Automate.Persistence.Sqlite`, each backfilling finished runs with plain SQL (`DATEDIFF_BIG(millisecond, StartedUtc, CompletedUtc)` on SQL Server, `CAST((julianday(CompletedUtc) - julianday(StartedUtc)) * 86400000 AS INTEGER)` on SQLite).
  Acceptance: `Analytics/RunDurationTests` (AN-2 AC6, AC7) green in `Umbraco.Automate/tests/Umbraco.Automate.Tests.Integration`. Both migration projects report no pending model changes.
  depends-on: none

- [x] **T2** - story: AN-2 (AC1, AC4, AC5, AC8, AC12), AN-3 (AC2), AN-4 (AC1, AC2, AC7), AN-6 (AC1 to AC4). Create the internal `Umbraco.Automate.Core/Analytics/IAutomateAnalyticsRepository.cs` and `Umbraco.Automate.Persistence/Analytics/EFCoreAutomateAnalyticsRepository.cs`, with the run queries only:
  - run counts grouped by UTC quarter hour (`Year`, `Month`, `Day`, `Hour`, `Minute / 15`) and status, for a UTC window
  - the median `DurationMs` of finished runs (a count, then `OrderBy` and `Skip`/`Take`)
  - the number of Suspended runs right now
  - per-automation aggregates: runs, completed plus rejected, failed, finished, average `DurationMs`, latest `StartedUtc`, with the automation's name and workspace id

  Each query takes a nullable workspace-id set. With a set, it joins `umbracoAutomateAutomation` on the current `WorkspaceId`, as `GetRunCountsByStatusAsync` does. Null means all workspaces and skips the join. Runs with no `StartedUtc` are left out. Every query groups in SQL and never loads run rows.
  Acceptance: `Analytics/AnalyticsRunQueryTests` green against SQLite in `Umbraco.Automate.Tests.Integration`, including a deleted automation (AN-4 AC7) and an automation that moved workspace (AN-6 AC4).
  depends-on: T1

- [x] **T3** - story: AN-5 (AC1 to AC7). Add the step-run query to the same repository: per `ActionAlias`, for step runs whose own `StartedUtc` is in the UTC window, return runs (any status except Skipped), completed, failed, rejected, the sum of `RetryCount`, the average `DurationTicks` of finished step runs, and failed counts per `ErrorCategory`. It uses the same workspace-join rule as T2.
  Acceptance: `Analytics/AnalyticsActionQueryTests` green against SQLite.
  depends-on: T2

- [x] **T4** - story: AN-1 (AC1 to AC3). Add the opt-in speed test `Analytics/AnalyticsSpeedTests` to `Umbraco.Automate.Tests.Integration`, with `[Trait("Category", "Performance")]`. It's skipped unless the `AUTOMATE_PERF_SQLSERVER` connection string is set, so CI never runs it. It:
  1. creates the database and applies the SQL Server migrations
  2. bulk-loads 1,000,000 runs and about 4,000,000 step runs over 180 days with `SqlBulkCopy` (STORIES AN-1)
  3. calls each repository query set once to warm up, then times the second call for a 90-day range plus its comparison
  Acceptance: all three timings under 2 seconds on LocalDB, recorded in `BUILD-LOG.md`. If any misses, stop: nothing after this task starts.
  depends-on: T3

## Core

- [x] **T5** - story: AN-2 (AC1 to AC5, AC8, AC11, AC12), AN-3 (AC1 to AC5), AN-7 (AC6, AC8 to AC12). Add the public `IAutomateAnalyticsService` with the summary method, plus `AnalyticsQuery` and `AnalyticsSummary`, in `Umbraco.Automate.Core/Analytics/`. Its jobs:
  - validate the query (R10) and return an Attempt-style failure for each R10 case
  - turn local dates into a UTC window with `TimeZoneInfo.FindSystemTimeZoneById`
  - fold the quarter-hour groups into the user's days, every day included, zeros too
  - work out the success rate (R4) and the median
  - read `waitingForApproval`
  - set `retainedFrom` from `RunCleanupPolicy.RetentionDays` (null when 0 or less)
  - return a null comparison when it starts before `retainedFrom` (R7)
  Acceptance: `Analytics/AnalyticsSummaryTests` green in `Umbraco.Automate.Tests.Unit`, with the repository mocked.
  depends-on: T4. parallel-group: B

- [x] **T6** - story: AN-4 (AC1 to AC3), AN-5 (AC1 to AC6). Add the automations and actions methods to the same service, with `AutomationAnalyticsRow` and `ActionAnalyticsRow`:
  - rates per R4 and R8, averages per R5
  - workspace names looked up through `IWorkspaceService` for the ids T2 returns
  - the most common error per R9 (most failures, lower enum value on a tie, null with none)
  - `compareItems` null per R7
  Acceptance: `Analytics/AnalyticsRowTests` green in `Umbraco.Automate.Tests.Unit`.
  depends-on: T5

- [x] **T7** - story: AN-6 (AC1 to AC3, AC6). Move `ResolveAccessibleWorkspacesAsync` from `RunSummaryMetricsController` into `MetricsControllerBase` as a protected helper, and make `RunSummaryMetricsController` use it. No behaviour change.
  Acceptance: the existing `Metrics/RunSummaryMetricsControllerTests` stay green, unchanged.
  depends-on: T4. parallel-group: B

## Management API

- [x] **T8** - story: AN-6 (AC1 to AC3, AC6), AN-7 (AC9 to AC12). Add `Umbraco.Automate.Web/Api/Management/Metrics/Controllers/AnalyticsMetricsController.cs` under route `metrics/analytics`, with `GET summary`, `GET automations` and `GET actions` taking SPEC's query. It scopes through the base helper and maps the service's validation failures to 400 `ProblemDetails`. Add the response models `Models/AnalyticsSummaryResponseModel.cs`, `AnalyticsAutomationsResponseModel.cs` and `AnalyticsActionsResponseModel.cs` in SPEC's exact shapes, with `topErrorCategory` as the enum name.
  Acceptance: `Analytics/AnalyticsMetricsControllerTests` green in `Umbraco.Automate.Tests.Unit`.
  depends-on: T6, T7

- [x] **T9** - **wire: analytics into the Management API.** story: AN-1 (AC1 to AC3), AN-8 (AC6). Register `IAutomateAnalyticsService` in `Umbraco.Automate.Core/Configuration/UmbracoBuilderExtensions.Collections.cs` and `IAutomateAnalyticsRepository` in `Umbraco.Automate.Persistence/Configuration/UmbracoBuilderExtensions.cs`, as singletons like the run service and repository.
  Acceptance:
  - on the v19 demo site, real backoffice-token requests to all three endpoints return 200 JSON in SPEC's shapes
  - a user without the Automate section gets 403
  - Swagger's Metrics group lists the three routes
  - with the demo site pointed at T4's seeded LocalDB database, each endpoint's second call is under 2 seconds (AN-1, timings in `BUILD-LOG.md`)
  depends-on: T8

## Backoffice

- [x] **T10** - story: AN-3 (AC9). Add the Analytics UI dependency:
  - `Umbraco.Analytics.UI` in the root `Directory.Packages.props` and as a `PackageReference` in `Umbraco.Automate.Web.StaticAssets/Umbraco.Automate.Web.StaticAssets.csproj`, pinned to MyGet nightly `19.0.0-alpha.1`. The `[19.0.0, 19.999.999)` range can't admit a prerelease below 19.0.0; T20 swaps it.
  - `@umbraco/analytics-ui` as a devDependency in `Client/package.json`, by the MyGet tarball URL of `19.0.0-alpha.1` (`https://www.myget.org/F/umbraconightly/npm/@umbraco/analytics-ui/-/@umbraco/analytics-ui-19.0.0-alpha.1.tgz`). Don't add an `.npmrc` scope: it would send every `@umbraco/` package to MyGet.
  - run `npm install` at the repo root, since this is an npm workspace
  Vite's existing `external: [/^@umbraco/]` already covers the import.
  Acceptance: `dotnet build` and the frontend build are green. On the demo site, the backoffice import map contains `@umbraco/analytics-ui`, and its module loads.
  depends-on: T4. parallel-group: B

- [x] **T11** - story: AN-8 (AC1, AC2, AC4). Create the shell under `Client/src/analytics/`:
  - `constants.ts`: aliases, the entity type, the storage key
  - `menu/manifests.ts`: a Monitoring `sectionSidebarApp` with the same workspaces-exist condition as the Automations one, its `menu`, and the Analytics `menuItem` (icon `icon-chart`, `entityType` set)
  - `workspace/manifests.ts`: a `workspace` with its own element, plus the Overview `workspaceView`
  - `workspace/analytics-workspace.element.ts`: wraps `umb-workspace-editor` with headline Analytics
  - `workspace/views/analytics-overview-view.element.ts`: an empty view for now
  - `manifests.ts`: exports them
  Also register them in `src/manifests.ts`, and add `uaMenu_monitoring` and the first `uaAnalytics_*` keys to `lang/en.ts`. In the acceptance suite, add `analyticsUrl()`, `analytics.menuItem`, `analytics.monitoringMenu` and `analytics.overviewLink` to `AutomateUiHelper`.
  Acceptance: `Analytics/analytics.navigation.spec.ts` AN-8 AC1, AC2 and AC4 green in `Umbraco.Automate/tests/Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/`.
  depends-on: T10. parallel-group: C

- [x] **T12** - story: AN-2 (AC9). Regenerate the API client (`npm run generate-client`) against the T9 demo site, so `Client/src/api/` has the three endpoints and their types.
  Acceptance: the generated types match SPEC's shapes and the frontend build is green.
  depends-on: T9, T10. parallel-group: C

- [ ] **T13** - story: AN-6 (AC5, AC7), AN-7 (AC1, AC2, AC4, AC13). Add `workspace/analytics-workspace.context.ts` (`UaAnalyticsWorkspaceContext`):
  - holds the range, compare, workspace id and the browser's IANA time zone
  - stored in `localStorage` under a key that includes the current user's key, with every read and write in try/catch
  - defaults to the last 30 days vs the previous period, all workspaces
  - drops a stored workspace the user can no longer access
  Fill the workspace element's header:
  - a `uui-select` of accessible workspaces from the existing workspace collection data source, with All workspaces first, hidden when there's only one
  - `uaui-date-range` with presets `last-7-days`, `last-30-days`, a custom `last-90-days` (its own `range` and label) and `custom`, and `maxDate` set to today
  Add the `dateRange`, `workspaceSelect`, `pickPreset`, `pickCustomRange` and `turnComparisonOff` members to `AutomateUiHelper.analytics` first. Acceptance: `Analytics/analytics.filters.spec.ts` (the AN-6 and AN-7 criteria above) green.
  depends-on: T11

- [ ] **T14** - story: AN-3 (AC6 to AC8), AN-5 (AC8 to AC11). Add:
  - `repository/analytics.server.data-source.ts`: the three generated-client calls, each keeping a request counter so an older answer is dropped
  - `analytics-adapter.ts`, which turns the summary into `UauiSeries` (Completed `positive`, Rejected `warning`, Cancelled `default`, Failed `danger`, In progress slot 1, in that order) and its comparison series
  - week and month totals in the adapter, from `uauiBucketsFor` with `weekStart` from `new Intl.Locale(culture).getWeekInfo().firstDay`
  - table rows from the API rows, with action names from `UaCatalogueRepository` (alias when missing), `Unknown` shown as Other, and null shown as None
  Acceptance: frontend build green. The listed criteria go green through T15 and T17's acceptance specs, which use this code.
  depends-on: T12, T13

- [ ] **T15** - story: AN-2 (AC9 to AC11), AN-3 (AC6 to AC10), AN-7 (AC3, AC5, AC7). Build the top of the Overview view to match `mockups/analytics.png`:
  - five `uaui-kpi-tile`s (Runs, Success rate with `delta-unit="points"`, Failed runs and Median run time with `lower-is-better`, Waiting for approval as an `href` to the Approvals dashboard)
  - the retention info line (R7)
  - "Runs over time" with `uaui-group-by` (day, week, month) and a stacked-bar `uaui-time-series-chart` with the comparison
  - every box's loading, error with Retry, and empty states
  Add `tile(id)` (by a stable id such as `runs`, never its label), `chart`, `chartBox`, `groupBy` and `retentionNotice` to `AutomateUiHelper.analytics` first. Acceptance: `Analytics/analytics.summary.spec.ts` green.
  depends-on: T14

- [ ] **T16** - story: AN-4 (AC4 to AC6, AC8). Add the Automations box to the view: `uaui-data-table` in client mode with a search field, compare rows, and columns:
  - name as a link cell to `UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN`, with the workspace name as its second line
  - runs as a share cell
  - success rate as a percent cell in points
  - failed and average run time as number cells, `lowerIsBetter`
  - last run as a date cell
  Add `automationsTable` and `automationsSearch` to `AutomateUiHelper.analytics` first. Acceptance: `Analytics/analytics.automations.spec.ts` green.
  depends-on: T15

- [ ] **T17** - story: AN-5 (AC8 to AC11). Add the Actions box to the view, in the same pattern: action name, runs (share), failure rate (percent, points, `lowerIsBetter`), retries, average run time (`lowerIsBetter`), most common error.
  Each actions row carries `data-alias` and `data-top-error` (`none`, `other` or the category name) so specs don't depend on labels. Add `actionsTable` and `actionRow(alias)` to `AutomateUiHelper.analytics` first. Acceptance: `Analytics/analytics.actions.spec.ts` green.
  depends-on: T16

- [ ] **T18** - story: AN-8 (AC3, AC5). In `section/dashboard/automate-dashboard.element.ts`, add the line under the status cards that links to Analytics (`mockups/overview.png`). It's left out in both welcome states. Add its `lang/en.ts` key.
  Acceptance: `Analytics/analytics.navigation.spec.ts` AN-8 AC3 and AC5 green.
  depends-on: T17

- [ ] **T19** - **wire: Analytics into the backoffice.** story: AN-8 (AC1 to AC3). End to end on the v19 demo site with real runs:
  - open Analytics from the sidebar and from Overview
  - every box loads real data
  - the screen matches the mockups
  - axe passes in the light, dark and high-contrast themes (SPEC Targets)
  Acceptance: every `Analytics/*.spec.ts` acceptance spec green together, and a smoke test with no findings left open.
  depends-on: T17, T18

## Release

- [ ] **T20** - story: AN-3 (AC9). When `Umbraco.Analytics.UI` 19.0.0 is on nuget.org and `@umbraco/analytics-ui` 19.0.0 is on npm, replace T10's nightly pins with `[19.0.0, 19.999.999)` and `^19.0.0`, then run `npm install` at the repo root.
  Acceptance: build green from public feeds only, and the T19 acceptance specs still green.
  depends-on: T19, and the external Analytics UI 19.0.0 release
