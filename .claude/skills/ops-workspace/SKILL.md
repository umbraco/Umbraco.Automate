---
name: ops-workspace
description: >-
  An isolated place to build and test one change in Umbraco.Automate — a git worktree under
  `.claude/worktrees/`, rooted on the target line's integration branch, with the local config
  files a build needs — and the teardown that removes it. Prepared and torn down by
  `ops-change`, never by a loop. Called by name with (action, context-json). Not model-invoked.
disable-model-invocation: true
---

# ops-workspace

One change, one clean place to build it.

**Visibility: supporting primitive.** Only **`ops-change`** calls it. A framework loop must
not — the loop does not know whether this repo needs a worktree, a container, a seeded
database, or nothing at all, and the moment a loop decides that, the engine has learned a
product fact.

## Invocation

```
ops-workspace <action> '<context-json>'
```

| Action | Context | Returns |
|---|---|---|
| `prepare` | `{ branch }` | `{ ok, path, reused, fidelity, notes }` |
| `teardown` | `{ path }` | `{ ok }` |

An absent context is `{}`. **Reject any other action.**

## Repo facts both actions depend on

> **This is the `v17` line.** Everything below is the v17 branch's own copy. The worktree
> layout, the hooks, the solution and the pipeline all match `v18/dev`. What differs is the
> demo-site directory (`demos/v17/`) and the npm registry: **this line has no `.npmrc`**, so
> `@umbraco-cms` packages come from the public registry, where v18 has to pull prereleases
> from MyGet.

**Worktrees live at `.claude/worktrees/<slug>`**, which is gitignored. The slug is the branch
name with the `feature/` segment dropped and remaining slashes flattened to dashes:
`v17/feature/issue-112-binding-case-insensitive-lookup` → `v17-issue-112-binding-case-insensitive-lookup`.

**The base is the line's own integration branch.** For a `vN/feature/*` branch, root it on
`origin/vN/dev`; fall back to `origin/vN/main` only if that line has no `dev`. **Version lines
are never forward-merged** — never root a v17 branch on anything from v18, and vice versa.

**`.githooks/pre-push` rejects any branch that is not** `vN/dev`, `vN/main`,
`vN/{feature,release,hotfix}/*` or `claude/*`. A workspace on a branch outside that set cannot
be pushed, so treat an off-pattern branch as an error rather than creating a tree for it.

**Package feeds need no credentials.** `NuGet.config` is committed and points at public feeds
(nuget.org, MyGet `umbracoprereleases`, MyGet `umbraconightly`). There is **no `.npmrc` on this
line** — npm resolves `@umbraco-cms` from the public registry, which is the correct v17
behaviour and not a missing file to add. **Never** add a feed, a token or a
`packageSourceCredentials` block here.

**No database to provision.** The integration tests are self-contained — `azure-pipelines.yml`
runs `dotnet test Umbraco.Automate.slnx` with no SQL service and no container. A plain worktree
is therefore full CI parity, and `prepare` **MUST NOT** stand up a database, a container or a
demo site.

**The demo site is not part of a loop workspace.** `.worktreeinclude` copies `demos/v17/` for a
human's interactive worktree, but the loops only build and test. Skip it — see `prepare` step 4.

## Action: `prepare`

Create an isolated workspace for that branch and leave it **ready to build**.

**Idempotent.** An existing workspace for that branch **MUST** be reused and returned with
`reused: true`, never duplicated. Two worktrees on one branch is a corruption, not a
convenience, and a re-fired routine will ask twice.

### Steps

1. **In a cloud routine, do nothing.** The session already has its own isolated checkout and no
   sibling work to collide with. Return that checkout's path with `reused: true`,
   `fidelity: "ci-parity"`, and a note saying the session checkout was used. Do **not** nest a
   worktree inside it. Detect this the plain way: read the repo's main worktree root with
   `git rev-parse --path-format=absolute --git-common-dir` and drop the trailing `.git`; if that
   root is not the developer's `D:/DXP/Automate/Umbraco.Automate` checkout, assume cloud. Note
   that on this line a developer's `v17/dev` itself normally sits in a worktree under
   `.claude/worktrees/`, so never decide cloud-versus-local from the current directory alone.

2. **Work out the slug and path** from the branch, per the convention above. If
   `.claude/worktrees/<slug>` already exists, return it with `reused: true` and stop.

3. **Create the worktree**, in this order:
   - the branch exists locally → `git worktree add "<path>" "<branch>"`
   - it exists on the remote → `git worktree add --track -b "<branch>" "<path>" "origin/<branch>"`
   - it exists nowhere → `git worktree add -b "<branch>" "<path>" "origin/<line>/dev"`

   Fetch first so `origin/<line>/dev` is current — a workspace rooted on a stale base produces a
   PR that conflicts on arrival.

4. **Copy the local config files, but not the demo site.** Copy from the main checkout, if
   present:

   ```
   appsettings.Development.json
   appsettings.Local.json
   .env*  (but never .env.template)
   ```

   **Do not copy `demos/`.** It is hundreds of megabytes, the loops never launch it, and a stale
   copy per worktree is pure cost. A human running `/demo-site-management` in their own worktree
   is a different case and not this action's concern. Say in `notes` that the demo site was
   skipped, so nobody reads its absence as a failure.

5. **Restore.** `dotnet restore Umbraco.Automate.slnx` in the worktree. Only if the change
   touches frontend files (anything under a `Client/` directory) also run `npm ci` **at the
   worktree root** — this is an npm workspaces monorepo, and installing inside `Client/` produces
   a spurious root lockfile diff.

6. Return:

   ```json
   {"ok": true, "path": "…", "reused": false, "fidelity": "ci-parity", "notes": "…"}
   ```

### `fidelity` is always `ci-parity` here

There is exactly one test target: `dotnet test` against the solution, the same command CI runs,
with no database engine to swap and no stand-in fixtures. So this action reports `ci-parity`
every time and never reports `reduced`. If that ever stops being true — a container appears, or
a faster stand-in target is added — this section is what has to change first, because a silent
downgrade would let `ops-change · verify` report a pass CI will not reproduce.

## Action: `teardown`

Remove the worktree and everything `prepare` created.

**Always tear down, including after a failure.** The branch has been pushed by the time anything
can fail inside `ops-change`, so nothing is lost, and worktrees left behind on failure are what
fills the disk and collides on the next run.

### Steps

1. If the path does not exist, return `{"ok": true}`. Nothing to do is a success.

2. If the path is the **main checkout**, a **cloud session checkout**, or the developer's
   standing `v17/dev` worktree — anything `prepare` returned with `reused: true` and did not
   create — remove nothing and return `{"ok": true}`.

3. `git worktree remove "<path>" --force`. If that fails (common on Windows when a build process
   or an IDE still holds a file), `git worktree prune`, then remove the directory.

4. **Never delete the branch.** The PR is built on it.

Return `{"ok": true}`.

## Rules

- **Never called by a loop.** If a loop is preparing a workspace, `ops-change` has been
  bypassed.
- **`prepare` leaves it buildable**, or reports why it could not.
- **Both actions are idempotent**, and `teardown` is safe on an absent workspace.
- **Teardown destroys only what prepare made** — never the main checkout, never a branch.
- **One workspace per change.** Never two changes in one tree.
- **Never provision a database, container or demo site.** If a change genuinely needs one, that
  is a repo fact that changed, and this file is where it gets recorded — not an improvisation
  inside a run.
- **Never add feed credentials.** The NuGet feeds are public and committed, and this line needs
  no `.npmrc` at all.
