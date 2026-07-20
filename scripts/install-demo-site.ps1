# Unified Demo Site Setup Script
# Creates a shared demo site with Umbraco.Automate

param(
    [switch]$SkipTemplateInstall,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Determine repository root (parent of scripts folder)
$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Split-Path -Parent $ScriptDir)).Path

# Change to repository root to ensure consistent behavior
Push-Location $RepoRoot

Write-Host "=== Umbraco.Automate Demo Site Setup ===" -ForegroundColor Cyan
Write-Host "Working directory: $RepoRoot" -ForegroundColor Gray
Write-Host ""

# Toolchain check — required Node version comes from package.json's engines.node, so this
# stays in lockstep with the npm-side enforcement and the .nvmrc.
$packageJson = Get-Content (Join-Path $RepoRoot "package.json") -Raw | ConvertFrom-Json
$requiredNodeRange = $packageJson.engines.node
if ($requiredNodeRange -match '(\d+)') {
    $requiredNodeMajor = [int]$matches[1]
} else {
    Write-Host "ERROR: Could not parse engines.node ('$requiredNodeRange') from package.json." -ForegroundColor Red
    exit 1
}

$nodeVersionRaw = (node --version 2>$null) -replace '^v', ''
if (-not $nodeVersionRaw) {
    Write-Host "ERROR: Node.js is not installed or not on PATH. package.json requires '$requiredNodeRange'." -ForegroundColor Red
    Write-Host "Install Node $requiredNodeMajor+ (e.g. 'nvm install $requiredNodeMajor && nvm use $requiredNodeMajor') and re-run." -ForegroundColor Yellow
    exit 1
}
$nodeMajor = [int]($nodeVersionRaw -split '\.')[0]
if ($nodeMajor -lt $requiredNodeMajor) {
    Write-Host "ERROR: Node $nodeVersionRaw detected; package.json requires '$requiredNodeRange'." -ForegroundColor Red
    Write-Host "Run 'nvm install $requiredNodeMajor && nvm use $requiredNodeMajor' (or equivalent) before re-running this script." -ForegroundColor Yellow
    exit 1
}
Write-Host "Node $nodeVersionRaw detected (satisfies '$requiredNodeRange')." -ForegroundColor Gray
Write-Host ""

# Detect template version and major from Directory.Packages.props.
# The Umbraco.Cms.Core version (lower bound if a range, or the fixed version) is the right
# template version to scaffold the demo site against.
$packagesPropsPath = Join-Path $RepoRoot "Directory.Packages.props"
if (-not (Test-Path $packagesPropsPath)) {
    Write-Host "ERROR: Could not find $packagesPropsPath" -ForegroundColor Red
    exit 1
}
$packagesContent = Get-Content $packagesPropsPath -Raw
if ($packagesContent -match 'Include="Umbraco\.Cms\.Core" Version="\[([^,\]]+)') {
    $TemplateVersion = $matches[1]
} elseif ($packagesContent -match 'Include="Umbraco\.Cms\.Core" Version="([^"\[]+)"') {
    $TemplateVersion = $matches[1]
} else {
    Write-Host "ERROR: Could not find Umbraco.Cms.Core version in $packagesPropsPath" -ForegroundColor Red
    exit 1
}
$VersionMajor = [int]($TemplateVersion -split '\.')[0]
$IsTemplatePrerelease = $TemplateVersion -match '-'
Write-Host "Target Umbraco.Cms template version: $TemplateVersion (v$VersionMajor)" -ForegroundColor Gray
Write-Host ""

# Versioned demo directory: demos/vN/
$DemoDir = "demos\v$VersionMajor"
$DemoSiteDir = "$DemoDir\Umbraco.Automate.DemoSite"

# Check if demo already exists
if ((Test-Path $DemoDir) -and -not $Force) {
    Write-Host "Demo folder '$DemoDir' already exists. Use -Force to recreate." -ForegroundColor Yellow
    Write-Host "Or open the existing Umbraco.Automate.local.slnx" -ForegroundColor Yellow
    exit 0
}

# Clean up existing demo if Force
if ($Force -and (Test-Path $DemoDir)) {
    Write-Host "Removing existing demo folder '$DemoDir'..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $DemoDir
}

if ($Force -and (Test-Path "Umbraco.Automate.local.slnx")) {
    Remove-Item -Force "Umbraco.Automate.local.slnx"
}

# Step 1: Install Umbraco templates
if (-not $SkipTemplateInstall) {
    Write-Host "Installing Umbraco templates ($TemplateVersion)..." -ForegroundColor Green

    # Uninstall any existing version to avoid conflicts
    Write-Host "Removing any existing Umbraco.Templates installations..." -ForegroundColor Gray
    $installedTemplates = dotnet new uninstall 2>&1 | Out-String
    if ($installedTemplates -match "Umbraco\.Templates") {
        try {
            dotnet new uninstall Umbraco.Templates 2>&1 | Out-Null
        } catch {
            # Ignore errors during uninstall
        }
    }

    if ($IsTemplatePrerelease) {
        # Prerelease templates require the umbracoprereleases MyGet feed to be configured.
        # If not yet configured: dotnet nuget add source https://www.myget.org/F/umbracoprereleases/api/v3/index.json --name UmbracoPreReleases
        Write-Host "NOTE: Prerelease template ($TemplateVersion) requires the umbracoprereleases MyGet source." -ForegroundColor Yellow
    }
    dotnet new install "Umbraco.Templates::$TemplateVersion" --force
}

# Step 2: Create demo folder with build overrides
Write-Host "Creating demo folder '$DemoDir'..." -ForegroundColor Green
New-Item -ItemType Directory -Path $DemoDir -Force | Out-Null

# Disable package validation for demo folder
$directoryBuildPropsSource = Join-Path $ScriptDir "templates\Directory.Build.props"
Copy-Item -Path $directoryBuildPropsSource -Destination "$DemoDir\Directory.Build.props" -Force

# Disable central package management for demo folder
$directoryPackagesPropsSource = Join-Path $ScriptDir "templates\Directory.Packages.props"
Copy-Item -Path $directoryPackagesPropsSource -Destination "$DemoDir\Directory.Packages.props" -Force

# Step 3: Create the Umbraco demo site
Write-Host "Creating Umbraco demo site..." -ForegroundColor Green
Push-Location $DemoDir
dotnet new umbraco --force -n "Umbraco.Automate.DemoSite" --friendly-name "Administrator" --email "admin@example.com" --password "password1234" --development-database-type SQLite
Pop-Location

# Step 3.1: Install Clean starter kit
# Clean's major version does not match the CMS major — map explicitly and add
# new entries as majors are released. Floating patterns keep multi-version installs correct:
#   17 -> 7.*   (stable)
#   18 -> 8.*-* (the CMS-v18-compatible Clean, e.g. 8.0.0-rc1, is still prerelease)
$cleanVersionMap = @{
    "17" = "7.*"
    "18" = "8.*-*"
}
$cleanVersion = $cleanVersionMap["$VersionMajor"]
Write-Host "Installing Clean starter kit..." -ForegroundColor Green
Push-Location $DemoSiteDir
if ($cleanVersion) {
    dotnet add package Clean --version $cleanVersion
} else {
    Write-Host "Warning: No Clean version mapping for v$VersionMajor, using latest stable." -ForegroundColor Yellow
    dotnet add package Clean
}
Pop-Location

# Step 3.2: Set fixed port for consistent development
Write-Host "Configuring fixed port (44380)..." -ForegroundColor Green
$launchSettingsSource = Join-Path $ScriptDir "templates\launchSettings.json"
$launchSettingsPath = "$DemoSiteDir\Properties\launchSettings.json"
New-Item -ItemType Directory -Path (Split-Path $launchSettingsPath) -Force | Out-Null
Copy-Item -Path $launchSettingsSource -Destination $launchSettingsPath -Force

# Step 3.3: Add NamedPipeListenerComposer for HTTP over named pipes
Write-Host "Adding NamedPipeListenerComposer for HTTP over named pipes..." -ForegroundColor Green
$composerSourcePath = Join-Path $ScriptDir "templates\NamedPipeListenerComposer.cs"
$composerDestPath = "$DemoSiteDir\Composers\NamedPipeListenerComposer.cs"
New-Item -ItemType Directory -Path (Split-Path $composerDestPath) -Force | Out-Null
Copy-Item -Path $composerSourcePath -Destination $composerDestPath -Force

# Step 3.4: Point Umbraco.Automate at the CMS database
# Automate needs its own connection string (defaults to umbracoAutomateDbDSN) or it throws
# on first run. For the demo we reuse the CMS SQLite connection (umbracoDbDSN) via
# UseNamedConnectionString so a single database backs both CMS and Automate. This must be
# configured before the first run, otherwise startup fails.
Write-Host "Configuring Umbraco.Automate to share the CMS database..." -ForegroundColor Green
$devSettingsPath = "$DemoSiteDir\appsettings.Development.json"
$devSettings = Get-Content $devSettingsPath -Raw | ConvertFrom-Json
if (-not $devSettings.Umbraco) {
    $devSettings | Add-Member -NotePropertyName "Umbraco" -NotePropertyValue ([PSCustomObject]@{})
}
if (-not $devSettings.Umbraco.Automate) {
    $devSettings.Umbraco | Add-Member -NotePropertyName "Automate" -NotePropertyValue ([PSCustomObject]@{})
}
$devSettings.Umbraco.Automate | Add-Member -NotePropertyName "UseNamedConnectionString" -NotePropertyValue "umbracoDbDSN" -Force
$devSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $devSettingsPath -Encoding utf8 -Force

# Step 4: Create unified solution
Write-Host "Creating unified solution..." -ForegroundColor Green
dotnet new sln -n "Umbraco.Automate.local" --force

# Helper function to add all projects from a product's src and tests folders
function Add-ProductProjects {
    param(
        [string]$ProductFolder,
        [string]$SolutionFolder
    )

    $count = 0
    foreach ($sub in @("src", "tests")) {
        $subPath = Join-Path $ProductFolder $sub
        if (Test-Path $subPath) {
            $projects = Get-ChildItem -Path $subPath -Filter "*.csproj" -Recurse
            foreach ($proj in $projects) {
                Write-Host "  Adding $($proj.Name)" -ForegroundColor Gray
                dotnet sln "Umbraco.Automate.local.slnx" add $proj.FullName --solution-folder $SolutionFolder 2>$null
                $count++
            }
        }
    }
    Write-Host "  Added $count projects" -ForegroundColor DarkGreen
}

# Step 5: Add Core projects
Write-Host "Adding Umbraco.Automate projects..." -ForegroundColor Green
Add-ProductProjects -ProductFolder "Umbraco.Automate" -SolutionFolder "Core"

# Step 6: Add OpenIddict projects
Write-Host "Adding Umbraco.Automate.OpenIddict projects..." -ForegroundColor Green
Add-ProductProjects -ProductFolder "Umbraco.Automate.OpenIddict" -SolutionFolder "OpenIddict"

# Step 7: Add Slack projects
Write-Host "Adding Umbraco.Automate.Slack projects..." -ForegroundColor Green
Add-ProductProjects -ProductFolder "Umbraco.Automate.Slack" -SolutionFolder "Slack"

# Step 8: Add demo site to solution
Write-Host "Adding demo site to solution..." -ForegroundColor Green
dotnet sln "Umbraco.Automate.local.slnx" add "$DemoSiteDir/Umbraco.Automate.DemoSite.csproj" --solution-folder "Demo"

# Step 7: Add project references to demo site
Write-Host "Adding project references to demo site..." -ForegroundColor Green
$demoProject = "$DemoSiteDir/Umbraco.Automate.DemoSite.csproj"

# Core references (Startup + Web.StaticAssets)
dotnet add $demoProject reference "Umbraco.Automate/src/Umbraco.Automate.Startup/Umbraco.Automate.Startup.csproj"
dotnet add $demoProject reference "Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Umbraco.Automate.Web.StaticAssets.csproj"

# OpenIddict add-on
if (Test-Path "Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.csproj") {
    dotnet add $demoProject reference "Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.csproj"
}

# Slack add-on
if (Test-Path "Umbraco.Automate.Slack/src/Umbraco.Automate.Slack/Umbraco.Automate.Slack.csproj") {
    dotnet add $demoProject reference "Umbraco.Automate.Slack/src/Umbraco.Automate.Slack/Umbraco.Automate.Slack.csproj"
}

Write-Host ""
Write-Host "=== Setup Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Solution: Umbraco.Automate.local.slnx" -ForegroundColor Cyan
Write-Host "Demo site: $DemoSiteDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Credentials:" -ForegroundColor Yellow
Write-Host "  Email: admin@example.com"
Write-Host "  Password: password1234"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open Umbraco.Automate.local.slnx in your IDE"
Write-Host "  2. Build the solution"
Write-Host "  3. Run the Umbraco.Automate.DemoSite project"
Write-Host ""

# Restore original directory
Pop-Location
