---
name: ops-branching
description: >-
  This repo's branch model, and the only holder of it. Merges a PR with whichever strategy
  the model calls for, opens a PR onto the correct base for its line, and starts a work
  branch rooted correctly. The integration branches, the release base and the merge
  strategy are PRIVATE — a caller asks for an outcome and never learns a branch name or a
  strategy. Command-only: it answers no reads. Called by name with (action, context-json)
  by a service, never by a loop. NOT for direct use — never select it from a description match.
---

# ops-branching

The repo's branch model lives here and **nowhere else**. Before this capability existed,
base-branch knowledge lived in four places — a config schema, a detection skill, the merge
loop's own resolve-and-compare, and a forge operation that deferred back to the detection
skill. They drifted. Collapsing them into one owner is the point of this skill.

**Visibility: supporting primitive.** A **service** (`ops-integrate`, `ops-release`,
`ops-change`) may call it. A **framework loop must never call it directly** — if a loop
holds a branch name, this capability has failed.

## The privacy rule

**The integration branches, the release base and the merge strategy never leave this
skill.** There is no `resolve-base`, no `merge-strategy`, and no `classify-pr`. A caller
says "merge this PR" and reads back what happened; it does not ask "what is the base?" and
then compare.

Why it is worth the inconvenience: the moment a caller can read the base, two callers can
disagree about it, and the drift is back. An outcome cannot drift.

**Command-only.** Every action below *does* something. This skill answers no questions.

## Invocation

```
ops-branching <action> '<context-json>'
```

| Action | Context | Returns |
|---|---|---|
| `merge` | `{ pr }` | `{ ok, merged, merge_commit, refused, line }` |
| `open-pr` | `{ branch, line, title, body }` | `{ ok, pr_number, url }` |
| `start-branch` | `{ line, slug }` | `{ ok, branch }` |

An absent context is `{}`. **Reject any other action** — do not guess. All three are
idempotent: see each action.

All GitHub work goes through **`github-ops`** by operation name. This skill decides *what*
and *onto what*; `github-ops` owns *how*.

## What it knows, privately

Resolve these **once per invocation**, and never return them:

1. **The live lines** — from `ops-repo-meta · lines`. **Read it every time; never cache
   it.** A major-version cutover adds a line, and no engine change may be needed for that.
2. **The branch model** is fixed: **`versioned-gitflow`**. Do not detect it, and do not
   improvise if a branch is missing — a live line that has no `vN/dev` **and** `vN/main` pair
   is a configuration error to report, not a gap to guess around. See
   [CONTRIBUTING.md](../../../CONTRIBUTING.md) for the full branch model.
3. **The integration branches — a SET, not a branch.** `vN/dev`, one per live line.
   **Every base check is set membership.** With v18 and v17 live there are two legitimate
   bases, and equality against one of them would flag the other as wrong-base.
4. **The release bases** — `vN/main`, one per live line.
5. **The merge strategy — `merge-commit`, always.** This is where this repo departs from the
   engine default, which squashes normal PRs. Do not squash anything:

   | Merging | Strategy | Why |
   |---|---|---|
   | a work branch into `vN/dev` | `merge-commit` | this repo's changelog is generated from conventional-commit history (`changelog-management`), and squashing collapses a multi-commit change into one subject, losing the `fix:` / `feat:` entries the changelog needs. Every merge in this repo's history is a merge commit. |
   | a `vN/release/*` or `vN/hotfix/*` branch into `vN/main` | `merge-commit` | the version-bump commit must survive for tagging and the back-merge. |

   The caller never passes the strategy.

## Action: `merge`

Merge the PR in front of you, using this repo's strategy.

1. Read the PR (`github-ops` → *Get a PR*): base, mergeable state, head branch.
2. **Check the base is one of the integration branches** — membership, not equality. It is
   not this skill's job to decide *whether* the PR should land (that is `ops-integrate`'s
   gates); it is this skill's job to refuse to merge into a branch the model says is not a
   merge target. Refuse rather than merging somewhere plausible.
3. Merge with the resolved strategy and **delete the head branch** (`github-ops` → *Merge a
   PR (+ delete branch)*).

**A refusal is classified, not narrated.** Return `{ ok: false, refused: … }` with one of:

| `refused` | The base is |
|---|---|
| `release-base` | this line's release base. The release path owns it, and this is a normal outcome. |
| `not-a-merge-target` | neither a release base nor an integration branch of any live line. |

Set `refused: null` when the merge went ahead. **This is the one thing about the model that
does leave the skill, and it is deliberate:** the caller has to tell a release PR apart from a
mistargeted one to report the right outcome, and making it read that out of a prose `detail`
puts a string-match where a decision belongs. A classification is not a branch name — the
caller still learns nothing about *which* branch, or how many there are. Add a plain-language
`detail` alongside it for the human-facing comment.

**Also return `line`: which live line the PR landed on.** A **line** is declared public data —
`ops-repo-meta · lines` hands the whole set to anyone who asks — so returning it breaks no
privacy rule. The branch it maps to stays private, and that mapping lives only here, which
makes this the only place the question can be answered. It matters because everything that
happens after a landing needs it: which issue to close, and which lines to port to. Without it
the caller has to read a line out of the base ref, which is the guess this whole capability
exists to remove.

**Idempotent:** a PR that is **already merged MUST** return `{ ok: true, merged: true }`
with the existing `merge_commit`, not attempt a second merge. The landing label stays on a
PR after it lands, so a sweeping caller *will* hand you the same PR twice.

**Never** force-merge, never use GitHub's native auto-merge (it would land without the
caller's gates), never merge into a release base here — a release lands through
`ops-release`.

## Action: `open-pr`

Open a PR from a work branch onto the right base for its line.

1. Resolve the integration branch **for that line** — the caller names the line (`v17`), not
   the branch (`v17/dev`). A caller that knows the branch already has the leak this skill
   exists to prevent.
2. Open it (`github-ops` → *Create a PR*) with the given title and body.

**Idempotent:** an open PR from that head branch onto that base **MUST** be returned as-is,
not duplicated.

## Action: `start-branch`

Create a work branch, named this repo's way, rooted on the right base.

1. Resolve the integration branch for the named line.
2. Name the branch **`<line>/<type>/<slug>`** — e.g. `v18/feature/issue-165`. Two rules that
   differ from the engine default and are **enforced by `.githooks/pre-push.sh`**, so a wrong
   name is not a style problem, it is a push that fails:
   - **The `vN/` prefix is mandatory.** A bare `feature/<slug>` is rejected at push time.
   - **`type` is one of `feature` / `hotfix` / `release` only.** There is no `fix`, `chore`,
     `docs`, `refactor` or `test` branch type here — those are *commit* scopes, not branch
     types. A fix goes on a `feature/` branch. Use `feature` unless the caller is the release
     path.

   `release` and `hotfix` are the release path's to create, not a caller's: `start-branch`
   should only ever be asked for `feature` in loop use.
3. Create it from that base (`github-ops` → *Create a branch*).

**Idempotent:** if the branch exists, return it.

## Rules

- **Never return a branch name or a strategy** except the head branch a caller just asked
  you to create (`start-branch`) — that one is the caller's own handle on its work, not
  knowledge of the model.
- **Never squash.** Every merge in this repo is a merge commit, because the changelog is built
  from conventional-commit history. A squash silently drops changelog entries, and nothing
  fails at the time to tell you.
- **Never create a branch without the `vN/` prefix.** The pre-push hook rejects it, so the
  work is done and then cannot be pushed.
- **Never commit directly to an integration branch or a release base.** Always a branch,
  always a PR.
- **Base checks are membership over the live-line set**, re-read every invocation.
- **Never force-push.**
- **A red PR is not this skill's problem.** It merges what it is told to merge, once the
  base is legitimate. Gating is `ops-integrate`'s job, and duplicating it here would put
  merge policy back in two places.
- **`custom` model with no repo override is an error, not an improvisation.**
