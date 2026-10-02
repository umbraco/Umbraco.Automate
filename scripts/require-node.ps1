# Shared Node.js toolchain gate.
#
# Dot-source it so any PATH fix applies to the calling script:
#     . (Join-Path $PSScriptRoot "require-node.ps1") -RepoRoot $RepoRoot
#
# The required version comes from package.json's engines.node, so this stays in
# lockstep with the npm-side enforcement and the .nvmrc. If the Node on PATH is
# missing or too old but nvm-for-windows already has a suitable version installed,
# that version is prepended to $env:PATH for THIS PROCESS ONLY. 'nvm use' is
# deliberately not used: it switches the default for the whole machine.

param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
)

$packageJson = Get-Content (Join-Path $RepoRoot "package.json") -Raw | ConvertFrom-Json
$requiredNodeRange = $packageJson.engines.node
if ($requiredNodeRange -match '(\d+)') {
    $requiredNodeMajor = [int]$matches[1]
} else {
    Write-Host "ERROR: Could not parse engines.node ('$requiredNodeRange') from package.json." -ForegroundColor Red
    exit 1
}

$nodeVersionRaw = (node --version 2>$null) -replace '^v', ''
if ($nodeVersionRaw -and [int]($nodeVersionRaw -split '\.')[0] -ge $requiredNodeMajor) {
    Write-Host "Node $nodeVersionRaw detected (satisfies '$requiredNodeRange')." -ForegroundColor Gray
    return
}

# Look for an already-installed nvm-for-windows version that satisfies the range.
$nvmRoot = if ($env:NVM_HOME) { $env:NVM_HOME } else { "C:\ProgramData\nvm" }
if (Test-Path $nvmRoot) {
    $candidate = Get-ChildItem -Path $nvmRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^v\d+\.\d+\.\d+$' -and (Test-Path (Join-Path $_.FullName "node.exe")) } |
        Where-Object { [int]($_.Name.TrimStart('v') -split '\.')[0] -ge $requiredNodeMajor } |
        Sort-Object { [version]$_.Name.TrimStart('v') } -Descending |
        Select-Object -First 1

    if ($candidate) {
        # Ask the candidate directly rather than re-resolving 'node' from PATH.
        $candidateVersion = (& (Join-Path $candidate.FullName "node.exe") --version 2>$null) -replace '^v', ''
        if ($candidateVersion) {
            $env:PATH = "$($candidate.FullName);$env:PATH"
            Write-Host "Node $candidateVersion found at $($candidate.FullName); added to PATH for this process only (satisfies '$requiredNodeRange')." -ForegroundColor Gray
            return
        }
    }
}

if ($nodeVersionRaw) {
    Write-Host "ERROR: Node $nodeVersionRaw detected; package.json requires '$requiredNodeRange'." -ForegroundColor Red
} else {
    Write-Host "ERROR: Node.js is not installed or not on PATH. package.json requires '$requiredNodeRange'." -ForegroundColor Red
}
Write-Host "Run 'nvm install $requiredNodeMajor && nvm use $requiredNodeMajor' (or equivalent) before re-running this script." -ForegroundColor Yellow
exit 1
