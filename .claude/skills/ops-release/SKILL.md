---
name: ops-release
description: >-
  Ship an Umbraco.Automate release for one version line. Delegates the mechanics to the repo's
  existing skills — `/release-management` for planning and cutting, `/post-release-cleanup` for
  the sync — then waits for the Azure pipeline, tags per product and per release, and publishes
  the GitHub release. Called by name with (action, context-json). Not model-invoked.
disable-model-invocation: true
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
| `publish` | Realize the release once its PR has landed: tag the commit, push the artifacts to their feed, and publish the release notes. MUST leave nothing half-published on failure. |
| `sync` | Put the line's branches back in step after a release, so the next change starts from what actually shipped. The step manual releases most often forget. |

## Repo facts every action depends on

**Two version schemes, and they are not the same thing.**

| Scheme | Looks like | Where it lives | What it names |
|---|---|---|---|
| Release number | `2026.07.4` | the release branch and the GitHub release | one shipping event, date-based |
| Product version | `18.1.0` | `<Product>/version.json` (Nerdbank.GitVersioning) | one package, semver |

So one release ships several products, each at its own semver, under one date-based number. The
version in the trigger issue title (`release 2026.08.1`) is the **release number**.

**Naming, all of which already exists and must not drift:**

- Release branch: `<line>/release/<release-number>` — e.g. `v18/release/2026.07.3`
- Release PR title: `chore(release): Prepare release <release-number> (<line>)`
- Per-product tag: `<Product>@<semver>` — e.g. `Umbraco.Automate@18.1.0`
- Release tag: `<release-number>` — e.g. `2026.07.4`
- GitHub release title: `Release <release-number>`

**`release-manifest.json`** at the repo root lists the products to package. CI **fails without it**
on a `vN/release/*` branch. It is deleted again after the release merges.

**CI is Azure Pipelines** (`azure-pipelines.yml`). It builds, tests, packs NuGet and npm, and
uploads them as **pipeline artifacts**. It does **not** push packages to any feed. Read CI state
through `ops-ci`, never by calling Azure directly.

### Delegation — and the one thing to watch

Three of the four actions delegate to skills this repo already ships. All three were confirmed
present:

| Action | Delegates to | Path |
|---|---|---|
| `plan`, `cut` | `/release-management` | `.claude/skills/release-management/` |
| (via the above) | `/changelog-management`, `/release-manifest-management` | `.claude/skills/` |
| `sync` | `/post-release-cleanup` | `.claude/skills/post-release-cleanup/` |

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
   `/release-management`'s change-detection phase (its Phase 1) in read-only mode. That gives you
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
{"plan":{"line":"v18","version":"2026.08.1","units":[],"products":[]}}
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

3. **Run `/release-management`**, passing the full plan so it has nothing to ask: the line, the
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
{"plan":{"line":"v18","version":"2026.08.1","products":[]},"merge_commit":"9f1c2ab"}
```

- `plan` — object — the output of `plan`
- `merge_commit` — string — the commit the release PR landed as

**Facts to return:**

- `tag` — string
- `url` — string — the published release

### Steps

1. **Idempotency check first.** If a GitHub release for `<version>` already exists, return it and
   stop. Publishing is the irreversible action here — check before, not after.

2. **Wait for CI on the merge commit.** Poll `ops-ci · status`. Green: continue. Red: stop with
   `{"ok": false, "detail": "…"}` and the failing stage from `ops-ci · log` — never publish off a
   red build. Still pending after a reasonable wait: stop and say it is still running, so the
   caller can come back rather than the release half-happening.

3. **Confirm the packages exist.** The pipeline's Pack stage must have produced artifacts for
   every product in `release-manifest.json`. A green run that packed nothing is not a release.

4. **Tag — both schemes, in this order.** Skip any tag that already exists; never move one.
   - one `<Product>@<semver>` tag per released product, on `merge_commit`
   - one `<version>` release tag, on `merge_commit`

   Push the per-product tags first: if the run dies between the two, the release tag is missing
   and step 1 will correctly see the release as unpublished and retry.

5. **Create the GitHub release** on the `<version>` tag, titled `Release <version>`, with the
   changelog sections as the body. Mark it pre-release if any product version carries a
   pre-release suffix.

6. **Push no packages.** The pipeline holds them as artifacts and nothing in this repo pushes them
   to a feed. Report the pipeline run URL so whoever does can find them.

7. Return `{"ok": true, "tag": "<version>", "url": "…", "product_tags": [...], "artifacts_url": "…"}`.

**Idempotency (a MUST).** Three guards, because this is the action that cannot be undone: an
existing GitHub release short-circuits at step 1; every tag is checked for existence before it is
pushed and never moved; and no package is pushed anywhere, so there is no feed to double-publish
to. A failure part-way leaves tags but no release — which the next call finishes.

## Action: `sync`

Put the line's branches back in step after a release.

**Context it receives:**

```json
{"line":"v18"}
```

- `line` — string — the line that was just released

**Facts to return:**

- `ok` — boolean
- `pr_number` — integer|null — set when the sync needed a PR rather than a fast-forward

### Steps

1. **Run `/post-release-cleanup`** for the line, passing the release branch and release number so
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
- **Release one line at a time.** Lines are never forward-merged, so releasing `v18` says nothing
  about `v17`. Each needs its own trigger issue.
- **Do not reimplement the release skills.** If `/release-management` or
  `/post-release-cleanup` is wrong, fix it there — this skill is the loop's adapter, not a second
  copy of the process.
- **A delegated skill that stops to ask a question is a failure.** Report the question; never
  answer it yourself.
