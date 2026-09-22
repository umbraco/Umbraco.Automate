#!/bin/bash
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

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &>/dev/null && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/.." &>/dev/null && pwd )"

FORCE=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --force|-f)
            FORCE=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -f, --force  Reinstall and rebuild even if the assets are already present"
            echo "  -h, --help   Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

ASSET_DIRS=(
    "Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/wwwroot"
    "Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict.Core/wwwroot"
)

asset_dir_populated() {
    local full="$REPO_ROOT/$1"
    [ -d "$full" ] || return 1
    [ -n "$(find "$full" -type f -print -quit 2>/dev/null)" ]
}

echo "========================================="
echo "Umbraco.Automate Frontend Assets"
echo "========================================="
echo "Working directory: $REPO_ROOT"

MISSING=()
for dir in "${ASSET_DIRS[@]}"; do
    asset_dir_populated "$dir" || MISSING+=("$dir")
done

if [ ${#MISSING[@]} -eq 0 ] && [ "$FORCE" = false ]; then
    echo "All frontend assets present - nothing to build. Use --force to rebuild."
    for dir in "${ASSET_DIRS[@]}"; do echo "  = $dir"; done
    exit 0
fi

if [ ${#MISSING[@]} -gt 0 ]; then
    echo "Missing frontend assets:"
    for dir in "${MISSING[@]}"; do echo "  - $dir"; done
fi
echo ""

# Toolchain check (shared with install-demo-site.sh). Sourced so a PATH fix sticks.
REQUIRE_NODE_REPO_ROOT="$REPO_ROOT" . "$SCRIPT_DIR/require-node.sh"
echo ""

cd "$REPO_ROOT" || exit 1

if [ "$FORCE" = true ] || [ ! -d "$REPO_ROOT/node_modules" ]; then
    # npm ci installs strictly from package-lock.json and never rewrites it. Plain
    # npm install can drop optional platform packages and add "peer" markers, which
    # show up as pure lockfile noise in the diff - fall back only if ci can't run.
    echo "Installing npm workspaces (npm ci, repo root)..."
    if ! npm ci; then
        echo "npm ci failed - falling back to 'npm install' (this may modify package-lock.json)."
        if ! npm install; then
            echo "ERROR: npm install failed." >&2
            exit 1
        fi
    fi
else
    echo "node_modules present - skipping install. Use --force to reinstall."
fi

echo ""
echo "Building frontend assets (npm run build, all workspaces)..."
if ! npm run build; then
    echo "ERROR: npm run build failed." >&2
    exit 1
fi

STILL_MISSING=()
for dir in "${ASSET_DIRS[@]}"; do
    asset_dir_populated "$dir" || STILL_MISSING+=("$dir")
done

if [ ${#STILL_MISSING[@]} -gt 0 ]; then
    echo ""
    echo "ERROR: The build completed but these asset folders are still empty:" >&2
    for dir in "${STILL_MISSING[@]}"; do echo "  - $dir" >&2; done
    exit 1
fi

echo ""
echo "Frontend assets ready:"
for dir in "${ASSET_DIRS[@]}"; do echo "  + $dir"; done
exit 0
