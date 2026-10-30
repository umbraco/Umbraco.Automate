---
name: ops-release
description: >-
  Ship an Umbraco.Automate release for one version line. Delegates the mechanics to the repo's
  existing skills — `/release-prepare` for planning and cutting, `/release-cleanup` for
  the sync — and `/release-publish` to queue the Azure pipeline run that publishes to NuGet.org,
  tags each product and creates the GitHub release. Called by name with (action, context-json). NOT for direct use — never select it from a
  description match.
---

# ops-release

Turn a release trigger into a shipped release for this repo: plan it, cut it, publish it, then
put the line's branches back in step.

**Visibility: service.** A framework loop may command it.

## Invocation

```
ops-release <action> '<context-json>'
```

Two positional arguments. `context` is a single JSON object encoded as a string; an
**absent context is `{}`**, not an error. **Reject any action not listed below** — never
guess at one, and never silently succeed.

| Action | What it must do |
|---|---|
| `plan` | Turn the trigger into release facts: which line, which version, and which units of work the release contains. Reads only; opens nothing. |
| `cut` | Branch, bump the version files, write the changelog, and open the release PR. Idempotent: re-running MUST return the existing release PR, never open a second. |
| `publish` | Realize the release once its PR has landed: queue the pipeline's publishing run on the release branch and see it through. The pipeline publishes, tags and creates the GitHub release. MUST leave nothing half-published on failure. |
| `sync` | Put the line's branches back in step after a release, so the next change starts from what actually shipped. The step manual releases most often forget. |

## Repo facts every action depends on

> **This is the `v17` line.** The release machinery matches `v18/dev` — same pipeline, same four
> delegated skills, same naming. Only the product semvers differ (17.x here), and
> `lines.primary` in `.claude/ops-repo-meta.json` is still `v18`, so a trigger issue that does
> not name a line defaults to v18, not to this one. Name the line in the issue.

**Two version schemes, and they are not the same thing.**

| Scheme | Looks like | Where it lives | What it names |
|---|---|---|---|
| Release number | `2026.07.4` | the release branch (from the date it was cut) and the GitHub release (from the date it was published; the two can differ) | one shipping event, date-based |
| Product version | `17.1.0` | `<Product>/version.json` (Nerdbank.GitVersioning) | one package, semver |

So one release ships several products, each at its own semver, under one date-based number. The
version in the trigger issue title (`release 2026.08.1`) is the **release number**.

**Naming, all of which already exists and must not drift:**

- Release branch: `<line>/release/<release-number>` — e.g. `v17/release/2026.07.4`
- Release PR title: `chore(release): Prepare release <release-number> (<line>)`
- Per-product tag: `<Product>@<semver>` — e.g. `Umbraco.Automate@17.1.0`
- Release tag and GitHub release: `YYYY.MM.N` titled `Release YYYY.MM.N`, created by the pipeline
  and numbered by **publish date**, so it can differ from the release branch's number. Never
  create either here.

**`release-manifest.json`** at the repo root lists the products to package. CI **fails without it**
on a `vN/release/*` branch. It is deleted again after the release merges.

**CI is Azure Pipelines** (`azure-pipelines.yml`, pipeline id `714`, extending Umbraco.Build's
shared monorepo template). A push builds, tests, packs and uploads **pipeline artifacts**, and
never publishes. Publishing is a run queued with `publish=release`, on a `vN/release/*` or
`vN/hotfix/*` branch only. Its Release stage waits for approval in the `Umbraco Automate Release`
environment, then pushes to NuGet.org, tags `<Product>@<version>` and creates the GitHub release. Read CI state
through `ops-ci`, never by calling Azure directly.

### Delegation — and the one thing to watch

All four actions delegate to skills this repo already ships. All three were confirmed
present:

| Action | Delegates to | Path |
|---|---|---|
| `plan`, `cut` | `/release-prepare` | `.claude/skills/release-prepare/` |
| (via the above) | `/changelog-management`, `/release-manifest-management` | `.claude/skills/` |
| `publish` | `/release-publish` | `.claude/skills/release-publish/` |
| `sync` | `/release-cleanup` | `.claude/skills/release-cleanup/` |

> **Both delegated skills are interactive** — they were written for a human at a keyboard and
> use `AskUserQuestion`. A release loop has nobody to answer. **Always pass the complete context
> up front** (line, release number, products, per-product bumps) so there is nothing left to ask,
> and **if a delegated skill still stops to ask a question, treat that as a failure**: return
> `{"ok": false, "detail": "…"}` naming the question. Never invent an answer to keep going — a
> guessed version bump ships the wrong package.

## Action: `plan`

Turn the trigger into release facts. **Reads only; opens nothing, branches nothing.**

**Context it receives** (guidance — never validate it at runtime):

```json
{"trigger":{"issue_number":512,"version_text":"release 2026.08.1"}}
```

- `trigger` — object — the issue, label or dispatch that asked for a release

**Facts to return:**

- `line` — string
- `version` — string
- `units` — array — the changes included, for the changelog

### Steps

1. **Read the release number** out of `trigger.version_text` — the text after `release `. If it
   is not a `YYYY.MM.N` release number, stop: `{"ok": false, "detail": "…"}`.

2. **Resolve the line.** If the trigger issue names one (`(v17)`, a `v17` label), use it.
   Otherwise use `lines.primary` from `.claude/ops-repo-meta.json`. The line **must** be in
   `lines.live`; refuse if it is not.

3. **Detect what changed** since each product's last release tag, using
   `/release-prepare`'s change-detection phase (its Phase 1) in read-only mode. That gives you
   the changed products, the recommended bump per product, and the cascade — a minor or major on
   Core forces a bump on OpenIddict and Slack.

4. **Preview the changelog** with `/changelog-management` to collect `units`.

5. Return `{"ok": true, "line": "…", "version": "…", "units": [...], "products": [...]}`, where
   `products` carries each product's name, current version and proposed new version. Carry that
   through to `cut` — it is what stops the delegated skill needing to ask.

6. **If nothing changed since the last tag**, say so: `{"ok": false, "detail": "no changes since
   <tag> — nothing to release"}`. An empty release is a mistake, not a no-op.

**Idempotency (a MUST).** `plan` has no side effects at all: it reads git, tags and commits and
returns facts. Nothing to detect, nothing to guard. Keep it that way.

## Action: `cut`

Branch, bump the version files, write the changelog, and open the release PR.

**Context it receives:**

```json
{"plan":{"line":"v17","version":"2026.08.1","units":[],"products":[]}}
```

- `plan` — object — the output of `plan`

**Facts to return:**

- `pr_number` — integer
- `branch` — string

### Steps

1. **Idempotency check, before anything else.** Look for an open PR whose head branch is
   `<line>/release/<version>` (`github-ops`). If one exists, **return it** — `pr_number`, `branch`
   — and stop. Never open a second release PR.

2. Also check whether the branch exists on the remote without a PR. That is a half-finished cut:
   resume from it rather than starting over.

3. **Run `/release-prepare`**, passing the full plan so it has nothing to ask: the line, the
   release number, and every product with its agreed version. It does the work —

   - creates `<line>/release/<version>` off the line's integration branch,
   - updates each product's `version.json`,
   - updates the inter-product ranges in `Directory.Packages.props`,
   - writes `release-manifest.json`,
   - generates each product's `CHANGELOG.md`,
   - commits it all to the release branch.

4. **Check its work before opening anything.** `release-manifest.json` must exist and list exactly
   the products in the plan — CI fails on a release branch without it. Each version bumped must
   match the plan.

5. **Open the release PR** onto the line's release base via `ops-branching · open-pr` — pass the
   line, never a branch name. Title it
   `chore(release): Prepare release <version> (<line>)`. Body: the changelog sections.

6. Return `{"ok": true, "pr_number": N, "branch": "<line>/release/<version>"}`.

**Idempotency (a MUST).** The branch name is a pure function of line + release number, so the same
plan always targets the same branch. Steps 1 and 2 detect an existing PR or an existing branch and
resume instead of duplicating. If `cut` fails partway, it leaves a branch and no PR — which step 2
picks up on the next call.

## Action: `publish`

Realize the release once its PR has landed.

**Context it receives:**

```json
{"plan":{"line":"v17","version":"2026.08.1","products":[]},"merge_commit":"9f1c2ab"}
```

- `plan` — object — the output of `plan`
- `merge_commit` — string — the commit the release PR landed as

**Facts to return:**

- `tag` — string
- `url` — string — the published release

### Steps

1. **Idempotency check first.** If every product's `<Product>@<semver>` tag already exists, the
   release is published: find the GitHub release whose notes list those versions, return it and
   stop. Do not look the release up by `<version>`: the pipeline names it by publish date.

2. **Check for a run already in flight.** If a run of pipeline 714 on `<line>/release/<version>`
   with `publish=release` is in progress or waiting for approval, follow that run (step 5) rather
   than queuing a second one.

3. **Wait for CI on the merge commit.** Poll `ops-ci · status`. Green: continue. Red: stop with
   `{"ok": false, "detail": "…"}` and the failing stage from `ops-ci · log` — never publish off a
   red build. Still pending after a reasonable wait: stop and say it is still running, so the
   caller can come back rather than the release half-happening.

4. **Run `/release-publish`** for `<line>/release/<version>` with target `release`, passing that it
   is called by this skill so it asks nothing. The run must be on the **release branch**, not the
   merge commit on `<line>/main`: the pipeline refuses `publish=release` anywhere else. It checks
   no version is already on NuGet.org, then queues the run.

5. **Follow the run.**
   - **Waiting for approval:** return `{"ok": false, "waiting": "approval", "detail": "…",
     "run_url": "…"}`. This is not a failure: a person approves in Azure, and a later call picks the
     run up at step 2. **Never approve it.**
   - **Failed:** return `{"ok": false, "detail": "…"}` with the failing stage and task. Never queue
     another run to retry: if the Release stage failed after some packages were pushed, a new run
     would repeat them and fail. A human decides.
   - **Succeeded:** continue.

6. **Confirm** the `<Product>@<semver>` tags exist (`git ls-remote --tags origin`) and find the
   GitHub release the run created.

7. Return `{"ok": true, "tag": "<YYYY.MM.N of the GitHub release>", "url": "…", "product_tags": [...], "run_url": "…"}`.

**Idempotency (a MUST).** Publishing cannot be undone, so: existing product tags short-circuit at
step 1; a run already in flight is followed, never duplicated, at step 2; and `/release-publish`
refuses any version already on NuGet.org. The pipeline skips tags that already exist and, on a
rerun, updates its own GitHub release rather than creating a second.

## Action: `sync`

Put the line's branches back in step after a release.

**Context it receives:**

```json
{"line":"v17"}
```

- `line` — string — the line that was just released

**Facts to return:**

- `ok` — boolean
- `pr_number` — integer|null — set when the sync needed a PR rather than a fast-forward

### Steps

1. **Run `/release-cleanup`** for the line, passing the release branch and release number so
   it has nothing to ask. It:
   - merges the release branch into `<line>/main` and `<line>/dev`,
   - bumps `version.json` on `<line>/dev` so nightly builds sort above what just shipped,
   - deletes the release branch,
   - and, on a major cutover only, creates the next line and moves the default branch.

2. **Remove `release-manifest.json`** from `<line>/dev` if the merge carried it back. It belongs
   only on a release branch, and the repo already does this
   (`chore(ci): Remove release-manifest.json after merge`).

3. **Never force a merge.** If either back-merge conflicts, open a PR instead and return its
   number in `pr_number`. A conflicting back-merge is a human's call.

4. Return `{"ok": true, "pr_number": null}` on a clean sync, or the PR number when one was needed.

**Idempotency (a MUST).** Before merging, check whether the release commit is already an ancestor
of `<line>/main` and `<line>/dev` — if it is, that half is done, so skip it. Check the version on
`<line>/dev` is not already above the released version before bumping again. A missing release
branch at step 1 means cleanup already ran: that is success, not an error.

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
- **Never publish off a red or pending build.** Green CI is the gate; there is no override in
  this skill.
- **Never delete or move an existing tag.** Tags are the record of what shipped.
- **Release one line at a time.** Lines are never forward-merged, so releasing `v17` says nothing
  about `v18`. Each needs its own trigger issue.
- **Do not reimplement the release skills.** If `/release-prepare` or
  `/release-cleanup` is wrong, fix it there — this skill is the loop's adapter, not a second
  copy of the process.
- **A delegated skill that stops to ask a question is a failure.** Report the question; never
  answer it yourself.
