---
name: release-management
description: Orchestrates the complete release preparation process - detects changed products, analyzes commits for version bumps, updates version.json files, generates manifests and changelogs, and creates release branches. Use when preparing a new release.
allowed-tools: Bash, Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion
---

# Release Manager

You are the orchestrator for preparing releases in the Umbraco.Automate repository.

## Task

Guide users through the complete release preparation process:

1. **Detect changed products** since their last release tags
2. **Analyze commits** to recommend version bumps (major/minor/patch)
3. **Confirm versions** with the user
4. **Update Directory.Packages.props** inter-product dependency ranges (with user approval)
5. **Create release branch** (e.g., `release/2026.02.1`) and switch to it
6. **Dependency validation** - Check for cross-product conflicts
7. **Update version.json** files for each product
8. **Generate release-manifest.json** via `/release-manifest-management`
9. **Generate CHANGELOG.md** files via `/changelog-management`
10. **Changelog review** - Review generated changelogs for quality and completeness
11. **Validate** all files are consistent
12. **Commit all changes** to the release branch

## Workflow

### Phase 1: Change Detection

1. **Find all products** - Discover Umbraco.Automate* directories at repo root

2. **For each product**, detect changes since last release:
    ```bash
    # Find the most recent release tag for this product
    git tag --list "Umbraco.Automate@*" --sort=-version:refname | head -n1

    # Get commits affecting this product since that tag
    git log <tag>..HEAD --oneline -- <ProductFolder>/

    # Exclude non-substantive changes (CHANGELOG.md, version.json)
    git diff <tag>..HEAD --name-only -- <ProductFolder>/ | grep -v 'CHANGELOG.md\|version.json'
    ```

3. **Present changed products** to user:
    ```
    Detected changes since last release:

    ┌───────────────────────────────┬──────────┬─────────────────────────────┐
    │ Product                       │ Last Tag │ Changes                     │
    ├───────────────────────────────┼──────────┼─────────────────────────────┤
    │ Umbraco.Automate              │ 0.1.0    │ 12 commits (3 feat, 2 fix)  │
    │ Umbraco.Automate.OpenIddict   │ 0.1.0    │ 3 commits (1 fix)           │
    │ Umbraco.Automate.Slack        │ 0.1.0    │ 5 commits (1 BREAKING)      │
    └───────────────────────────────┴──────────┴─────────────────────────────┘
    ```

### Phase 2: Version Bump Analysis

For each changed product:

1. **Analyze commit types** since last tag:
    ```bash
    # Get all commits with their messages
    git log <tag>..HEAD --pretty=format:"%s" -- <ProductFolder>/
    ```

2. **Determine bump level** based on conventional commits:
    - **BREAKING CHANGE** in body or exclamation mark after scope → **Major** (1.0.0 → 2.0.0)
    - feat: or feat(scope): → **Minor** (1.0.0 → 1.1.0)
    - fix:, perf: → **Patch** (1.0.0 → 1.0.1)
    - Only docs:, chore:, refactor: → **No bump** (but user can override)

3. **Read current version** from `<Product>/version.json`

4. **Calculate new version**:
    - Major: Increment X in X.Y.Z, reset Y and Z to 0
    - Minor: Increment Y in X.Y.Z, reset Z to 0
    - Patch: Increment Z in X.Y.Z

5. **Present recommendations**:
    ```
    Version bump recommendations:

    ┌───────────────────────────────┬──────────┬──────────┬─────────────────────────────┐
    │ Product                       │ Current  │ Proposed │ Reason                      │
    ├───────────────────────────────┼──────────┼──────────┼─────────────────────────────┤
    │ Umbraco.Automate              │ 0.1.0    │ 0.2.0    │ 3 feat, 2 fix commits       │
    │ Umbraco.Automate.OpenIddict   │ 0.1.0    │ 0.1.1    │ 1 fix commit                │
    │ Umbraco.Automate.Slack        │ 0.1.0    │ 1.0.0    │ 1 BREAKING CHANGE           │
    └───────────────────────────────┴──────────┴──────────┴─────────────────────────────┘
    ```

### Phase 3: Version Confirmation

Use **AskUserQuestion** to confirm or adjust versions:

- **Default option**: "Use recommended versions (above)"
- **Alternative options**:
    - "Downplay breaking changes to minor" - Treat breaking changes as minor bumps (X.Y.0 → X.Y+1.0 instead of X+1.0.0)
    - "Adjust individual versions" - Manually specify version for each product
    - "Cancel release preparation"

**If user chooses "Downplay breaking changes to minor":**
- For all products with major bumps (X.Y.Z → X+1.0.0), change to minor bumps (X.Y.Z → X.Y+1.0)
- Keep all other bumps (minor, patch) as-is
- Show updated version table and confirm

**If user chooses "Adjust individual versions":**
- For each product, ask for custom version
- Validate version format (X.Y.Z)
- Warn if version doesn't follow semver conventions

### Phase 4: Update Inter-Product Dependency Ranges (.NET)

After confirming versions, update the `Directory.Packages.props` file to reflect new minimum version requirements **only for products with breaking changes**.

**Important:** Only update dependency ranges when there's a breaking change that requires dependent packages to update. If dependent packages can continue working with the previous version, keep the existing range.

**Workflow:**

1. **Read current Directory.Packages.props**:
   ```bash
   # Read the root Directory.Packages.props file
   cat Directory.Packages.props
   ```

2. **Identify products with breaking changes**:
   - Look for products with BREAKING CHANGE in commit bodies
   - Look for commits with `!` after scope (e.g., `feat!:`, `refactor!:`)
   - Track whether breaking changes were downplayed to minor (still breaking!)
   - **Only these products need dependency range updates**

3. **Determine which ranges need updating**:
   - Look for `<PackageVersion Include="Umbraco.Automate.*" Version="[X.Y.Z, X.999.999)" />` entries
   - **Only update ranges for products with breaking changes**:
     - **Major bump** (X.Y.Z → X+1.0.0): Update both bounds: `[X+1.0.0, X+1.999.999)`
     - **Minor bump from downplayed breaking change** (X.Y.Z → X.Y+1.0): Update lower bound only: `[X.Y+1.0, X.999.999)`
   - **Do NOT update ranges for products without breaking changes** (feat/fix only bumps)

4. **Present proposed changes** to user (if any breaking changes detected):
   ```
   Directory.Packages.props updates:

   Breaking changes detected in:
   - Umbraco.Automate (BREAKING CHANGE: removed legacy API)

   The following dependency ranges will be updated:
   - Umbraco.Automate.Core: [0.1.0, 0.999.999) → [0.2.0, 0.999.999)

   Products without breaking changes (OpenIddict) will keep existing ranges.
   ```

5. **Ask for approval** using AskUserQuestion (only if breaking changes detected):
   - **Default option**: "Update dependency ranges for breaking changes (recommended)"
   - **Alternative options**:
     - "Skip dependency updates" - Continue without updating ranges
     - "Adjust manually later" - Skip now, remind user to update manually

6. **If no breaking changes detected**:
   ```
   ✓ No breaking changes detected - dependency ranges remain unchanged
   ```

7. **If approved, update the file**:
   ```bash
   # Use Edit tool to update Directory.Packages.props
   ```

8. **Confirm updates**:
   ```
   ✓ Updated inter-product dependency ranges in Directory.Packages.props
   ```

**Important Notes:**
- **Conservative approach**: Only update ranges when there are breaking changes
- Products without breaking changes keep their existing ranges (allowing flexibility)
- Only the lower bound changes for minor bumps from downplayed breaking changes
- Both bounds change for true major bumps (X.0.0 → X+1.0.0)
- These changes will be staged and committed with other release files
- If unsure, err on the side of NOT updating (keeps more flexibility for consumers)

### Phase 5: Create Release Branch

**IMPORTANT:** Create the release branch BEFORE making any file changes.

**Branch Naming Convention:**

Per CONTRIBUTING.md, the **recommended** convention is calendar-based with incrementing numbers:
- `release/YYYY.MM.N` - Year, month, and incrementing release number
- Example: `release/2026.02.1` for the first February 2026 release
- Example: `release/2026.02.2` for the second February 2026 release

This is independent from product version numbers (which follow semantic versioning). A single release branch like `release/2026.02.1` can contain multiple products at different versions (e.g., Core@0.2.0, OpenIddict@0.1.1, Slack@1.0.0).

**Workflow:**

1. **Determine current date** - Get current year and month for default branch name:
   ```bash
   date +"%Y.%m"
   ```

2. **Find next release number** - Check existing date-based release tags:
   ```bash
   git fetch --tags
   git tag --list "2026.02.*" --sort=-version:refname
   ```

3. **Ask user for branch name**:
   ```
   Create release branch using recommended calendar naming?

   Latest release tag for February 2026: 2026.02.2
   Suggested branch name: release/2026.02.3

   Options:
   - Use suggested name (release/2026.02.3)
   - Enter custom name
   - Cancel
   ```

4. **Create and checkout branch**:
    ```bash
    git checkout -b release/<name>
    ```

### Phase 6: Dependency Validation

Check for cross-product dependency issues:

1. **Find dependency ranges** in `Directory.Packages.props`:
    ```bash
    grep -r "Umbraco.Automate" Directory.Packages.props
    ```

2. **Warn about breaking changes**:
    ```
    ⚠️  Warning: Version conflict detected

    Umbraco.Automate → 1.0.0 (major bump)
    Umbraco.Automate.Slack requires [0.1.0, 0.999.999)

    Recommendations:
    - Include Umbraco.Automate.Slack in this release and update its dependency
    - Or: Keep Umbraco.Automate at 0.x for this release
    ```

### Phase 7: Update version.json Files

For each product with confirmed version:

1. **Read current version.json**
2. **Update version field** using Edit tool
3. **Verify update** by reading the file back

### Phase 8: Generate Release Manifest

Invoke `/release-manifest-management` skill with product list:
- Build comma-separated list of all products being released
- Pass via `--products="Product1,Product2,..."` parameter
- Example: `--products="Umbraco.Automate,Umbraco.Automate.OpenIddict"`
- Skill will generate manifest automatically without prompting

### Phase 9: Generate Changelogs

For each product in the manifest:

1. **Invoke `/changelog-management`** skill:
    ```bash
    /changelog-management --product=<ProductName> --version=<Version>
    ```

2. **Verify changelog** was generated correctly

### Phase 9.5: Changelog Review

After generating changelogs, review each product's new version entry for quality and completeness.

#### Step 1: Noise Detection

For each product's changelog entry, check for and flag:

1. **Internal-only entries that shouldn't be public:**
   - `refactor` sections — these are hidden from changelogs per convention
   - Test-only fixes (e.g., "Fix failing tests") — not user-facing
   - Build/CI changes (e.g., "Exclude dev files from NuGet package")

2. **Leaked metadata:**
   - `Co-Authored-By` lines in commit bodies that leaked into changelog entries
   - PR merge commit noise
   - Internal tool references (e.g., "after /simplify review")

3. **Duplicate cross-product entries:**
   - Same commit appearing in multiple product changelogs — verify it's actually relevant to each product

#### Step 2: Completeness Check

For each product being released, verify the changelog captured all meaningful changes:

1. **Compare changelog entries against actual file changes:**
   ```bash
   git diff <tag>..HEAD --name-only -- <ProductFolder>/ | grep -v 'CHANGELOG.md\|version.json'
   git log <tag>..HEAD --pretty=format:"%h %s" -- <ProductFolder>/
   ```

2. **Look for missing entries** — commits scoped to other products that touched this product's files

3. **For each missing change, determine if it's changelog-worthy:**
   - Does it change user-facing behavior? → Add a manually written entry
   - Is it only adapting to a dependency change? → Skip (internal plumbing)

#### Step 3: Empty Changelog Assessment

For products with **empty changelog entries** (version header but no content):

1. **Review what actually changed** in the product directory
2. **Categorize the changes:**
   - **Build/tooling only** → Recommend dropping from release
   - **Dependency adaptation** → Recommend dropping OR add a brief entry
   - **Real features/fixes** missed by generator → Add entries manually

3. **If recommending to drop a product**, remind about the release manifest:
   - Move it from `include` to `exclude` in `release-manifest.json`
   - Revert its `version.json` to the pre-release value

#### Step 4: Present Review

Present findings to the user organized by severity:

```
Changelog Review Results:

🔴 Issues requiring attention:
- [Product]: Empty changelog — only build changes detected, recommend dropping
- [Product]: Missing entry for [feature] — cross-product commit scoped elsewhere

🟡 Noise to clean up:
- [Product]: Refactor section should be removed
- [Product]: Co-Authored-By leaked into breaking change body

✅ Clean:
- [Product]: Changelog looks good
```

#### Step 5: Apply Fixes

After user confirms:

1. **Remove noise entries** — Edit changelogs to remove flagged items
2. **Add missing entries** — Write manually crafted entries for missed changes
3. **Drop products** — Update release manifest, revert versions
4. **Re-validate** — Ensure all changelogs still have content after cleanup

### Phase 10: Validation

Verify all files are consistent:

1. **Check version.json** matches intended versions
2. **Check CHANGELOG.md** exists and has correct version header
3. **Check release-manifest.json** includes all intended products
4. **Report any issues** to user

### Phase 11: Commit Changes

**All work has been done on the release branch.** Now commit everything:

1. **Stage all changes**:
    ```bash
    git add release-manifest.json
    git add Directory.Packages.props
    git add */version.json
    git add */CHANGELOG.md
    ```

2. **Create commit**:
    ```bash
    git commit -m "chore(release): Prepare release 2026.02.1

    Updated products:
    - Umbraco.Automate: 0.1.0 → 0.2.0
    - Umbraco.Automate.OpenIddict: 0.1.0 → 0.1.1
    - Umbraco.Automate.Slack: 0.1.0 → 1.0.0

    Co-Authored-By: Claude <noreply@anthropic.com>"
    ```

3. **Show summary**:
    ```
    ✓ Release branch created: release/2026.02.1
    ✓ Updated 3 products:
      - Umbraco.Automate: 0.1.0 → 0.2.0
      - Umbraco.Automate.OpenIddict: 0.1.0 → 0.1.1
      - Umbraco.Automate.Slack: 0.1.0 → 1.0.0
    ✓ Generated changelogs
    ✓ All changes committed

    Next steps:
    - Review the changes: git show HEAD
    - Push to remote: git push -u origin release/2026.02.1
    - Create PR to merge into main
    - CI will validate and build packages
    ```

## Important Notes

- Always run from repository root
- **Branch naming**: Use calendar-based naming `release/YYYY.MM.N` (recommended)
  - Independent from product versions (multiple products = different versions in one release)
  - N is an incrementing number for each release in that month (1, 2, 3, etc.)
- Use conventional commit analysis for version recommendations
- Validate cross-product dependencies
- This skill orchestrates `/release-manifest-management` and `/changelog-management`
- Creates commits following conventional commit format
- Release branches trigger CI validation and packaging

## Version Bump Decision Logic

```
Priority (highest first):
1. BREAKING CHANGE in commit body → Major
2. ! after scope (e.g., feat!:) → Major
3. feat: or feat(<scope>): → Minor
4. fix: or perf: → Patch
5. Only docs/chore/refactor → Ask user (default: patch)
```

## Cross-Product Dependency Check

Read `Directory.Packages.props` to detect version ranges:

```xml
<PackageVersion Include="Umbraco.Automate.Core" Version="[0.1.0, 0.999.999)" />
```

If bumping Core to 1.0.0, warn about all products with `[0.x, 0.999.999)` ranges.

## Example Flow

```
User invokes: /release-management

Phase 1: Detect changes
You scan git history and show:
- Umbraco.Automate: 12 commits since 0.1.0
- Umbraco.Automate.OpenIddict: 3 commits since 0.1.0
- Umbraco.Automate.Slack: 5 commits since 0.1.0

Phase 2: Analyze versions
You show recommendations:
- Umbraco.Automate: 0.1.0 → 0.2.0 (minor - 3 feat commits)
- Umbraco.Automate.OpenIddict: 0.1.0 → 0.1.1 (patch - 1 fix)
- Umbraco.Automate.Slack: 0.1.0 → 1.0.0 (major - BREAKING CHANGE)

Phase 3: Confirm versions
You ask: Use these versions?
User confirms

Phase 4: Update Directory.Packages.props
You read Directory.Packages.props
You identify products with BREAKING CHANGES (Slack only)
You present proposed dependency range updates
User approves updates

Phase 5: Create release branch
You fetch tags, find latest date tag
You suggest: release/2026.04.1
You create the branch and switch to it

Phase 6: Check dependencies
You check Directory.Packages.props
All ranges compatible

Phase 7: Update version.json
You edit all three version.json files on the release branch

Phase 8: Generate manifest
You invoke /release-manifest-management --products="Umbraco.Automate,Umbraco.Automate.OpenIddict,Umbraco.Automate.Slack"

Phase 9: Generate changelogs
You invoke /changelog-management for each product

Phase 9.5: Changelog review
You review each product's changelog for noise and completeness
You present review results to user
User confirms fixes
You apply any needed cleanup

Phase 10: Validate
You verify all files are correct

Phase 11: Commit changes
You commit all changes to the release branch
You show summary and next steps
```

## Error Handling

- **No changes detected**: Ask user if they want to proceed anyway (manual version bump)
- **Git tag not found**: Fall back to comparing with main branch
- **Invalid version.json**: Report error and ask user to fix manually
- **Changelog generation fails**: Report error but continue with other products
- **Dependency conflict**: Warn user but allow them to proceed
