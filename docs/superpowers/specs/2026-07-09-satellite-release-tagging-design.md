# Design: Tagging & GitHub Releases for Umbraco.*.Automate Satellites

Date: 2026-07-09

## Problem

The six `Umbraco.*.Automate` satellite repos (Commerce, Engage, Forms, Workflow, Deploy, UIBuilder — see
`automate-satellite-repos` memory) have a working release process (release branch → CI build/test →
manual push to MyGet by Rick → merge-back into `main`/`support/17.x` → delete release branch) but no
mechanism for marking a shipped version with a git tag or a GitHub Release. Only one stray tag exists
today (`release-17.0.0-beta` on Commerce, predating this process).

## Decisions

1. **Where the mechanism lives:** as a step inside the existing manual post-release flow, not an
   automated CI/Action trigger. Publishing to MyGet is a manual, out-of-band step — nothing in git or
   ADO observes it directly — so the only reliable "this was actually shipped" signal is Rick confirming
   the publish succeeded and then running the merge-back. Tagging happens right there.

2. **Tag convention:** `release-<version>` (e.g. `release-18.0.0`, `release-17.0.0`), matching the
   prefix style of the existing stray tag.

3. **Skill scope:** light versions of the core repo's `release-management` / `post-release-cleanup`
   skills, scoped to what the satellites actually do — no changelogs, no release manifest, no
   multi-product cascade (each satellite is a single product). Full parity with the core repo's
   machinery was explicitly ruled out as out of scope.

4. **Release notes:** `gh release create <tag> --target <branch> --generate-notes` — GitHub's
   auto-generated summary of merged PRs since the previous release. No hand-authored notes.

5. **Backfill scope:** only the July 2026 releases (`release/2026.07.1 → main` @ `18.0.0`,
   `release/2026.07.2 → support/17.x` @ `17.0.0`, all six repos = 12 tags/releases). The June
   `release/2026.06.1` branch turned out to be an ancestor of both `main` and `support/17.x` (the
   pre-v18-split beta baseline, already carrying its own one-off `release-17.0.0-beta` tag) — it never
   went through a real release/publish/merge-back cycle, so it's excluded from backfill.

## New skills (added verbatim to all six satellite repos)

### `/release-management` (light)

1. Ask which version line to release (v17 via `support/17.x` / v18 via `main`).
2. Compute the next `release/2026.MM.N` branch name (check existing `release/*` refs for the current
   month, increment N).
3. Create and check out the release branch.
4. Bump `Directory.Packages.props`: every `Umbraco.Automate`/`.Core`/`.Testing` `PackageVersion` →
   `[X.0.0, X.999.999)` for the target line's major `X`.
5. Bump `version.json` `"version"` → stable `X.0.0` (drop `-beta`/prerelease suffix).
6. Commit both files on the release branch.
7. Tell the user to push and let CI (`azure-pipelines.yml` `release/*` trigger) validate, then publish
   to MyGet manually.

No changelog generation, no release manifest, no dependency cascade — single product per repo, and
these files don't exist in the satellites today.

### `/post-release-cleanup` (light)

Run after the manual MyGet publish is confirmed:

1. Merge the release branch `--no-ff` into its target (`main` for v18, `support/17.x` for v17).
2. Read the version from `version.json` on the merge commit.
3. `git tag release-<version>` on the merge commit; push the tag.
4. `gh release create release-<version> --target <branch> --generate-notes`.
5. Patch-bump `version.json` on the target branch (e.g. `18.0.0` → `18.0.1`) so nightly `--preview.*`
   builds sort above the just-shipped stable version.
6. Delete the release branch, local and remote.

## Backfill plan

For each of the six repos, using the already-identified July merge commits:

| Repo | v18 tag @ commit (main) | v17 tag @ commit (support/17.x) |
|---|---|---|
| Commerce | `release-18.0.0` @ `aa73b94` | `release-17.0.0` @ `b0dc1e7` |
| Engage | `release-18.0.0` @ `751b78b` | `release-17.0.0` @ `477456c` |
| Forms | `release-18.0.0` @ `6d52de5` | `release-17.0.0` @ `2111c33` |
| Workflow | `release-18.0.0` @ `c048303` | `release-17.0.0` @ `6c04458` |
| Deploy | `release-18.0.0` @ `b2ba16d` | `release-17.0.0` @ `ad02522` |
| UIBuilder | `release-18.0.0` @ `9e6a84b` | `release-17.0.0` @ `c64dff9` |

For each: create the tag on the given commit, push it, then `gh release create <tag> --target
<branch> --generate-notes`.

## Out of scope

- Changelog generation (`CHANGELOG.md`) for satellites.
- Release manifests / multi-product cascade logic.
- Automating the MyGet publish step itself.
- Tagging the June `17.0.0-beta` baseline.
