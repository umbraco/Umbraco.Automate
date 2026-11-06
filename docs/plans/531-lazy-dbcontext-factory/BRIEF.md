# Brief: Survive the installer restart without a connection string

A site installed through the backoffice installer, with no connection string at first boot, goes down the moment you click Install. Automate and OAuth connections stay on a placeholder in-memory database until the application process restarts. This hits anyone setting up a new Umbraco site with Automate installed (#531). The fix also replaces the two copies of the fallback code that caused #487 (#532).

## Why now
- Proven on v18/dev (CMS 18.1.0): a fresh backoffice install with no connection string stops the site right after Install. If the site is kept alive, backoffice login returns 500. It's the first thing a new user sees.
- #532 was waiting on a decision, and this fix needs the same shared code, so both close together.

## Done when
- A fresh backoffice install with no connection strings finishes, and the site keeps running.
- In that same process, with no restart: login works, and a Manual automation with a Log Message step can be created, published and run to Completed.
- OAuth credential cleanup runs without "no such table" errors, and the outbox stops logging errors once a second.
- An automated test boots without a connection string, then sets one, and shows Automate reaching the real database.
- Core and OpenIddict share one "resolve the connection string, or fall back" code path, not two copies.
- The startup warning says Automate starts working once a connection string is configured, without suggesting a process restart is needed, or says a restart is needed if that turns out to be true.
- The same fix is on v17/dev.
- Other places that hold onto the placeholder are fixed here when they go through the same factory. Anything that captures the connection some other way gets its own follow-up issue.

## Must work with
- Core and OpenIddict ship together: one v18 PR, then one v17 PR.
- The next release is a core minor, and OpenIddict's minimum core version goes up in that same release.
- Umbraco CMS 18.x (v18 line) and 17.x (v17 port). The CMS restart behaves the same on both: release-18.1.0 and release-17.3.4.
- SQLite and SQL Server.
- Hosts that supply the connection string late through the options pipeline (Umbraco Cloud, Deploy). This is why the connection is resolved at run time today and not at composition.
- The ambient-scope sharing that exists when Automate uses the CMS database (`UseNamedConnectionString: umbracoDbDSN`).
- No dependency upgrades.

## Unknowns
- Whether anything else holds onto the placeholder connection past the restart, without going through the pooled factory. Candidates: the ambient enlisted factory, the WorkflowCore persistence provider's detached factory, `AutomateSchemaInitializer`, health checks, and cached singletons such as `TriggerSubscriptionRegistry`.
- Whether SQL Server behaves the same as SQLite. Expected yes, not tested.
- How IIS behaves after the host stops today. It probably restarts on the next request, which would hide the bug there. Untested.
- Whether code already running when the connection becomes available (the outbox loop, background jobs) picks it up on its next pass, or needs a nudge.

## Not doing
- **Fixing only the warning wording.** The site still crashes after install.
- **Forcing a process restart after install.** Under Kestrel or `dotnet run` the site would just stop.
- **Changing the CMS installer.** The fix has to work with the CMS as it is.
- **The installer's upgrade path.** An upgrade always starts with a connection string, so it never sees the placeholder.
- **Stopping `WorkflowHostLifecycle` from taking the host down when stuck-run recovery throws.** Fixing the connection already stops this crash. The hardening gets its own follow-up issue.

## Repro
1. Make a demo site with no `umbracoDbDSN` or Automate connection string, `UseNamedConnectionString: umbracoDbDSN`, and unattended install off. A scratch copy is in the agent worktree `.claude/worktrees/v18-agent-a6c5384e59b2e60b5`, under `demos/v18-531/`.
2. Start it. Both "no database connection string configured yet" warnings log, one for Automate and one for OpenIddict.
3. Complete the backoffice installer with SQLite.
4. **Today:** "no such table" errors start straight after `RestartRuntimeStep`, and stuck-run recovery stops the host (`run1.log`). With `BackgroundServiceExceptionBehavior.Ignore`, login returns 500 (`run3.log`).
5. **Fixed:** the site stays up. Without restarting, log in, then create, publish and run a Manual automation to Completed. For comparison, `run2.log` shows the clean boot after a manual restart.
