# Architecture: Automate Analytics

An Analytics item in a new Monitoring menu opens its own workspace. Its Overview view draws Analytics UI elements from three new read-only endpoints. Those endpoints count runs and step runs with `GROUP BY` queries on two new indexes and one new column.

![Analytics flow](./diagrams/analytics-flow.svg)

## Decisions
- **Run time is a stored `DurationMs` column**, filled whenever a run is saved with a start and an end time, and backfilled by the migration. Neither database can work out a median from two date columns in the same EF query, and an average would be skewed by runs that wait days for approval.
- **Analytics gets its own Monitoring menu** between Automations and Settings, as Umbraco.AI does. In the Automations menu it would sit below a tree that can be long.
- **The workspace filter is a `uui-select`, not a picker.** It filters a screen rather than saving a value, the list is short, and Analytics UI's Workflow example filters the same way.
- **The server returns days only.** Week and month totals are summed in the browser with Analytics UI's `uauiBucketsFor`, which also follows each user's first day of the week.
- **A menu item opening a root workspace**, not a fourth dashboard tab. This is Umbraco.AI's Analytics pattern (a `menuItem` with an `entityType`, a `workspace`, and `workspaceView`s), so later views such as Approvals become new tabs without moving anything.
- **The workspace has its own element** that wraps `umb-workspace-editor`, rather than `kind: "default"`. The workspace filter and date range sit in its header, and every view shares them through `UaAnalyticsWorkspaceContext`.
- **Live `GROUP BY` queries with new indexes**, not a daily summary table (your call). Everything goes through `IAutomateAnalyticsService`, so if the speed test fails, a summary table can be swapped in under the same interface without touching the API or the UI.
- **The first build task is a speed test**: seed 500,000 runs and about 2 million step runs on SQL Server and time each endpoint against the target in SPEC. Nothing else gets built until it passes.
- **Workspace scoping joins on the automation's current workspace**, the same as `GetRunCountsByStatusAsync`, so Analytics matches the run list and the Overview. The run's own `WorkspaceId` snapshot is not used.
- **The repository returns UTC quarter-hour groups and the service turns them into the user's local days.** EF can translate `Year`, `Month`, `Day`, `Hour` and `Minute / 15` on both SQL Server and SQLite. Quarter hours cover every real time-zone offset, including +05:30 and +05:45. A 90-day range comes to at most about 35,000 small groups.
- **Rejected runs count as successes**, as `GetRunCountsByAutomationAsync` already does: someone saying no is an approval flow working. Rates are worked out over finished runs only. (SPEC R4)
- **A new controller under the existing `metrics` route**, reusing `RunSummaryMetricsController`'s workspace-scoping helper. That helper moves into `MetricsControllerBase` so both controllers share it.
- **Action names are looked up in the browser** from `UaCatalogueRepository`, as the run views do. The API returns aliases, so an action whose package has been removed still shows, by its alias.
- **Analytics UI comes from the shared import map**, not bundled. Vite's `external: [/^@umbraco/]` already covers `@umbraco/analytics-ui`. Until it's on nuget.org, use the MyGet `umbraconightly` build (`19.0.0-alpha.1`, from the feed `NuGet.config` already lists) and the npm `19.0.0-alpha.1` types.
- **The date range, comparison and workspace are saved per user in `localStorage`**, so the page opens where they left it. If storage fails, the page falls back to the last 30 days, compared with the period before, for all workspaces.

## What changes

**Server: `Umbraco.Automate/src/`**

```text
Umbraco.Automate.Core/
  Analytics/
    IAutomateAnalyticsService.cs        public: summary, automations, actions for a range
    AutomateAnalyticsService.cs         scoping, local days, compare, rates, median
    IAutomateAnalyticsRepository.cs     internal: grouped counts only
    AnalyticsQuery.cs                   range, compare, time zone, workspace ids
    AnalyticsSummary.cs                 totals, days, median, retained-from
    AutomationAnalyticsRow.cs           one automation's figures
    ActionAnalyticsRow.cs               one action's figures
  Configuration/
    UmbracoBuilderExtensions.Collections.cs   registers the service
Umbraco.Automate.Persistence/
  Analytics/
    EFCoreAutomateAnalyticsRepository.cs   GROUP BY queries, workspace join
  Configuration/
    UmbracoBuilderExtensions.cs         registers the repository
  Runs/
    AutomationRunEntity.cs              + DurationMs
    AutomationRunFactory.cs             fills DurationMs on save
  UmbracoAutomateDbContext.cs           + 2 indexes, DurationMs column
Umbraco.Automate.Persistence.SqlServer/
  Migrations/<stamp>_UmbracoAutomate_AddRunAnalytics.cs   column, backfill, indexes
Umbraco.Automate.Persistence.Sqlite/
  Migrations/<stamp>_UmbracoAutomate_AddRunAnalytics.cs   same, SQLite backfill SQL
Umbraco.Automate.Web/
  Api/Management/Metrics/
    Controllers/MetricsControllerBase.cs        + ResolveAccessibleWorkspacesAsync (moved)
    Controllers/RunSummaryMetricsController.cs  uses the base helper
    Controllers/AnalyticsMetricsController.cs   3 GET endpoints
    Models/Analytics*ResponseModel.cs           response shapes (SPEC)
Umbraco.Automate.Web.StaticAssets/
  Umbraco.Automate.Web.StaticAssets.csproj      + Umbraco.Analytics.UI [19.0.0, 19.999.999)
```

**Backoffice: `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/`**

```text
package.json                         + devDependency @umbraco/analytics-ui
src/
  manifests.ts                       + analyticsManifests
  api/                               regenerated client (generate-client)
  analytics/
    constants.ts                     aliases, entity type, storage key
    manifests.ts
    menu/manifests.ts                Monitoring menu + sidebar app + Analytics item
    workspace/
      manifests.ts                   workspace + Overview workspaceView
      analytics-workspace.element.ts header: workspace select, uaui-date-range
      analytics-workspace.context.ts filters, persisted per user
      views/
        analytics-overview-view.element.ts   tiles, chart, two tables
    repository/
      analytics.server.data-source.ts      calls the generated client
    analytics-adapter.ts             API answers to series, rows, week/month sums
  section/dashboard/
    automate-dashboard.element.ts    + Analytics link under the status cards
  lang/en.ts                         + uaAnalytics_* keys, uaMenu_monitoring
```

- **Database:** one migration per provider. It adds the column, fills it in from `StartedUtc` and `CompletedUtc` with plain SQL (`DATEDIFF_BIG` on SQL Server, `julianday` on SQLite), and adds the two indexes.
- **Permissions:** the existing Automate section policy on `UmbracoAutomateManagementControllerBase`, plus workspace scoping. No new permission.

Not touched: Deploy and uSync (runs aren't synced between environments), search index (runs aren't indexed), cache refreshers (nothing is cached: every load queries the database, which keeps load-balanced servers in step), notifications (read-only feature), version history (runs aren't versioned), OpenTelemetry metrics in `AutomateMetrics` (in memory, not used here).

## Data model

No new tables. Two existing tables change.

**`umbracoAutomateRun`**

| Column / index | Type | Change |
|---|---|---|
| `DurationMs` | `bigint` null | New. `CompletedUtc − StartedUtc` in ms, null until the run ends |
| `IX_..._StartedUtc` | `(StartedUtc)` include `AutomationId, Status, DurationMs` | New |

**`umbracoAutomateStepRun`**

| Column / index | Type | Change |
|---|---|---|
| `IX_..._StartedUtc` | `(StartedUtc)` include `RunId, ActionAlias, Status, RetryCount, DurationTicks, ErrorCategory` | New |

- **Included columns** are SQL Server only (`IncludeProperties`). SQLite ignores them and uses the plain index on `StartedUtc`, which is fine for development sites. The speed target only applies to SQL Server.
- **Step runs are filtered by their own `StartedUtc`**, not their run's, so the action query doesn't need the run table for dates. It joins runs and automations only to scope by workspace, and an admin's "All workspaces" skips the join.
- **Run cleanup** deletes rows as it does now. The indexes add a little to each delete, which the speed test also measures.
