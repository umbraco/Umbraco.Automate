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

# Check if demo already exists
if ((Test-Path "demo") -and -not $Force) {
    Write-Host "Demo folder already exists. Use -Force to recreate." -ForegroundColor Yellow
    Write-Host "Or open the existing Umbraco.Automate.local.slnx" -ForegroundColor Yellow
    exit 0
}

# Clean up existing demo if Force
if ($Force -and (Test-Path "demo")) {
    Write-Host "Removing existing demo folder..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force "demo"
}

if ($Force -and (Test-Path "Umbraco.Automate.local.slnx")) {
    Remove-Item -Force "Umbraco.Automate.local.slnx"
}

# Step 1: Install Umbraco templates
if (-not $SkipTemplateInstall) {
    Write-Host "Installing Umbraco templates..." -ForegroundColor Green
    dotnet new install Umbraco.Templates --force
}

# Step 2: Create demo folder with build overrides
Write-Host "Creating demo folder..." -ForegroundColor Green
New-Item -ItemType Directory -Path "demo" -Force | Out-Null

# Disable package validation for demo folder
$directoryBuildPropsSource = Join-Path $ScriptDir "templates\Directory.Build.props"
Copy-Item -Path $directoryBuildPropsSource -Destination "demo\Directory.Build.props" -Force

# Disable central package management for demo folder
$directoryPackagesPropsSource = Join-Path $ScriptDir "templates\Directory.Packages.props"
Copy-Item -Path $directoryPackagesPropsSource -Destination "demo\Directory.Packages.props" -Force

# Step 3: Create the Umbraco demo site
Write-Host "Creating Umbraco demo site..." -ForegroundColor Green
Push-Location "demo"
dotnet new umbraco --force -n "Umbraco.Automate.DemoSite" --friendly-name "Administrator" --email "admin@example.com" --password "password1234" --development-database-type SQLite
Pop-Location

# Step 3.1: Install Clean starter kit
Write-Host "Installing Clean starter kit..." -ForegroundColor Green
Push-Location "demo\Umbraco.Automate.DemoSite"
dotnet add package Clean
Pop-Location

# Step 3.2: Set fixed port for consistent development
Write-Host "Configuring fixed port (44380)..." -ForegroundColor Green
$launchSettingsSource = Join-Path $ScriptDir "templates\launchSettings.json"
$launchSettingsPath = "demo\Umbraco.Automate.DemoSite\Properties\launchSettings.json"
New-Item -ItemType Directory -Path (Split-Path $launchSettingsPath) -Force | Out-Null
Copy-Item -Path $launchSettingsSource -Destination $launchSettingsPath -Force

# Step 3.3: Add NamedPipeListenerComposer for HTTP over named pipes
Write-Host "Adding NamedPipeListenerComposer for HTTP over named pipes..." -ForegroundColor Green
$composerSourcePath = Join-Path $ScriptDir "templates\NamedPipeListenerComposer.cs"
$composerDestPath = "demo\Umbraco.Automate.DemoSite\Composers\NamedPipeListenerComposer.cs"
New-Item -ItemType Directory -Path (Split-Path $composerDestPath) -Force | Out-Null
Copy-Item -Path $composerSourcePath -Destination $composerDestPath -Force

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
dotnet sln "Umbraco.Automate.local.slnx" add "demo/Umbraco.Automate.DemoSite/Umbraco.Automate.DemoSite.csproj" --solution-folder "Demo"

# Step 7: Add project references to demo site
Write-Host "Adding project references to demo site..." -ForegroundColor Green
$demoProject = "demo/Umbraco.Automate.DemoSite/Umbraco.Automate.DemoSite.csproj"

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
Write-Host "Demo site: demo/Umbraco.Automate.DemoSite" -ForegroundColor Cyan
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
