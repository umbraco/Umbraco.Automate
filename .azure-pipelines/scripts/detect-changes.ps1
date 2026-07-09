# ============================================================================
# Umbraco.Automate Monorepo Change Detection Script
# ============================================================================
# Automatically discovers products, analyzes dependencies, and determines
# which products need to be rebuilt based on git changes.
#
# Key Features:
# - Auto-discovers products by folder naming convention (Umbraco.Automate.*)
# - Parses .csproj files to extract actual dependencies
# - Calculates build levels using topological sort
# - Propagates changes to dependent products
# ============================================================================

param(
    [string]$SourceBranch = $env:BUILD_SOURCEBRANCH,
    [string]$SourceVersion = $env:BUILD_SOURCEVERSION,
    [string]$RootPath = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================

function Get-ProductKey {
    <#
    .SYNOPSIS
    Converts a product name to a key used in hashtables.
    .EXAMPLE
    "Umbraco.Automate" -> "core"
    "Umbraco.Automate.OpenIddict" -> "openiddict"
    #>
    param([string]$ProductName)

    if ($ProductName -eq "Umbraco.Automate") { return "core" }
    return ($ProductName -replace "^Umbraco\.Automate\.", "").ToLower()
}

function Get-VariableName {
    <#
    .SYNOPSIS
    Converts a product key to a pipeline variable name.
    .EXAMPLE
    "core" -> "CoreChanged"
    "openiddict" -> "OpeniddictChanged"
    #>
    param([string]$ProductKey)

    $pascalCase = ($ProductKey -split '\.' | ForEach-Object {
        $_.Substring(0,1).ToUpper() + $_.Substring(1).ToLower()
    }) -join ''

    return "${pascalCase}Changed"
}

# ============================================================================
# PRODUCT DISCOVERY
# ============================================================================

function Get-UmbracoAutomateProducts {
    <#
    .SYNOPSIS
    Auto-discovers all Umbraco.Automate products and their subprojects.

    .DESCRIPTION
    Scans the root directory for folders matching "Umbraco.Automate*" pattern,
    finds their main .csproj files, and maps all subprojects to enable
    dependency resolution.
    #>
    param([string]$RootPath)

    Write-Host "Auto-discovering products..." -ForegroundColor Cyan

    $products = @{}
    $productsByName = @{}

    # Find all product folders (Umbraco.Automate.*)
    $productFolders = Get-ChildItem -Path $RootPath -Directory | Where-Object {
        $_.Name -match "^Umbraco\.Automate" -and
        -not $_.Name.StartsWith(".")
    }

    foreach ($folder in $productFolders) {
        $productName = $folder.Name
        $productKey = Get-ProductKey -ProductName $productName

        # Find main project file (meta-package or single project)
        $mainProject = Join-Path $folder.FullName "src\$productName\$productName.csproj"
        if (-not (Test-Path $mainProject)) {
            $possibleProjects = Get-ChildItem -Path (Join-Path $folder.FullName "src") -Filter "$productName.csproj" -Recurse -ErrorAction SilentlyContinue
            if ($possibleProjects) {
                $mainProject = $possibleProjects[0].FullName
            }
        }

        if (-not (Test-Path $mainProject)) {
            Write-Host "  ! Skipped: $productName (no project file found)" -ForegroundColor Yellow
            continue
        }

        Write-Host "  ✓ Found: $productName" -ForegroundColor Green

        # Register product
        $products[$productKey] = @{
            Path = "$($folder.Name)/"
            Variable = Get-VariableName -ProductKey $productKey
            DisplayName = $productName
            ProjectFile = $mainProject
            DependsOn = @()  # Populated later
        }

        # Map product name and all subprojects to this product key
        $productsByName[$productName] = $productKey

        $srcFolder = Join-Path $folder.FullName "src"
        if (Test-Path $srcFolder) {
            Get-ChildItem -Path $srcFolder -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                $subprojectName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
                if (-not $productsByName.ContainsKey($subprojectName)) {
                    $productsByName[$subprojectName] = $productKey
                }
            }
        }
    }

    return @{
        Products = $products
        ProductsByName = $productsByName
    }
}

# ============================================================================
# DEPENDENCY ANALYSIS
# ============================================================================

function Get-ProjectDependencies {
    <#
    .SYNOPSIS
    Extracts Umbraco.Automate dependencies from a .csproj file.

    .DESCRIPTION
    Parses ProjectReference and PackageReference elements to find
    dependencies on other Umbraco.Automate products.
    #>
    param(
        [string]$ProjectPath,
        [hashtable]$ProductsByName
    )

    if (-not (Test-Path $ProjectPath)) { return @() }

    try {
        [xml]$projectXml = Get-Content $ProjectPath -ErrorAction Stop
        $dependencies = @()

        # Find all references (ProjectReference + PackageReference)
        $references = $projectXml.Project.ItemGroup.ProjectReference + $projectXml.Project.ItemGroup.PackageReference

        foreach ($reference in $references) {
            if ($null -eq $reference -or -not $reference.Include) { continue }

            $refName = $null

            # Extract project name from ProjectReference path
            if ($reference.Include -match '([^\\\/]+)\.csproj$') {
                $refName = $Matches[1]
            }
            # Use PackageReference Include directly if it's Umbraco.Automate.*
            elseif ($reference.Include -match '^Umbraco\.Automate') {
                $refName = $reference.Include
            }

            # Map to product key if it's an Umbraco.Automate product
            if ($refName -and $ProductsByName.ContainsKey($refName)) {
                $depKey = $ProductsByName[$refName]
                if ($depKey -and $dependencies -notcontains $depKey) {
                    $dependencies += $depKey
                }
            }
        }

        return $dependencies
    }
    catch {
        Write-Host "  Warning: Failed to parse $ProjectPath - $_" -ForegroundColor Yellow
        return @()
    }
}

function Get-AllProductDependencies {
    <#
    .SYNOPSIS
    Analyzes all subprojects to build the complete dependency graph.
    #>
    param(
        [hashtable]$Products,
        [hashtable]$ProductsByName,
        [string]$RootPath
    )

    Write-Host ""
    Write-Host "Analyzing project dependencies..." -ForegroundColor Cyan

    foreach ($productKey in $Products.Keys) {
        $product = $Products[$productKey]
        $allDeps = @()

        $srcFolder = Join-Path $RootPath ($product.Path.TrimEnd('/')) | Join-Path -ChildPath "src"

        if (Test-Path $srcFolder) {
            # Scan all .csproj files in this product
            Get-ChildItem -Path $srcFolder -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                $deps = Get-ProjectDependencies -ProjectPath $_.FullName -ProductsByName $ProductsByName

                # Merge dependencies (exclude self-references and duplicates)
                foreach ($dep in $deps) {
                    if ($dep -and $dep -ne $productKey -and $allDeps -notcontains $dep) {
                        $allDeps += $dep
                    }
                }
            }
        }

        $product.DependsOn = $allDeps | Sort-Object

        # Log dependencies
        if ($allDeps.Count -gt 0) {
            $depList = ($allDeps | ForEach-Object { $Products[$_].DisplayName }) -join ", "
            Write-Host "  $($product.DisplayName) → $depList" -ForegroundColor Cyan
        }
        else {
            Write-Host "  $($product.DisplayName) → (no dependencies)" -ForegroundColor Gray
        }
    }
}

# ============================================================================
# BUILD ORDER CALCULATION
# ============================================================================

function Get-BuildLevels {
    <#
    .SYNOPSIS
    Calculates build levels using topological sort.

    .DESCRIPTION
    Level 0 = no dependencies
    Level 1 = depends only on Level 0
    Level 2 = depends on Level 1, etc.
    #>
    param([hashtable]$Products)

    $levels = @{}
    $visited = @{}

    function Get-Level {
        param([string]$ProductKey, [hashtable]$Levels, [hashtable]$Visited)

        if ($Visited.ContainsKey($ProductKey)) {
            return $Levels[$ProductKey]
        }

        $Visited[$ProductKey] = $true
        $deps = $Products[$ProductKey].DependsOn

        # No dependencies = Level 0
        if ($null -eq $deps -or @($deps).Count -eq 0) {
            $Levels[$ProductKey] = 0
            return 0
        }

        # Level = max(dependency levels) + 1
        $maxDepLevel = -1
        foreach ($dep in $deps) {
            $depLevel = Get-Level -ProductKey $dep -Levels $Levels -Visited $Visited
            if ($depLevel -gt $maxDepLevel) {
                $maxDepLevel = $depLevel
            }
        }

        $Levels[$ProductKey] = $maxDepLevel + 1
        return $maxDepLevel + 1
    }

    # Calculate level for each product
    foreach ($productKey in $Products.Keys) {
        $null = Get-Level -ProductKey $productKey -Levels $levels -Visited $visited
    }

    return $levels
}

# ============================================================================
# CHANGE DETECTION
# ============================================================================

function Get-LastSuccessfulBuildCommit {
    <#
    .SYNOPSIS
    Returns the source commit SHA of the most recent successful build of this
    pipeline on the same branch, via the Azure DevOps REST API.

    .DESCRIPTION
    Requires SYSTEM_ACCESSTOKEN to be exposed to the script (done by setting
    env: SYSTEM_ACCESSTOKEN: $(System.AccessToken) on the task). Returns $null
    if the API call fails, no previous build exists, or we're not running in
    Azure Pipelines.
    #>
    param([string]$SourceBranch)

    $collectionUri = $env:SYSTEM_COLLECTIONURI
    $project = $env:SYSTEM_TEAMPROJECT
    $definitionId = $env:SYSTEM_DEFINITIONID
    $accessToken = $env:SYSTEM_ACCESSTOKEN
    $currentBuildId = $env:BUILD_BUILDID

    if (-not $collectionUri -or -not $project -or -not $definitionId) {
        Write-Host "    Azure DevOps env vars not set - skipping API lookup" -ForegroundColor Gray
        return $null
    }

    if (-not $accessToken) {
        Write-Host "    SYSTEM_ACCESSTOKEN not available - add 'env: SYSTEM_ACCESSTOKEN: \$(System.AccessToken)' to the task" -ForegroundColor Yellow
        return $null
    }

    $encodedBranch = [System.Uri]::EscapeDataString($SourceBranch)
    $uri = "${collectionUri}${project}/_apis/build/builds?definitions=${definitionId}&branchName=${encodedBranch}&statusFilter=completed&resultFilter=succeeded&`$top=5&api-version=7.1-preview.7"
    $headers = @{ "Authorization" = "Bearer $accessToken" }

    try {
        $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get -ErrorAction Stop

        # Pick the most recent build that isn't the current one
        foreach ($build in $response.value) {
            if ($build.id -ne $currentBuildId -and $build.sourceVersion) {
                Write-Host "    Last successful build: #$($build.id) @ $($build.sourceVersion)" -ForegroundColor Gray
                return $build.sourceVersion
            }
        }

        Write-Host "    No prior successful build found for branch" -ForegroundColor Gray
        return $null
    }
    catch {
        Write-Host "    Azure DevOps API call failed: $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }
}

function Test-SubstantiveChange {
    <#
    .SYNOPSIS
    Determines if a file change is substantive (code/config) or non-substantive (docs/version).

    .DESCRIPTION
    On release/hotfix branches, some file changes represent release preparation
    rather than actual code changes (e.g., updating CHANGELOG dates, bumping versions).
    This function identifies such non-substantive changes.
    #>
    param([string]$FilePath)

    $fileName = Split-Path $FilePath -Leaf

    # Non-substantive files (release prep artifacts)
    $nonSubstantiveFiles = @(
        "CHANGELOG.md",  # Changelog updates (often batch date changes)
        "version.json"   # Version bumps without code changes
    )

    return $nonSubstantiveFiles -notcontains $fileName
}

function Get-AffectedProductsFromPackageProps {
    <#
    .SYNOPSIS
    Determines which products are affected by changes to Directory.Packages.props.

    .DESCRIPTION
    Parses the git diff of Directory.Packages.props to find changed package names,
    then searches .csproj files to find which products reference those packages.
    Returns only the product keys that are actually affected.

    For Directory.Build.props and global.json, all products are affected (returns all keys).
    #>
    param(
        [string]$ComparisonRef,
        [hashtable]$Products,
        [string]$RootPath
    )

    # Parse the diff to extract changed package names
    $diff = git diff "$ComparisonRef..HEAD" -- "Directory.Packages.props" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    Could not diff Directory.Packages.props, assuming all affected" -ForegroundColor Yellow
        return @($Products.Keys)
    }

    $changedPackages = @()
    foreach ($line in $diff) {
        if ($line -is [string] -and $line -match '^[+-].*<PackageVersion\s+Include="([^"]+)"') {
            $packageName = $Matches[1]
            if ($changedPackages -notcontains $packageName) {
                $changedPackages += $packageName
            }
        }
    }

    if ($changedPackages.Count -eq 0) {
        Write-Host "    No package version changes detected in diff" -ForegroundColor Gray
        return @()
    }

    Write-Host "    Changed packages: $($changedPackages -join ', ')" -ForegroundColor Cyan

    # Find which products reference these packages
    $affectedKeys = @()
    foreach ($productKey in $Products.Keys) {
        $product = $Products[$productKey]
        $srcFolder = Join-Path $RootPath ($product.Path.TrimEnd('/')) | Join-Path -ChildPath "src"

        if (-not (Test-Path $srcFolder)) { continue }

        $csprojFiles = Get-ChildItem -Path $srcFolder -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
        $isAffected = $false

        foreach ($csproj in $csprojFiles) {
            if ($isAffected) { break }

            try {
                $content = Get-Content $csproj.FullName -Raw -ErrorAction Stop
                foreach ($pkg in $changedPackages) {
                    if ($content -match [regex]::Escape($pkg)) {
                        $isAffected = $true
                        Write-Host "    ✓ $($product.DisplayName) references $pkg" -ForegroundColor Green
                        break
                    }
                }
            }
            catch {
                # If we can't read a csproj, skip it
            }
        }

        if ($isAffected -and $affectedKeys -notcontains $productKey) {
            $affectedKeys += $productKey
        }
    }

    return $affectedKeys
}

function Get-ChangedProducts {
    <#
    .SYNOPSIS
    Determines which products have changed based on git diff or tag name.
    #>
    param(
        [hashtable]$Products,
        [string]$SourceBranch
    )

    $changed = @{}
    $Products.Keys | ForEach-Object { $changed[$_] = $false }

    # Git diff-based detection with intelligent base selection
    Write-Host "Git diff-based detection" -ForegroundColor Cyan

    $changedFiles = $null
    $comparisonBase = $null

    # Extract the version prefix (e.g. "v18") from the source branch so comparisons
    # target the correct version line's long-term branches (origin/vN/main, origin/vN/dev).
    $versionPrefix = $null
    if ($SourceBranch -match '^refs/heads/(v[0-9]+)/') {
        $versionPrefix = $Matches[1]
    }

    # Determine comparison point based on build context
    if ($env:SYSTEM_PULLREQUEST_TARGETBRANCH) {
        # For PRs: compare against merge-base with target branch
        $targetBranch = $env:SYSTEM_PULLREQUEST_TARGETBRANCH -replace '^refs/heads/', ''
        Write-Host "  PR detected, target branch: $targetBranch" -ForegroundColor Cyan

        $mergeBase = git merge-base "origin/$targetBranch" HEAD 2>&1
        if ($LASTEXITCODE -eq 0) {
            $comparisonBase = $mergeBase.Trim()
            Write-Host "  Comparing PR against merge-base: $comparisonBase" -ForegroundColor Cyan
            $changedFiles = git diff --name-only $comparisonBase HEAD 2>&1
        }
    }
    elseif ($SourceBranch -match '^refs/heads/v[0-9]+/(main|dev)$') {
        # For main/dev pushes: origin/$branchName always points at HEAD after CI's fetch,
        # so a direct git diff would be empty. Ask Azure DevOps for the previous successful
        # build's source version on this branch and diff against that — captures the whole
        # push range (even multi-commit pushes) accurately.
        $branchName = if ($SourceBranch -match '/main$') { 'main' } else { 'dev' }
        Write-Host "  Main/dev branch detected" -ForegroundColor Cyan

        $previousSha = Get-LastSuccessfulBuildCommit -SourceBranch $SourceBranch
        if ($previousSha) {
            # Verify the SHA is reachable — it might not be if history was rewritten
            # or the fetch depth is too shallow.
            $null = git cat-file -e "$previousSha^{commit}" 2>$null
            if ($LASTEXITCODE -eq 0) {
                $comparisonBase = $previousSha
                $changedFiles = git diff --name-only $previousSha HEAD 2>&1
                Write-Host "  Comparing against last successful build: $previousSha" -ForegroundColor Cyan
            }
            else {
                Write-Host "  Previous build SHA $previousSha not reachable - building all products" -ForegroundColor Yellow
                $Products.Keys | ForEach-Object { $changed[$_] = $true }
                return $changed
            }
        }
        else {
            # No previous successful build (first build on branch, API unreachable, etc.)
            # Safer to build everything than miss something.
            Write-Host "  No previous successful build found - building all products" -ForegroundColor Yellow
            $Products.Keys | ForEach-Object { $changed[$_] = $true }
            return $changed
        }
    }
    elseif ($SourceBranch -match '^refs/heads/v[0-9]+/(release|hotfix)/') {
        # Release/hotfix branches: per-product tag-based comparison
        # Each product compares against its own latest release tag
        $branchType = if ($SourceBranch -match '/release/') { "Release" } else { "Hotfix" }
        Write-Host "  $branchType branch detected - using per-product tag comparison" -ForegroundColor Cyan

        # Process each product independently
        foreach ($productKey in $Products.Keys) {
            $product = $Products[$productKey]

            # Find latest tag for this product
            $latestTag = git describe --tags --abbrev=0 --match="$($product.DisplayName)@*" 2>&1

            if ($LASTEXITCODE -ne 0 -or -not ($latestTag -is [string])) {
                Write-Host "  ! $($product.DisplayName): No release tag found, falling back to merge-base with $versionPrefix/main" -ForegroundColor Yellow

                # Fallback: use merge-base with vN/main for this product
                $mergeBase = git merge-base "origin/$versionPrefix/main" HEAD 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $comparisonBase = $mergeBase.Trim()
                    $productChangedFiles = git diff --name-only "$comparisonBase..HEAD" -- $product.Path 2>&1
                }
                else {
                    Write-Host "  ! $($product.DisplayName): Merge-base failed, assuming changed" -ForegroundColor Yellow
                    $changed[$productKey] = $true
                    continue
                }
            }
            else {
                $latestTag = $latestTag.Trim()
                Write-Host "  $($product.DisplayName): Comparing against tag $latestTag" -ForegroundColor Cyan

                # Get changes for this product since its tag
                $productChangedFiles = git diff --name-only "$latestTag..HEAD" -- $product.Path 2>&1
            }

            if ($LASTEXITCODE -ne 0) {
                Write-Host "  ! $($product.DisplayName): git diff failed, assuming changed" -ForegroundColor Yellow
                $changed[$productKey] = $true
                continue
            }

            # Filter to string paths only
            $productChangedFiles = $productChangedFiles | Where-Object { $_ -is [string] }

            # Check for substantive changes
            $hasSubstantiveChange = $false
            foreach ($file in $productChangedFiles) {
                if (Test-SubstantiveChange -FilePath $file) {
                    $changed[$productKey] = $true
                    $hasSubstantiveChange = $true
                    Write-Host "  ✓ $productKey changed (file: $file)" -ForegroundColor Green
                    break  # One substantive change is enough
                }
                else {
                    Write-Host "  ~ $productKey non-substantive change (file: $file)" -ForegroundColor DarkGray
                }
            }

            # Log if only non-substantive changes
            if (-not $hasSubstantiveChange -and $productChangedFiles.Count -gt 0) {
                Write-Host "  ⊘ $productKey skipped (only non-substantive changes)" -ForegroundColor Yellow
            }
        }

        # Check root-level build files for changes
        $anyTag = git describe --tags --abbrev=0 2>&1
        if ($LASTEXITCODE -eq 0) {
            $anyTag = $anyTag.Trim()
            $rootChangedFiles = git diff --name-only "$anyTag..HEAD" 2>&1 | Where-Object { $_ -is [string] }

            foreach ($file in $rootChangedFiles) {
                if ($file -eq "Directory.Packages.props") {
                    # Smart detection: trace changed packages to affected products
                    Write-Host "  Root file changed: Directory.Packages.props - tracing affected products" -ForegroundColor Yellow
                    $affectedKeys = Get-AffectedProductsFromPackageProps -ComparisonRef $anyTag -Products $Products -RootPath (Get-Location).Path
                    foreach ($key in $affectedKeys) {
                        $changed[$key] = $true
                        Write-Host "  ✓ $key affected by Directory.Packages.props change" -ForegroundColor Green
                    }
                }
                elseif ($file -eq "Directory.Build.props" -or $file -eq "global.json") {
                    # These truly affect all products
                    Write-Host "  ✓ Root file changed: $file (affects all products)" -ForegroundColor Yellow
                    $Products.Keys | ForEach-Object { $changed[$_] = $true }
                    break
                }
            }
        }

        return $changed
    }
    else {
        # For feature branches: compare with merge-base against this version line's dev
        $versionPrefix = if ($SourceBranch -match '^refs/heads/(v[0-9]+)/') { $Matches[1] } else { 'v18' }
        Write-Host "  Feature branch detected, comparing against $versionPrefix/dev" -ForegroundColor Cyan

        $mergeBase = git merge-base "origin/$versionPrefix/dev" HEAD 2>&1
        if ($LASTEXITCODE -eq 0) {
            $comparisonBase = $mergeBase.Trim()
            Write-Host "  Using merge-base with $versionPrefix/dev: $comparisonBase" -ForegroundColor Cyan
            $changedFiles = git diff --name-only $comparisonBase HEAD 2>&1
        }
    }

    # Fallback if merge-base approach failed
    if ($LASTEXITCODE -ne 0 -or $null -eq $changedFiles) {
        Write-Host "  Warning: Merge-base comparison failed, falling back to HEAD~1" -ForegroundColor Yellow
        $comparisonBase = "HEAD~1"
        $changedFiles = git diff --name-only HEAD~1 HEAD 2>&1
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Warning: Could not determine changed files, building all products" -ForegroundColor Yellow
        $Products.Keys | ForEach-Object { $changed[$_] = $true }
        return $changed
    }

    Write-Host "  Comparison: $comparisonBase..HEAD" -ForegroundColor Gray

    # Filter out ErrorRecord objects (git warnings/errors) and keep only strings (file paths)
    # Log any warnings for visibility, but don't fail the build
    $gitWarnings = $changedFiles | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
    if ($gitWarnings) {
        Write-Host "  Git warnings (non-critical):" -ForegroundColor DarkYellow
        $gitWarnings | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    }
    $changedFiles = $changedFiles | Where-Object { $_ -is [string] }

    # Check product folders
    # Note: release/hotfix branches use per-product tag comparison and return early above
    foreach ($file in $changedFiles) {
        foreach ($productKey in $Products.Keys) {
            if ($file.StartsWith($Products[$productKey].Path)) {
                $changed[$productKey] = $true
                Write-Host "  ✓ $productKey changed (file: $file)" -ForegroundColor Green
                break
            }
        }
    }

    # Check root-level build files
    foreach ($file in $changedFiles) {
        if ($file -eq "Directory.Packages.props") {
            # Smart detection: trace changed packages to affected products
            Write-Host "  Root file changed: Directory.Packages.props - tracing affected products" -ForegroundColor Yellow
            $affectedKeys = Get-AffectedProductsFromPackageProps -ComparisonRef $comparisonBase -Products $Products -RootPath $RootPath
            foreach ($key in $affectedKeys) {
                $changed[$key] = $true
                Write-Host "  ✓ $key affected by Directory.Packages.props change" -ForegroundColor Green
            }
        }
        elseif ($file -eq "Directory.Build.props" -or $file -eq "global.json") {
            # These truly affect all products
            Write-Host "  ✓ Root file changed: $file (affects all products)" -ForegroundColor Yellow
            $Products.Keys | ForEach-Object { $changed[$_] = $true }
            break
        }
    }

    return $changed
}

function Resolve-ManifestProductKeys {
    <#
    .SYNOPSIS
    Resolves a list of product names to product keys, validating each entry.
    #>
    param(
        [object[]]$ProductNames,
        [string]$ListName,
        [hashtable]$Products,
        [hashtable]$ProductsByName
    )

    $validNames = @($Products.Values | ForEach-Object { $_.DisplayName })
    $keys = @()

    foreach ($item in $ProductNames) {
        if (-not ($item -is [string]) -or [string]::IsNullOrWhiteSpace($item)) {
            throw "release-manifest.json '$ListName' must contain only non-empty strings"
        }
        if (-not ($validNames -contains $item)) {
            throw "release-manifest.json '$ListName' contains unknown product: $item"
        }
        $key = $ProductsByName[$item]
        if ($null -eq $key -or -not $Products.ContainsKey($key)) {
            throw "release-manifest.json '$ListName' product is not a packable product: $item"
        }
        if ($keys -notcontains $key) {
            $keys += $key
        }
    }

    return $keys
}

function Get-ReleasePackageKeys {
    <#
    .SYNOPSIS
    Loads and validates release-manifest.json and returns product keys.

    .DESCRIPTION
    Supports two formats:
    - Array format (legacy):  ["Product1", "Product2"]
    - Object format:          { "include": ["Product1"], "exclude": ["Product2"] }

    The object format adds an "exclude" list for products that have changed but are
    explicitly excluded from the release (e.g., only chore/build changes).

    Returns a hashtable with 'Include' and 'Exclude' keys, each containing an array
    of product keys.
    #>
    param(
        [string]$RootPath,
        [hashtable]$Products,
        [hashtable]$ProductsByName
    )

    $manifestPath = Join-Path $RootPath "release-manifest.json"
    if (-not (Test-Path $manifestPath)) {
        throw "release-manifest.json is required on release branches"
    }

    $raw = Get-Content $manifestPath -Raw
    try {
        $manifest = $raw | ConvertFrom-Json
    }
    catch {
        throw "Failed to parse release-manifest.json: $($_.Exception.Message)"
    }

    if ($null -eq $manifest -or $manifest -is [string]) {
        throw "release-manifest.json must be a JSON array or object with 'include' property"
    }

    $includeList = $null
    $excludeList = @()

    if ($manifest -is [System.Collections.IEnumerable] -and -not ($manifest.PSObject.Properties.Name -contains 'include')) {
        # Legacy array format: ["Product1", "Product2"]
        $includeList = @($manifest)
    }
    elseif ($manifest.PSObject.Properties.Name -contains 'include') {
        # Object format: { "include": [...], "exclude": [...] }
        $includeList = @($manifest.include)
        if ($manifest.PSObject.Properties.Name -contains 'exclude' -and $null -ne $manifest.exclude) {
            $excludeList = @($manifest.exclude)
        }
    }
    else {
        throw "release-manifest.json must be a JSON array or object with 'include' property"
    }

    $includeKeys = Resolve-ManifestProductKeys -ProductNames $includeList -ListName "include" -Products $Products -ProductsByName $ProductsByName
    $excludeKeys = @()
    if ($excludeList.Count -gt 0) {
        $excludeKeys = Resolve-ManifestProductKeys -ProductNames $excludeList -ListName "exclude" -Products $Products -ProductsByName $ProductsByName
    }

    # Validate no overlap
    $overlap = @($includeKeys | Where-Object { $excludeKeys -contains $_ })
    if ($overlap.Count -gt 0) {
        $overlapDisplay = $overlap | ForEach-Object { $Products[$_].DisplayName }
        throw "release-manifest.json has products in both 'include' and 'exclude': $($overlapDisplay -join ', ')"
    }

    if ($includeKeys.Count -eq 0) {
        throw "release-manifest.json must include at least one product"
    }

    return @{
        Include = $includeKeys
        Exclude = $excludeKeys
    }
}

# ============================================================================
# OUTPUT GENERATION
# ============================================================================

function Write-PipelineVariables {
    <#
    .SYNOPSIS
    Outputs Azure DevOps pipeline variables for changed products and build levels.

    .DESCRIPTION
    Outputs the following variables:
    - Per-product: CoreChanged, OpeniddictChanged, etc.
    - Per-level: Level0Changed, Level0Products, Level0Matrix, etc.
    - Global: AnyChanged, AllChangedProducts, MaxLevel
    #>
    param(
        [hashtable]$Products,
        [hashtable]$ChangedProducts,
        [hashtable]$BuildLevels,
        [string]$RootPath
    )

    Write-Host ""
    Write-Host "Setting pipeline variables:" -ForegroundColor Cyan

    # Output per-product variables
    foreach ($productKey in $Products.Keys) {
        $variable = $Products[$productKey].Variable
        $value = $ChangedProducts[$productKey].ToString().ToLower()
        $level = $BuildLevels[$productKey]

        Write-Host "##vso[task.setvariable variable=$variable;isOutput=true]$value"

        $status = if ($ChangedProducts[$productKey]) { "✓ BUILD" } else { "- SKIP" }
        $color = if ($ChangedProducts[$productKey]) { "Green" } else { "Gray" }
        Write-Host "  $status $productKey (level $level)" -ForegroundColor $color
    }

    # Build level-based product lists
    $maxLevel = if ($BuildLevels.Count -gt 0) { ($BuildLevels.Values | Measure-Object -Maximum).Maximum } else { 0 }

    Write-Host ""
    Write-Host "Build levels:" -ForegroundColor Cyan

    # Track all changed products for summary variables
    $allChangedDisplayNames = @()
    $maxChangedLevel = -1

    # Create JSON structure for dynamic pipeline generation (matrix format)
    for ($level = 0; $level -le $maxLevel; $level++) {
        $productsAtLevel = @()
        $changedAtLevel = @()
        $changedDisplayNamesAtLevel = @()
        $matrixJson = @{}

        foreach ($productKey in $Products.Keys) {
            if ($BuildLevels[$productKey] -eq $level) {
                $product = $Products[$productKey]

                # Add to all products at this level
                $productsAtLevel += $productKey

                # If changed, add to matrix JSON with Azure Pipelines matrix format
                if ($ChangedProducts[$productKey]) {
                    $changedAtLevel += $productKey
                    $changedDisplayNamesAtLevel += $product.DisplayName
                    $allChangedDisplayNames += $product.DisplayName
                    $maxChangedLevel = $level

                    # Create matrix key (replace dots and hyphens with underscores)
                    $matrixKey = $product.DisplayName -replace '[.-]', '_'

                    # Check if product has frontend
                    $frontendPath = Join-Path $RootPath $product.Path "src" "$($product.DisplayName).Web.StaticAssets" "Client"
                    $hasFrontend = Test-Path $frontendPath

                    # Check if frontend package exports types (has "types" field in package.json)
                    $hasNpmTypes = $false
                    if ($hasFrontend) {
                        $packageJsonPath = Join-Path $frontendPath "package.json"
                        if (Test-Path $packageJsonPath) {
                            try {
                                $packageJson = Get-Content $packageJsonPath -Raw | ConvertFrom-Json
                                $hasNpmTypes = $null -ne $packageJson.types
                            }
                            catch {
                                Write-Host "    Warning: Failed to parse $packageJsonPath" -ForegroundColor Yellow
                            }
                        }
                    }

                    $matrixJson[$matrixKey] = @{
                        name = $product.DisplayName
                        path = $product.Path.TrimEnd('/')
                        hasFrontend = $hasFrontend.ToString().ToLower()
                        hasNpmTypes = $hasNpmTypes.ToString().ToLower()
                    }
                }
            }
        }

        if ($productsAtLevel.Count -gt 0) {
            $productList = $productsAtLevel -join ", "
            $changedList = if ($changedAtLevel.Count -gt 0) { $changedAtLevel -join ", " } else { "none" }

            Write-Host "  Level $level`: $productList" -ForegroundColor Cyan
            Write-Host "    Changed: $changedList" -ForegroundColor $(if ($changedAtLevel.Count -gt 0) { "Yellow" } else { "Gray" })

            # Set level variables
            $anyChangedAtLevel = ($changedAtLevel.Count -gt 0).ToString().ToLower()
            # Use display names for products list (needed for artifact download)
            $changedDisplayNamesStr = $changedDisplayNamesAtLevel -join ","

            Write-Host "##vso[task.setvariable variable=Level${level}Changed;isOutput=true]$anyChangedAtLevel"
            Write-Host "##vso[task.setvariable variable=Level${level}Products;isOutput=true]$changedDisplayNamesStr"

            # Output matrix JSON for this level
            if ($matrixJson.Count -gt 0) {
                $matrixJsonStr = $matrixJson | ConvertTo-Json -Depth 3 -Compress
                Write-Host "##vso[task.setvariable variable=Level${level}Matrix;isOutput=true]$matrixJsonStr"
                Write-Host "    Matrix JSON: $matrixJsonStr" -ForegroundColor Magenta
            }
            else {
                # Output empty matrix to avoid errors when no products changed at this level
                Write-Host "##vso[task.setvariable variable=Level${level}Matrix;isOutput=true]{}"
            }
        }
    }

    # Output global summary variables
    Write-Host ""
    Write-Host "Summary variables:" -ForegroundColor Cyan

    # AnyChanged - whether any product changed
    $anyChanged = ($allChangedDisplayNames.Count -gt 0).ToString().ToLower()
    Write-Host "##vso[task.setvariable variable=AnyChanged;isOutput=true]$anyChanged"
    Write-Host "  AnyChanged: $anyChanged" -ForegroundColor $(if ($anyChanged -eq "true") { "Green" } else { "Gray" })

    # AllChangedProducts - comma-separated list of all changed product display names
    $allChangedProductsStr = $allChangedDisplayNames -join ","
    Write-Host "##vso[task.setvariable variable=AllChangedProducts;isOutput=true]$allChangedProductsStr"
    Write-Host "  AllChangedProducts: $(if ($allChangedProductsStr) { $allChangedProductsStr } else { '(none)' })" -ForegroundColor Cyan

    # MaxLevel - the highest build level with changed products
    Write-Host "##vso[task.setvariable variable=MaxLevel;isOutput=true]$maxChangedLevel"
    Write-Host "  MaxLevel: $maxChangedLevel" -ForegroundColor Cyan
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

Write-Host "==================================="
Write-Host "Umbraco.Automate Change Detection"
Write-Host "==================================="
Write-Host ""
Write-Host "Source Branch: $SourceBranch"
Write-Host "Source Version: $SourceVersion"
Write-Host "Root Path: $RootPath"
Write-Host ""

# 1. Discover products
$discovery = Get-UmbracoAutomateProducts -RootPath $RootPath
$products = $discovery.Products
$productsByName = $discovery.ProductsByName

# 2. Analyze dependencies
Get-AllProductDependencies -Products $products -ProductsByName $productsByName -RootPath $RootPath

# 3. Calculate build order
$buildLevels = Get-BuildLevels -Products $products

# 4. Detect changes
$changedProducts = Get-ChangedProducts -Products $products -SourceBranch $SourceBranch

# 5. Release/hotfix branch manifest enforcement
$manifestPath = Join-Path $RootPath "release-manifest.json"
if ($SourceBranch -match '^refs/heads/v[0-9]+/release/') {
    Write-Host "Release branch detected - enforcing release-manifest.json" -ForegroundColor Cyan

    $manifest = Get-ReleasePackageKeys -RootPath $RootPath -Products $products -ProductsByName $productsByName
    $releaseKeys = $manifest.Include
    $excludeKeys = $manifest.Exclude

    $changedKeys = @($changedProducts.Keys | Where-Object { $changedProducts[$_] })
    $missingKeys = @($changedKeys | Where-Object { $releaseKeys -notcontains $_ -and $excludeKeys -notcontains $_ })
    if ($missingKeys.Count -gt 0) {
        $missingDisplay = $missingKeys | ForEach-Object { $products[$_].DisplayName }
        throw "release-manifest.json is missing changed products (add to 'include' or 'exclude'): $($missingDisplay -join ', ')"
    }

    if ($excludeKeys.Count -gt 0) {
        $excludeDisplay = $excludeKeys | ForEach-Object { $products[$_].DisplayName }
        Write-Host "Explicitly excluded products: $($excludeDisplay -join ', ')" -ForegroundColor DarkYellow
    }

    # Override change list to match manifest (force pack list)
    $products.Keys | ForEach-Object { $changedProducts[$_] = $false }
    foreach ($key in $releaseKeys) { $changedProducts[$key] = $true }

    $releaseDisplay = $releaseKeys | ForEach-Object { $products[$_].DisplayName }
    Write-Host "Release packages: $($releaseDisplay -join ', ')" -ForegroundColor Yellow
}
elseif ($SourceBranch -match '^refs/heads/v[0-9]+/hotfix/') {
    if (Test-Path $manifestPath) {
        Write-Host "Hotfix branch detected - applying release-manifest.json" -ForegroundColor Cyan

        $manifest = Get-ReleasePackageKeys -RootPath $RootPath -Products $products -ProductsByName $productsByName
        $releaseKeys = $manifest.Include
        $excludeKeys = $manifest.Exclude

        $changedKeys = @($changedProducts.Keys | Where-Object { $changedProducts[$_] })
        $missingKeys = @($changedKeys | Where-Object { $releaseKeys -notcontains $_ -and $excludeKeys -notcontains $_ })
        if ($missingKeys.Count -gt 0) {
            $missingDisplay = $missingKeys | ForEach-Object { $products[$_].DisplayName }
            throw "release-manifest.json is missing changed products (add to 'include' or 'exclude'): $($missingDisplay -join ', ')"
        }

        if ($excludeKeys.Count -gt 0) {
            $excludeDisplay = $excludeKeys | ForEach-Object { $products[$_].DisplayName }
            Write-Host "Explicitly excluded products: $($excludeDisplay -join ', ')" -ForegroundColor DarkYellow
        }

        # Override change list to match manifest (force pack list)
        $products.Keys | ForEach-Object { $changedProducts[$_] = $false }
        foreach ($key in $releaseKeys) { $changedProducts[$key] = $true }

        $releaseDisplay = $releaseKeys | ForEach-Object { $products[$_].DisplayName }
        Write-Host "Release packages: $($releaseDisplay -join ', ')" -ForegroundColor Yellow
    }
    else {
        Write-Host "Hotfix branch detected - no release-manifest.json found, using change detection" -ForegroundColor Gray
    }
}

# 6. Output pipeline variables
Write-PipelineVariables -Products $products -ChangedProducts $changedProducts -BuildLevels $buildLevels -RootPath $RootPath

Write-Host ""
Write-Host "Change detection complete" -ForegroundColor Green

# This script signals genuine failures via `throw` (terminating, non-zero exit).
# Reaching this line means change detection succeeded, so the task must report
# success. Handled, non-fatal native commands can leave a non-zero $LASTEXITCODE
# behind — notably `git describe --tags` returns non-zero on the very first
# release when no tags exist yet (we intentionally fall back to merge-base). The
# PowerShell@2 task runs `exit $LASTEXITCODE`, so that stray code would fail the
# step despite success. Force a clean exit to reflect the script's real outcome.
exit 0
