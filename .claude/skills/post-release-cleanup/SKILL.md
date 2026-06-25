---
name: post-release-cleanup
description: Merges a release or hotfix branch back into its version line's vN/main and vN/dev, bumps version.json on vN/dev so nightly builds produce versions higher than the released version, and optionally deletes the release branch. Creates the next major's vN+1 line and updates the GitHub default branch on a major-version cutover. Use after a release has been deployed and tagged.
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, AskUserQuestion
---

# Post-Release Cleanup

You are the orchestrator for post-release cleanup in the Umbraco.Automate repository.

## Task

After a release has been deployed and tagged by the release pipeline, merge the release/hotfix branch back into `main` and `dev`, bump `version.json` on `dev` so nightly builds produce versions **higher** than the released version, and optionally clean up the branch.

## Why This Matters

Without the version bump on `dev`, NBGV + `Umbraco.GitVersioning.Extensions` produces packages like `1.5.0--preview.4.gabcdef0` which sorts **lower** than the stable `1.5.0` in SemVer — making nightlies useless for testing.

## Workflow

### Phase 1: Detect Release Context

1. **Check current branch** — verify it is `vN/release/*` or `vN/hotfix/*`:
   ```bash
   git branch --show-current
   ```
   If not on a release/hotfix branch, ask the user to specify which branch to process.

   **Extract the version prefix** from the branch — every long-term branch is version-prefixed:
   ```bash
   # e.g. v18/release/2026.06.5 → prefix=v18, major=18
   #      v17/hotfix/2026.06.1  → prefix=v17, major=17
   release_branch=$(git branch --show-current)
   prefix=$(echo "$release_branch" | grep -oE '^v[0-9]+')
   major=$(echo "$prefix" | grep -oE '[0-9]+')
   ```
   If the branch does not start with `vN/`, ask the user which version line to target.

2. **Fetch latest tags and remote state:**
   ```bash
   git fetch origin --tags
   ```

3. **Find product version tags on this branch** that are not yet on `main`:
   ```bash
   # Get the merge-base between the release branch and this line's main
   merge_base=$(git merge-base origin/$prefix/main HEAD)

   # Get all commits on the release branch since the merge-base
   commits=$(git rev-list $merge_base..HEAD)

   # For each product tag matching *@*, check if it points at one of these commits
   for tag in $(git tag --list '*@*'); do
       tag_commit=$(git rev-parse "$tag^{commit}" 2>/dev/null)
       if echo "$commits" | grep -q "$tag_commit"; then
           echo "$tag"
       fi
   done
   ```

4. **Parse product names and versions** from tags (e.g., `Umbraco.Automate@0.2.0` → product=`Umbraco.Automate`, version=`0.2.0`).

5. **Present findings to user** for confirmation:
   ```
   Found released products on this branch:
   - Umbraco.Automate @ 0.2.0
   - Umbraco.Automate.OpenIddict @ 0.2.0

   Proceed with merge and version bump? [Yes/Cancel]
   ```

   If NO tags are found, warn the user:
   ```
   ⚠ No product version tags found on this branch.
   This usually means the release pipeline hasn't run yet, or tags haven't been pushed.

   Options:
   - Wait for the release pipeline to complete and try again
   - Proceed anyway (merge only, skip version bump)
   - Cancel
   ```

### Phase 2: Merge to Main

1. **Confirm with user** before merging.

2. The release branch name and `$prefix` were captured in Phase 1.

3. **Checkout and merge** into this line's main:
   ```bash
   git checkout $prefix/main
   git pull origin $prefix/main
   git merge origin/$release_branch --no-ff -m "Merge $release_branch into $prefix/main"
   ```

4. **Push main:**
   ```bash
   git push origin $prefix/main
   ```

   The post-merge hook will auto-delete `release-manifest.json` if present and commit the cleanup.

### Phase 3: Merge Main to Dev

1. ```bash
   git checkout $prefix/dev
   git pull origin $prefix/dev
   git merge $prefix/main --no-ff -m "Merge $prefix/main into $prefix/dev"
   ```

2. **Handle merge conflicts** — if conflicts occur (likely in `version.json` or `CHANGELOG.md`):
   - For `version.json`: keep the **higher** version (this will be overwritten in Phase 4 anyway)
   - For `CHANGELOG.md`: keep **both** sets of entries (combine)
   - For `release-manifest.json`: delete the file (it should not exist on dev)
   - Ask the user for help with any other conflicts

3. **Push dev:**
   ```bash
   git push origin $prefix/dev
   ```

   The post-merge hook will auto-delete `release-manifest.json` if present and commit the cleanup.

### Phase 4: Bump Versions on Dev

For each released product detected in Phase 1:

1. **Read** the current `<Product>/version.json`

2. **Compute the patch bump:**
   - Stable version: `0.2.0` → `0.2.1`
   - Pre-release with numeric suffix: `1.0.0-beta2` → `1.0.0-beta3`
   - Pre-release without numeric suffix: `1.0.0-alpha` → `1.0.0-alpha.1`

3. **Update** the `"version"` field in `version.json` using the Edit tool

4. **After all products are bumped**, commit and push:
   ```bash
   git add */version.json
   git commit -m "chore(release): Bump dev versions after release

   Products bumped:
   - Umbraco.Automate: 0.2.0 → 0.2.1
   - Umbraco.Automate.OpenIddict: 0.2.0 → 0.2.1

   Co-Authored-By: Claude <noreply@anthropic.com>"

   git push origin $prefix/dev
   ```

### Phase 5: Major Version Cutover (only on a new major)

Run this **only** when the released major is greater than the major of the current GitHub default branch — i.e. this release ships a brand-new CMS major and the `v(N+1)/dev` line does not yet exist.

1. **Get the current default branch major:**
   ```bash
   gh api repos/umbraco/Umbraco.Automate --jq '.default_branch'   # e.g. "v18/dev" → 18
   ```

2. **Compare** with the released `major` from Phase 1. If `major` ≤ default major, **skip this phase**.

3. **Check whether the new line already exists:**
   ```bash
   new_prefix="v$major"
   git ls-remote origin "refs/heads/$new_prefix/dev" "refs/heads/$new_prefix/main"
   ```

4. **Create the missing branches** from the just-released line:
   ```bash
   git push origin $prefix/main:refs/heads/$new_prefix/main
   git push origin $prefix/dev:refs/heads/$new_prefix/dev
   ```

5. **Update the GitHub default branch:**
   ```bash
   gh api repos/umbraco/Umbraco.Automate -X PATCH -f default_branch=$new_prefix/dev --jq '.default_branch'
   ```

6. **Tell the developer** to switch lines:
   ```bash
   git fetch origin && git checkout $new_prefix/dev
   ```

### Phase 6: Cleanup (Optional)

1. **Ask the user** if they want to delete the release/hotfix branch (local + remote):
   ```
   Delete the release branch '$release_branch'?
   - Local and remote
   - Local only
   - Skip (keep branch)
   ```

2. If deleting:
   ```bash
   git branch -d $release_branch
   git push origin --delete $release_branch
   ```

3. **Return to dev branch:**
   ```bash
   git checkout $prefix/dev
   ```

### Phase 7: Summary

Present a summary of everything that was done:

```
✅ Post-release cleanup complete!

Merged:
- $release_branch → $prefix/main
- $prefix/main → $prefix/dev

Version bumps on dev:
- Umbraco.Automate: 0.2.0 → 0.2.1
- Umbraco.Automate.OpenIddict: 0.2.0 → 0.2.1

Branch cleanup: [deleted/kept]

Nightly builds on dev will now produce versions higher than the released versions.
```

## Version Bump Logic

### Stable Versions

Simply increment the patch version:
- `0.2.0` → `0.2.1`
- `1.0.0` → `1.0.1`
- `1.0.3` → `1.0.4`

### Pre-release Versions

Increment the numeric portion of the pre-release identifier:
- `1.0.0-beta2` → `1.0.0-beta3`
- `1.0.0-rc.1` → `1.0.0-rc.2`
- `1.0.0-alpha` → `1.0.0-alpha.1` (append `.1` if no numeric suffix)

## Important Notes

- **Always fetch tags first** — the release pipeline creates tags asynchronously after deploy
- **Use `--no-ff` merges** — preserves the merge commit for clear history
- **Post-merge hooks handle `release-manifest.json` cleanup** — don't manually delete it
- **version.json only has a `"version"` field** — update only that field, preserve all other properties
- **Both `vN/release/*` and `vN/hotfix/*` branches are supported** — the workflow is identical; everything targets the same `vN` line the release branch belongs to
- **If no tags are found**, the user can still proceed with merge-only (skip Phase 4)

## Error Recovery

- If the merge to main fails (conflicts), help the user resolve conflicts before continuing
- If the push fails, check if the branch is protected and advise accordingly
- If version.json has unexpected format, show the user and ask how to proceed
- Never force-push — if push is rejected, pull and retry the merge
