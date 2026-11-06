# Plan: Survive the installer restart without a connection string

**8 tasks in 5 areas.** T1 comes first, then T2 and T3 can run in parallel (group B). T4 and T5 follow both of them, T6 and T7 follow T5, and T8 runs in the release PR.

## Core

- [x] **T1** - story: S1 (AC1-AC10), S2 (AC1). Add `PlaceholderConnectionState<TDbContext>` and `ResolvingPooledDbContextFactory<TDbContext>` in `Umbraco.Automate/src/Umbraco.Automate.Core/Persistence/Scoping/`, both internal.
  - **Factory:** while the state says the DI pool was built on the placeholder, it calls `DatabaseConnectionInfo.Resolve(IOptionsMonitor<ConnectionStrings>, IConfiguration)` on each create. On success it builds the real `DbContextOptions<TDbContext>` with the caller's provider-setup delegate and wraps them in `new PooledDbContextFactory<TDbContext>(options, 1024)`. That happens once, under a lock, with a volatile field. It logs the caller's "found" line once, then uses the real pool from then on. When `Resolve` still throws, it returns a context from the DI pool. When the state says the DI pool is real, it returns the DI pool's context after one flag check.
  - **Shared helper:** `AddAutomatePooledDbContext<TDbContext>(this IServiceCollection, ...)` in `AmbientDbContextFactoryExtensions.cs` takes:
    - the provider-setup delegate `(IServiceProvider, DbContextOptionsBuilder, string connectionString, string providerName)`,
    - the enlisted-context builder,
    - the placeholder warning text, the "found" text and a logger category.

    It calls `AddUmbracoDbContext<TDbContext>(..., shareUmbracoConnection: false)` with one shared options delegate. That delegate resolves the connection or falls back to `DatabaseConnectionInfo.PlaceholderConnection`, logs the warning, sets the state flag, and then calls the provider setup. The helper then calls `EnlistDbContextFactoryInAmbientScope`, which now wraps the resolved DI pool in `ResolvingPooledDbContextFactory` before handing it to `DetachedDbContextFactory`.
  - Update `DatabaseConnectionInfo.PlaceholderConnection`'s remarks so they no longer say "restart into Run".

  Acceptance: Core builds. `AmbientDbContextFactoryCompositionTests` stays green (nothing calls the new helper yet).
  depends-on: none

## Persistence and OpenIddict

- [x] **T2** - **wire: core's DbContext into the shared helper.** story: S1 (AC1-AC10), S2 (AC1, AC2), S3 (AC1, AC2).
  - `AddUmbracoAutomateDbContext` (`Umbraco.Automate/src/Umbraco.Automate.Persistence/Configuration/UmbracoBuilderExtensions.cs`) calls `AddAutomatePooledDbContext<UmbracoAutomateDbContext>`.
  - `AutomatePooledDbContextOptions` shrinks to the provider setup (`UmbracoAutomateDbContext.ConfigureProvider` plus `AutomateReadinessInterceptor`). Its resolve-or-fallback copy and the old warning go.
  - The S3 warning text and the S1 AC6 "found" text are passed in.

  Acceptance: the S1, S2 and S3 specs in `ResolvingPooledDbContextFactoryTests` go green against the real `AddUmbracoAutomateDbContext` registration, and the full `Umbraco.Automate.slnx` test run is green.
  depends-on: T1. parallel-group: B

- [x] **T3** - **wire: OpenIddict's DbContext into the shared helper.** story: S4 (AC1-AC4).
  - `AddOpenIddictDbContext` (`Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict.Core/Configuration/UmbracoBuilderExtensions.cs`) calls `AddAutomatePooledDbContext<OpenIddictDbContext>`.
  - `OpenIddictPooledDbContextOptions` shrinks to `OpenIddictDbContext.ConfigureProvider`, and its fallback copy goes. This closes #532's duplicate.
  - `PooledDbContextOptionsTests` follows the shared helper.

  Acceptance: the S4 specs go green, and the full `Umbraco.Automate.OpenIddict.slnx` test run is green.
  depends-on: T1. parallel-group: B

## Docs

- [x] **T4** - story: S3 (AC1). Update the "Persistence registration" paragraph in `docs/engineering-spec.md`: the factory picks up a connection string that appears after boot, so the placeholder is temporary.
  Acceptance: the paragraph matches the code after T2 and T3.
  depends-on: T2, T3

## Smoke

- [x] **T5** - **wire: the fix into a fresh backoffice install.** story: S5 (AC1-AC4). Run the brief's Repro on a scratch demo with no connection strings, `UseNamedConnectionString: umbracoDbDSN` and unattended install off. Use an explicit `--urls` port, and check the listener's exe path is this worktree's. Complete the installer with SQLite. Then, without restarting, log in, create, publish and run a Manual automation with a Log Message step. Check the log for OAuth cleanup errors five minutes after boot.
  Acceptance: all four S5 criteria hold in one process, and the log has no "no such table" errors after `RestartRuntimeStep`.
  depends-on: T2, T3

## Follow-ups and port

- [x] **T6** - story: S5. Draft a follow-up issue: `WorkflowHostLifecycle` lets a stuck-run recovery exception stop the host (`WorkflowHostLifecycle.cs:46`). It should log and carry on. The draft is for Rick to approve before filing.
  Acceptance: the issue text is drafted, and it's listed in the PR's "Left out".
  depends-on: T5

- [x] **T7** - story: S1-S5. Port the merged v18 PR to `v17/dev` with `change-backport`. Same code on v17: core fallback at `UmbracoBuilderExtensions.cs:187`, OpenIddict at `:189`, CMS 17.3.4's `RestartAsync` identical. Run the same specs. Re-run T5's smoke on a v17 demo if one can be set up; otherwise the PR says it wasn't run.
  Acceptance: a v17 PR is open with the specs and full suites green.
  depends-on: T5

- [ ] **T8** - story: S4. **In the release PR that ships this fix:** raise OpenIddict's `Umbraco.Automate.Core` floor in `Directory.Packages.props` (today `[18.5.1, 18.999.999)`) to the first Core version containing `AddAutomatePooledDbContext`. OpenIddict now calls it, so the next OpenIddict on an older Core would fail at startup with `MissingMethodException`. Do the same on the v17 line for T7's port. Not done in this PR, because nightly Core builds sit below the next minor (see ARCHITECTURE "Decisions").
  Acceptance: the release PR raises the floor, and the PR for this feature lists it under "Left out".
  depends-on: T7
