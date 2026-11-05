# Decision log

## 05-11-2026: Brief

- **Fix the database connection, not just the warning.** Proven that a fresh install stops the site and breaks login. Rewording the warning alone leaves that in place.
- **No forced process restart after install.** Under Kestrel or `dotnet run`, nothing brings the site back.
- **#532 folds in.** Its shared fallback code is the same code this fix needs, so the two copies in core and OpenIddict become one. Agreed by Rick on 05-11-2026.
- **Brief approved as written** (Rick, 05-11-2026). One v18 PR, then one v17 PR, covering core and OpenIddict. OpenIddict's minimum core version goes up in the next core minor. `WorkflowHostLifecycle` hardening and anything that captures the connection outside the factory become follow-up issues. The upgrade path is out of scope.
- **Design (feature-design):** the fix goes in `EnlistDbContextFactoryInAmbientScope`, because every consumer gets its factory there. It wraps DI's pool and builds a second pool only when the first was built on the placeholder, so sites with a connection string at boot keep today's path. Rejected: a wrapper-owned pool for every site, which changes the hot path for everyone; inferring the placeholder from the built options, which depends on EF Core internals.
- **The approach is already agreed** for `feature-design` to work out: a lazy factory wrapper that keeps resolving the connection string until it gets a real one. Agreed by Rick on 05-11-2026, after the #531 repro.
- **Plan (feature-plan):**
  - T1 lands the shared seam before anything calls it, so T2 (core) and T3 (OpenIddict) can be built and reviewed in parallel without touching each other's files.
  - The installer smoke (T5) runs after both wires, because the repro needs core and OpenIddict fixed to pass.
  - `docs/engineering-spec.md` joined the design's "What changes" here, because the #561 paragraph would otherwise describe the placeholder as permanent.
- **Specs live in the feature worktree, not on trunk.** The main checkout holds another session's uncommitted work, so writing test files there could break that session's builds. The plan folder moves into the worktree with them and becomes the branch's first commit.

## 05-11-2026: Build

- **T1: the wrapper is opt-in through an optional settings argument on `EnlistDbContextFactoryInAmbientScope`**, so callers that haven't moved yet behave exactly as before. T3 makes it required (or makes `Enlist` private to the helper) once both contexts use the helper, so a future context can't miss the fix.
- **T1: the real pool calls `UseApplicationServiceProvider`**, the same as EF Core's own options building, so logging and interceptors resolve the same way as on the DI pool.
- **T1: log lines use the `{Message}` template**, so the caller's text renders exactly and braces in it can't be read as placeholders.
- **T1: the abandoned placeholder pool isn't disposed early.** It's a DI singleton, so the container disposes it at shutdown.
