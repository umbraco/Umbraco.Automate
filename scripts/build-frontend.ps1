# Frontend Asset Build
# Ensures the backoffice assets exist, building them when they don't.
#
# Both wwwroot folders below are gitignored build output produced by `npm run build`
# at the repo ROOT. A fresh clone or a new worktree has neither, and the Automate
# section of the backoffice then renders blank with no error, so the demo site
# start path runs this first.
#
# Install and build from the ROOT, never from a Client folder: the workspaces are
# declared in the root package.json, and installing inside a Client folder produces
# a spurious root lockfile diff.

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"
# Keep native exit codes as data so the npm ci -> npm install fallback can run.
$PSNativeCommandUseErrorActionPreference = $false

$ScriptDir = $PSScriptRoot
$RepoRoot = (Resolve-Path (Split-Path -Parent $ScriptDir)).Path

$AssetDirs = @(
    "Umbraco.Automate\src\Umbraco.Automate.Web.StaticAssets\wwwroot",
    "Umbraco.Automate.OpenIddict\src\Umbraco.Automate.OpenIddict.Core\wwwroot"
)

function Test-AssetDir {
    param([string]$RelativePath)
    $full = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path $full)) { return $false }
    return $null -ne (Get-ChildItem -Path $full -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1)
}

Write-Host "=== Umbraco.Automate Frontend Assets ===" -ForegroundColor Cyan
Write-Host "Working directory: $RepoRoot" -ForegroundColor Gray

$missing = @($AssetDirs | Where-Object { -not (Test-AssetDir $_) })

if ($missing.Count -eq 0 -and -not $Force) {
    Write-Host "All frontend assets present - nothing to build. Use -Force to rebuild." -ForegroundColor Green
    foreach ($dir in $AssetDirs) { Write-Host "  = $dir" -ForegroundColor Gray }
    exit 0
}

if ($missing.Count -gt 0) {
    Write-Host "Missing frontend assets:" -ForegroundColor Yellow
    foreach ($dir in $missing) { Write-Host "  - $dir" -ForegroundColor Yellow }
}
Write-Host ""

# Toolchain check (shared with install-demo-site.ps1). Dot-sourced so a PATH fix sticks.
. (Join-Path $ScriptDir "require-node.ps1") -RepoRoot $RepoRoot
Write-Host ""

Push-Location $RepoRoot
try {
    if ($Force -or -not (Test-Path (Join-Path $RepoRoot "node_modules"))) {
        # npm ci installs strictly from package-lock.json and never rewrites it. Plain
        # npm install can drop optional platform packages and add "peer" markers, which
        # show up as pure lockfile noise in the diff - fall back only if ci can't run.
        Write-Host "Installing npm workspaces (npm ci, repo root)..." -ForegroundColor Cyan
        npm ci
        if ($LASTEXITCODE -ne 0) {
            Write-Host "npm ci failed - falling back to 'npm install' (this may modify package-lock.json)." -ForegroundColor Yellow
            npm install
            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: npm install failed." -ForegroundColor Red
                exit 1
            }
        }
    } else {
        Write-Host "node_modules present - skipping install. Use -Force to reinstall." -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "Building frontend assets (npm run build, all workspaces)..." -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: npm run build failed." -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}

$stillMissing = @($AssetDirs | Where-Object { -not (Test-AssetDir $_) })
if ($stillMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "ERROR: The build completed but these asset folders are still empty:" -ForegroundColor Red
    foreach ($dir in $stillMissing) { Write-Host "  - $dir" -ForegroundColor Red }
    exit 1
}

Write-Host ""
Write-Host "Frontend assets ready:" -ForegroundColor Green
foreach ($dir in $AssetDirs) { Write-Host "  + $dir" -ForegroundColor Gray }
exit 0
