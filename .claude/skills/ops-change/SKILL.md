---
name: ops-change
description: >-
  Build one change in Umbraco.Automate: implement it on a `vN/feature/*` branch, prove it with
  `dotnet build` + the affected product's tests and its dependents' tests, and close the issue
  once it has landed on every line a human confirmed as a target. Ports are never opened without
  that confirmation. Called by name with (action, context-json). Not model-invoked.
disable-model-invocation: true
---

# ops-change

Build one change in this repo: implement it, prove it, and close the issue that asked for it.

**Visibility: service.** A framework loop may command it.

## Invocation

```
ops-change <action> '<context-json>'
```

Two positional arguments. `context` is a single JSON object encoded as a string; an
**absent context is `{}`**, not an error. **Reject any action not listed below** — never
guess at one, and never silently succeed.

| Action | What it must do |
|---|---|
| `implement` | Make the change the issue asks for on a work branch, and push it. Where the context carries a port source this is a port, not a replay: adapt the change to the target line, because a port that needs adapting is a real change. |
| `verify` | Run this repo's build, tests and sanity checks against the change, and report pass or fail with enough detail for the caller to act on a failure. |
| `close-issue` | Told that a PR has landed, work out which issue it was for and close that issue only once EVERY target line has landed — one logical change lands N times at N moments. The caller passes the PR, not the issue, because how a PR references its issue and which lines are targets are both repo facts. MUST close explicitly: a `Closes #N` keyword does not cross repos. MUST tolerate an already-closed issue, and MUST report `closed: false` with the lines still outstanding rather than closing early. |

## Repo facts every action depends on

**The product graph.** Three products, in dependency order. This is the same graph
`azure-pipelines.yml` encodes as level 0 / 1 / 2, and it decides test scope:

| Level | Product | Path | Depends on | Test projects |
|---|---|---|---|---|
| 0 | `Umbraco.Automate` | `Umbraco.Automate/` | — | `Tests.Unit`, `Tests.Integration` |
| 1 | `Umbraco.Automate.OpenIddict` | `Umbraco.Automate.OpenIddict/` | Core | `Tests.Unit`, `Tests.Integration` |
| 2 | `Umbraco.Automate.Slack` | `Umbraco.Automate.Slack/` | OpenIddict, Core | **none** |

**Branch naming is hook-enforced.** `.githooks/pre-push` rejects any branch that is not
`vN/dev`, `vN/main`, `vN/{feature,release,hotfix}/*` or `claude/*`. Only `v*/feature/*` is in
the Azure Pipelines push trigger, so a `claude/*` branch would push but never get CI — and the
issue loop needs CI green. Work branches are therefore **always** `vN/feature/…`.

**Commits are linted.** `.githooks/commit-msg` runs commitlint. Every commit must be
`<type>(<scope>): <Sentence case description>` with a type and scope from `CLAUDE.md`.

## Action: `implement`

Make the change the issue asks for on a work branch, and push it. Where the context carries a
port source this is a port, not a replay: adapt the change to the target line.

**Context it receives** (guidance — never validate it at runtime):

```json
{"issue":{"repo":"umbraco/Umbraco.Automate","number":159,"title":"Config references inside larger strings are not resolved"},"line":"v18","port":null}
```

- `issue` — object — the issue being worked, including the repo that holds it
- `line` — string — the line to implement on, e.g. `v18`
- `port` — object|null — the source line and commit when this is a port of an already-landed change

**Facts to return:**

- `branch` — string — the branch the work was pushed to
- `summary` — string — what was changed, for the PR body

### Steps

1. **Work out the branch name first**, because it is also the idempotency key:
   `<line>/feature/issue-<issue.number>-<slug>`, where `<slug>` is a short kebab-case phrase from
   the issue title. Example: `v18/feature/issue-159-embedded-config-references`.

   > This action names and creates its own branch rather than calling
   > `ops-branching · start-branch`. **Reason:** the engine default names branches
   > `<type>/<slug>`, which `.githooks/pre-push` rejects outright, and the retired
   > `branching.branch_naming` key no longer exists to tell it otherwise. Base *resolution*
   > still belongs to `ops-branching` — see step 3.

2. **Idempotency check.** Ask `github-ops` whether that branch already exists on the remote.
   If it does, do not implement again: return the existing branch with a summary read from its
   commits, and stop.

3. **Get the workspace.** Call `ops-workspace · prepare` with the branch. Let it root the branch
   on the line's integration branch — do not resolve `v18/dev` here by hand. Everything below
   runs inside that workspace.

4. **Read before writing.** Read the issue in full, then `CLAUDE.md` at the repo root and the
   `CLAUDE.md` of the product you are about to touch. Follow the conventions already in the
   surrounding files.

5. **Make the change.**
   - **A normal change:** implement what the issue asks for, and add or update tests in the
     affected product's `Tests.Unit` (or `Tests.Integration` where the behaviour needs a real
     database or Umbraco host).
   - **A port** (`port` is non-null): start from `port.commit`, then **adapt it to the target
     line**. Cherry-picking blind is not a port. APIs, namespaces and CMS versions differ
     between lines; a conflict is a signal to think, not to force. If the change genuinely
     cannot be adapted, stop and return `{"ok": false, "detail": "…"}` naming what diverged.
   - **Never** upgrade a dependency version that the issue did not ask you to upgrade.
   - **Never** add a secret, key or connection string.

6. **Commit** in conventional-commit form, sentence-case, valid scope. Small, coherent commits.
   Do not use `--no-verify`; if commitlint rejects the message, fix the message.

7. **Push** the branch.

8. Return `{"ok": true, "branch": "…", "summary": "…"}`. Write the summary as PR-body prose: what
   changed, why, and how a reviewer can test it — the PR template asks for all three.

   **Leave the port decision open, and say so.** End the summary with the template's *Other
   version lines* section, both boxes **unticked**, naming the other live lines and asking the
   reviewer to pick one. Never tick a box on the reviewer's behalf. That section is what
   `close-issue` reads later, so an unanswered question there is what correctly holds the issue
   open instead of closing it early.

**Idempotency (a MUST).** The branch name is derived purely from `issue.number` and `line`, so
the same context always produces the same name. Step 2 detects an existing remote branch and
returns it untouched instead of implementing a second time.

## Action: `verify`

Run this repo's build, tests and sanity checks against the change, and report pass or fail with
enough detail for the caller to act on a failure.

**Context it receives** (guidance — never validate it at runtime):

```json
{"branch":"v18/feature/issue-159-embedded-config-references"}
```

- `branch` — string — the branch to verify
- `scope` — string — optional narrowing, e.g. one test project

**Facts to return:**

- `ok` — boolean
- `failures` — array — one entry per failing check, with its output

### Steps

1. **Prepare the workspace** for the branch (`ops-workspace · prepare`) and run everything
   inside it. Never verify against a dirty local tree.

2. **Build everything:**

   ```bash
   dotnet build Umbraco.Automate.slnx
   ```

   The root solution. A change in one product routinely breaks a downstream one at compile time,
   and this is the cheapest place to catch it.

3. **Work out the test scope: what changed, plus its dependents.** Diff the branch against the
   line's integration branch, map each changed path to a product using the table above, then walk
   *downstream*:

   | Changed product | Test these |
   |---|---|
   | `Umbraco.Automate` (Core) | Core, OpenIddict *(Slack has no tests — the build in step 2 is its check)* |
   | `Umbraco.Automate.OpenIddict` | OpenIddict |
   | `Umbraco.Automate.Slack` | nothing to run; step 2 is its check |
   | repo root / `Directory.Packages.props` / `scripts/` | all of them |

   Never test *upstream*. A Slack change cannot break Core.

4. **Run the tests** for each product in scope:

   ```bash
   dotnet test <Product>/<Product>.slnx
   ```

   If `scope` was passed, honour it and narrow to that project — but say in the result that the
   run was narrowed, so the caller does not read a partial pass as a full one.

5. **Frontend, only if frontend files changed** (anything under a `Client/` directory). Install
   at the **repo root**, never inside `Client/` — this is an npm workspaces monorepo and
   installing in the workspace directory produces a spurious root lockfile diff:

   ```bash
   npm ci          # repo root
   npm run build   # repo root
   ```

6. **Formatting**, only if you touched files Prettier owns: `npm run format:check` at the root.

7. Return `{"ok": true, "failures": []}`, or `{"ok": false, "failures": [...]}` with, per failure,
   the command that ran, the project, and the trimmed output — enough for the caller to fix it
   without re-running anything.

**Idempotency (a MUST).** `verify` is pure: it builds, tests and reports, and writes nothing to
GitHub, no branch, no tag, no comment. Running it twice costs time and changes nothing, so no
already-ran detection is needed. Keep it that way — if this action ever needs a side effect, it
belongs in a different action.

## Action: `close-issue`

Told that a PR has landed, work out which issue it was for and close that issue only once
**every** target line has landed.

**Context it receives** (guidance — never validate it at runtime):

```json
{"landed":{"repo":"umbraco/Umbraco.Automate","pr_number":174,"line":"v18"}}
```

- `landed` — object — the PR that just landed: repo, pr_number, and its line
- `issue` — object — optional, when the caller already knows which issue it was

**Facts to return:**

- `closed` — boolean
- `issue` — object — the issue this resolved to, so the caller can report it
- `waiting_on` — array — target lines not yet landed; empty when `closed` is true

### Steps

1. **Resolve the issue.** Use `context.issue` if the caller supplied it. Otherwise read the PR
   via `github-ops` and take the number from its **head branch**: `vN/feature/issue-<N>-<slug>`.
   The branch is the reliable source here — merged PRs in this repo routinely have an empty body.
   Fall back to a `#N` reference in the PR body or title only if the branch carries no number. If
   nothing resolves, return `{"ok": false, "detail": "cannot resolve an issue for PR #…"}` and
   close nothing.

2. **Work out the target lines from the human's confirmed port decision** — never from
   `lines.live` alone. Read the decision, in this order, and stop at the first one that answers:

   | Where | What it means |
   |---|---|
   | The landed PR's **Other version lines** section, `only applies to the version line I am targeting` ticked | targets = the landed line only |
   | The same section, `should be ported` ticked, with the lines or a linked PR named | targets = the landed line plus those lines |
   | A maintainer comment on the issue or PR saying which lines to port to (or that none apply) | targets = what it says |
   | A line-scoped label on the issue (e.g. `v17`) | targets = the landed line plus that line |

   **If none of those answers it, the decision has not been made.** Do not assume, in either
   direction. Ask for it (step 3) and close nothing.

3. **If the decision is missing**, ask a human via `ops-notify · send`, keyed on the issue number
   so the same question is never sent twice. Say which line landed and which other live lines
   exist. Then return `{"ok": true, "closed": false, "issue": {...}, "waiting_on": ["<undecided
   lines>"], "detail": "awaiting a port decision"}`.

4. **Check each target line has landed.** For each one, search for a merged PR whose head branch
   is `<line>/feature/issue-<N>-…`. The lines with no merged PR go in `waiting_on`.

5. **If `waiting_on` is non-empty**, return `{"ok": true, "closed": false, "issue": {...},
   "waiting_on": [...]}`. Close nothing. Half a change is not a fixed issue.

6. **If every target line has landed**, close the issue **explicitly** via `github-ops` with a
   comment listing each line and its PR number. Never rely on a `Closes #N` keyword.

7. Return `{"ok": true, "closed": true, "issue": {...}, "waiting_on": []}`.

**Idempotency (a MUST).** Read the issue's state before closing. An already-closed issue is a
success, not an error: return `closed: true` and add no second comment. Check for an existing
close-comment from this action before writing one.

## Rules

- **Reject an unknown action.** Report it; never guess, never silently succeed.
- **Every action is idempotent.** A loop sweeps on a cadence and will hand you the same
  work twice.
- **A failed action leaves a safe state** — no partial publish, no dangling branch it
  created and cannot resume.
- **Make success and failure unambiguous.** End with a single JSON object:
  `{"ok": true, ...facts...}` or `{"ok": false, "detail": "..."}`.
- **All GitHub work goes through `github-ops`** by operation name — never a raw `gh` or
  `curl` here.
- **Never port without a human's confirmation, and never assume a change is single-line either.**
  `CLAUDE.md` is the rule: before a change counts as done, ask whether it applies to the other
  active lines and confirm with a human. Silence is not an answer — an unanswered port question
  holds the issue open rather than closing it or opening a speculative PR.

  When a port **is** confirmed, it needs its own PR per line, branched from that line's own
  integration branch — version lines are never forward-merged. Title it the way the repo already
  does: `… (v17 backport)` or `… (v18 forward-port of #107)`. Respect each line's phase: a line
  in **security** phase takes security fixes only, and an **EOL** line is skipped, whatever the
  confirmation said.

  A port arriving as `implement` with a non-null `port` **is** the confirmation — the caller has
  already been told yes. Do not ask again.

- **Never `--no-verify`.** The commit-msg and pre-push hooks encode rules the loops depend on.
- **Never force-push** a branch another PR is built on.
- **Never commit a secret.** If you find one already in the code, stop and report it.
- **Never upgrade a dependency** that the issue did not ask you to upgrade.
- **Follow the repo's conventions rather than importing new ones** — read the nearest
  `CLAUDE.md` before writing code.
