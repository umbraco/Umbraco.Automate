# Setup script to configure git hooks for Umbraco.Automate monorepo

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Umbraco.Automate Git Hooks Setup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Get the repository root
$repoRoot = git rev-parse --show-toplevel 2>$null

if ([string]::IsNullOrEmpty($repoRoot)) {
    Write-Error "Error: Not in a git repository"
    exit 1
}

# Convert Unix path to Windows path if needed
$repoRoot = $repoRoot -replace '/', '\'
$hooksDir = Join-Path $repoRoot ".githooks"

# Check if hooks directory exists
if (-not (Test-Path $hooksDir)) {
    Write-Error "Error: .githooks directory not found at $hooksDir"
    exit 1
}

# Configure git to use the custom hooks directory
Write-Host "Configuring git hooks..."
git config core.hooksPath .githooks

# Configure custom merge driver for release-manifest.json
Write-Host "Configuring custom merge driver for release-manifest.json..."
$mergeDriverScript = if ($IsWindows -or $env:OS -match "Windows") {
    "pwsh .githooks/merge-preserve-on-release.ps1 %O %A %B %L %P"
} else {
    ".githooks/merge-preserve-on-release.sh %O %A %B %L %P"
}
git config merge.preserve-on-release.name "Preserve release-manifest.json on release/hotfix branches"
git config merge.preserve-on-release.driver $mergeDriverScript

Write-Host ""
Write-Host "Git hooks configured successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "The following hooks are now active:"
Write-Host "  - pre-push: Validates branch naming conventions"
Write-Host "  - commit-msg: Validates commit messages (conventional commits)"
Write-Host "  - post-merge: Commits staged release-manifest.json on vN/release, vN/hotfix; removes it on vN/main, vN/dev"
Write-Host "  - pre-merge-commit: Ensures release-manifest.json is staged on release/hotfix branches"
Write-Host "  - merge driver: Preserves release-manifest.json on release/hotfix branches during merges"
Write-Host ""
Write-Host "To disable hooks, run:"
Write-Host "  git config --unset core.hooksPath"
Write-Host ""
