# Package Test Site Setup Script
# Creates a fresh Umbraco site with Umbraco.Automate packages from nightly/prerelease/release feeds

param(
    [string]$SiteName = "Umbraco.Automate.PackageTestSite",
    [ValidateSet("nightly", "prereleases", "release")]
    [string]$Feed = "nightly",
    [switch]$SkipTemplateInstall,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Feed URLs
$FeedUrls = @{
    "nightly" = "https://www.myget.org/F/umbraconightly/api/v3/index.json"
    "prereleases" = "https://www.myget.org/F/umbracoprereleases/api/v3/index.json"
    "release" = "https://api.nuget.org/v3/index.json"
}

$FeedUrl = $FeedUrls[$Feed]

# Determine repository root (parent of scripts folder)
$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Split-Path -Parent $ScriptDir)).Path

# Change to repository root to ensure consistent behavior
Push-Location $RepoRoot

# Detect major version from the Umbraco.Cms.Core version in Directory.Packages.props
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

Write-Host "=== Umbraco.Automate Feed Test Site Setup ===" -ForegroundColor Cyan
Write-Host "Working directory: $RepoRoot" -ForegroundColor Gray
Write-Host "Feed: $Feed ($FeedUrl)" -ForegroundColor Gray
Write-Host "Site name: $SiteName" -ForegroundColor Gray
Write-Host "Version: v$VersionMajor (from Directory.Packages.props)" -ForegroundColor Gray
Write-Host ""

# Check if specific site folder already exists
$sitePath = "demos\v$VersionMajor\$SiteName"
if ((Test-Path $sitePath) -and -not $Force) {
    Write-Host "Site folder '$sitePath' already exists. Use -Force to recreate." -ForegroundColor Yellow
    exit 0
}

# Clean up existing site if Force
if ($Force -and (Test-Path $sitePath)) {
    Write-Host "Removing existing site folder '$sitePath'..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $sitePath
}

# Clear NuGet cache for pre-release feeds (nightly builds change frequently)
# Skip cache clear for release feed (stable packages don't change)
if ($Feed -ne "release") {
    Write-Host "Clearing NuGet cache to fetch fresh packages..." -ForegroundColor Green
    dotnet nuget locals all --clear
}

# Step 1: Install Umbraco templates
if (-not $SkipTemplateInstall) {
    $IsTemplatePrerelease = $TemplateVersion -match '-'
    Write-Host "Installing Umbraco templates ($TemplateVersion)..." -ForegroundColor Green
    if ($IsTemplatePrerelease) {
        Write-Host "NOTE: Prerelease template ($TemplateVersion) requires the umbracoprereleases MyGet source." -ForegroundColor Yellow
    }
    dotnet new install "Umbraco.Templates::$TemplateVersion" --force
}

# Step 2: Create demo folder
Write-Host "Creating demo folder 'demos\v$VersionMajor'..." -ForegroundColor Green
New-Item -ItemType Directory -Path "demos\v$VersionMajor" -Force | Out-Null

# Step 3: Create the Umbraco site
Write-Host "Creating Umbraco site '$SiteName'..." -ForegroundColor Green
Push-Location "demos\v$VersionMajor"
dotnet new umbraco --force -n $SiteName --friendly-name "Administrator" --email "admin@example.com" --password "password1234" --development-database-type SQLite
Pop-Location

# Step 4: Add dedicated Automate connection string
# Umbraco.Automate requires umbracoAutomateDbDSN — add it to appsettings.Development.json
Write-Host "Adding Umbraco Automate connection string..." -ForegroundColor Green
$devSettingsPath = "demos\v$VersionMajor\$SiteName\appsettings.Development.json"
$devSettings = Get-Content $devSettingsPath -Raw | ConvertFrom-Json

# Ensure ConnectionStrings section exists
if (-not $devSettings.ConnectionStrings) {
    $devSettings | Add-Member -NotePropertyName "ConnectionStrings" -NotePropertyValue ([PSCustomObject]@{})
}

# Add the dedicated Automate connection string (SQLite, separate database file)
$devSettings.ConnectionStrings | Add-Member -NotePropertyName "umbracoAutomateDbDSN" `
    -NotePropertyValue "Data Source=|DataDirectory|/UmbracoAutomate.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True" -Force
$devSettings.ConnectionStrings | Add-Member -NotePropertyName "umbracoAutomateDbDSN_ProviderName" `
    -NotePropertyValue "Microsoft.Data.Sqlite" -Force

$devSettings | ConvertTo-Json -Depth 10 | Out-File -FilePath $devSettingsPath -Encoding utf8 -Force

# Step 5: Install Clean starter kit
Write-Host "Installing Clean starter kit..." -ForegroundColor Green
Push-Location "demos\v$VersionMajor\$SiteName"
dotnet add package Clean
Pop-Location

# Step 6: Configure NuGet sources and PackageSourceMapping
Write-Host "Configuring NuGet sources and package routing..." -ForegroundColor Green
Push-Location "demos\v$VersionMajor\$SiteName"

# Determine feed name
$feedName = switch ($Feed) {
    "nightly" { "UmbracoNightly" }
    "prereleases" { "UmbracoPreReleases" }
    "release" { "NuGet.org" }
}

# Create complete nuget.config with sources and PackageSourceMapping
$nugetConfig = "nuget.config"

# For release feed, use simpler config (NuGet.org only)
if ($Feed -eq "release") {
    Write-Host "Creating nuget.config for NuGet.org..." -ForegroundColor Gray
    $configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
} else {
    Write-Host "Creating nuget.config with package source mapping..." -ForegroundColor Gray
    $configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="$feedName" value="$FeedUrl" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="$feedName">
      <package pattern="Umbraco.Automate" />
      <package pattern="Umbraco.Automate.*" />
      <package pattern="Umbraco" />
      <package pattern="Umbraco.*" />
      <package pattern="Clean" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
      <package pattern="Umbraco.Automate" />
      <package pattern="Umbraco.Automate.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
}

$configContent | Out-File -FilePath $nugetConfig -Encoding utf8 -Force

Pop-Location

# Step 7: Install Umbraco.Automate packages from feed
Write-Host "Installing Umbraco.Automate packages from $Feed feed..." -ForegroundColor Green
Push-Location "demos\v$VersionMajor\$SiteName"

# Build the base command arguments for dotnet add package
# For nightly/prereleases, append --prerelease flag
function Install-Package {
    param([string]$PackageName)
    Write-Host "  Installing $PackageName..." -ForegroundColor Gray
    if ($Feed -eq "release") {
        dotnet add package $PackageName
    } else {
        dotnet add package $PackageName --prerelease
    }
}

# Core meta-package (includes Startup + Web.StaticAssets)
Install-Package "Umbraco.Automate"

Pop-Location

Write-Host ""
Write-Host "=== Setup Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Site location: demos\v$VersionMajor\$SiteName" -ForegroundColor Cyan
Write-Host ""
Write-Host "Credentials:" -ForegroundColor Yellow
Write-Host "  Email: admin@example.com"
Write-Host "  Password: password1234"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. cd demos\v$VersionMajor\$SiteName"
Write-Host "  2. dotnet run"
Write-Host "  3. Open https://localhost:44380 in your browser"
Write-Host ""
Write-Host "Note: All packages were installed from the $Feed feed:" -ForegroundColor Gray
Write-Host "  $FeedUrl" -ForegroundColor Gray
Write-Host ""

# Restore original directory
Pop-Location
