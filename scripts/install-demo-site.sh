#!/bin/bash
# Unified Demo Site Setup Script
# Creates a shared demo site with Umbraco.Automate

set -e

# Determine repository root (parent of scripts folder)
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &>/dev/null && pwd )"
REPO_ROOT="$( cd "$SCRIPT_DIR/.." &>/dev/null && pwd )"

# Change to repository root to ensure consistent behavior
cd "$REPO_ROOT" || exit 1

# Parse arguments
SKIP_TEMPLATE_INSTALL=false
FORCE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-template-install|-s)
            SKIP_TEMPLATE_INSTALL=true
            shift
            ;;
        --force|-f)
            FORCE=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -s, --skip-template-install  Skip reinstalling Umbraco.Templates"
            echo "  -f, --force                  Recreate demo if it already exists"
            echo "  -h, --help                   Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

echo "========================================="
echo "Umbraco.Automate Demo Site Setup"
echo "========================================="
echo "Working directory: $REPO_ROOT"
echo ""

# Toolchain check — required Node version comes from package.json's engines.node, so this
# stays in lockstep with the npm-side enforcement and the .nvmrc.
REQUIRED_NODE_RANGE=$(grep -oE '"node"[[:space:]]*:[[:space:]]*"[^"]+"' "$REPO_ROOT/package.json" | head -1 | grep -oE '"[^"]+"$' | tr -d '"')
REQUIRED_NODE_MAJOR=$(echo "$REQUIRED_NODE_RANGE" | grep -oE '[0-9]+' | head -1)
if [ -z "$REQUIRED_NODE_MAJOR" ]; then
    echo "ERROR: Could not parse engines.node ('$REQUIRED_NODE_RANGE') from package.json." >&2
    exit 1
fi
if ! command -v node >/dev/null 2>&1; then
    echo "ERROR: Node.js is not installed or not on PATH. package.json requires '$REQUIRED_NODE_RANGE'." >&2
    echo "Install Node $REQUIRED_NODE_MAJOR+ (e.g. 'nvm install $REQUIRED_NODE_MAJOR && nvm use $REQUIRED_NODE_MAJOR') and re-run." >&2
    exit 1
fi
NODE_VERSION_RAW=$(node --version | sed 's/^v//')
NODE_MAJOR=${NODE_VERSION_RAW%%.*}
if [ "${NODE_MAJOR:-0}" -lt "$REQUIRED_NODE_MAJOR" ]; then
    echo "ERROR: Node $NODE_VERSION_RAW detected; package.json requires '$REQUIRED_NODE_RANGE'." >&2
    echo "Run 'nvm install $REQUIRED_NODE_MAJOR && nvm use $REQUIRED_NODE_MAJOR' (or equivalent) before re-running this script." >&2
    exit 1
fi
echo "Node $NODE_VERSION_RAW detected (satisfies '$REQUIRED_NODE_RANGE')."
echo ""

# Detect template version and major from Directory.Packages.props.
# The Umbraco.Cms.Core version (lower bound if a range, or the fixed version) is the right
# template version to scaffold the demo site against.
PACKAGES_PROPS_PATH="$REPO_ROOT/Directory.Packages.props"
if [ ! -f "$PACKAGES_PROPS_PATH" ]; then
    echo "ERROR: Could not find $PACKAGES_PROPS_PATH" >&2
    exit 1
fi
# Try range format first: Version="[18.0.0,...)"
TEMPLATE_VERSION=$(grep -oE 'Include="Umbraco\.Cms\.Core" Version="\[[^,\]]+' "$PACKAGES_PROPS_PATH" | grep -oE '\[[^,\]]+' | tr -d '[')
if [ -z "$TEMPLATE_VERSION" ]; then
    # Try fixed version format: Version="18.0.0"
    TEMPLATE_VERSION=$(grep -oE 'Include="Umbraco\.Cms\.Core" Version="[^"\[]*"' "$PACKAGES_PROPS_PATH" | grep -oE '"[^"\[]*"$' | tr -d '"')
fi
if [ -z "$TEMPLATE_VERSION" ]; then
    echo "ERROR: Could not find Umbraco.Cms.Core version in $PACKAGES_PROPS_PATH" >&2
    exit 1
fi
VERSION_MAJOR=$(echo "$TEMPLATE_VERSION" | cut -d. -f1)
IS_TEMPLATE_PRERELEASE=false
if echo "$TEMPLATE_VERSION" | grep -q '-'; then
    IS_TEMPLATE_PRERELEASE=true
fi
echo "Target Umbraco.Cms template version: $TEMPLATE_VERSION (v$VERSION_MAJOR)"
echo ""

# Versioned demo directory: demos/vN/
DEMO_DIR="demos/v${VERSION_MAJOR}"
DEMO_SITE_DIR="${DEMO_DIR}/Umbraco.Automate.DemoSite"

# Check if demo already exists
if [ -d "$DEMO_DIR" ] && [ "$FORCE" = false ]; then
    echo "Demo folder '$DEMO_DIR' already exists. Use --force to recreate."
    echo "Or open the existing Umbraco.Automate.local.slnx"
    exit 0
fi

# Clean up existing demo if Force
if [ "$FORCE" = true ] && [ -d "$DEMO_DIR" ]; then
    echo "Removing existing demo folder '$DEMO_DIR'..."
    rm -rf "$DEMO_DIR"
fi

if [ "$FORCE" = true ] && [ -f "Umbraco.Automate.local.slnx" ]; then
    rm -f "Umbraco.Automate.local.slnx"
fi

# Step 1: Install Umbraco templates
if [ "$SKIP_TEMPLATE_INSTALL" = false ]; then
    echo "Installing Umbraco templates ($TEMPLATE_VERSION)..."
    # Uninstall any existing version to avoid conflicts
    echo "Removing any existing Umbraco.Templates installations..."
    if dotnet new uninstall 2>&1 | grep -q "Umbraco\.Templates"; then
        dotnet new uninstall Umbraco.Templates 2>/dev/null || true
    fi
    if [ "$IS_TEMPLATE_PRERELEASE" = true ]; then
        # Prerelease templates require the umbracoprereleases MyGet feed to be configured.
        # If not yet configured: dotnet nuget add source https://www.myget.org/F/umbracoprereleases/api/v3/index.json --name UmbracoPreReleases
        echo "NOTE: Prerelease template ($TEMPLATE_VERSION) requires the umbracoprereleases MyGet source."
    fi
    dotnet new install "Umbraco.Templates::${TEMPLATE_VERSION}" --force
fi

# Step 2: Create demo folder with build overrides
echo "Creating demo folder '$DEMO_DIR'..."
mkdir -p "$DEMO_DIR"

# Disable package validation for demo folder
cp "$SCRIPT_DIR/templates/Directory.Build.props" "$DEMO_DIR/Directory.Build.props"

# Disable central package management for demo folder
cp "$SCRIPT_DIR/templates/Directory.Packages.props" "$DEMO_DIR/Directory.Packages.props"

# Step 3: Create the Umbraco demo site
echo "Creating Umbraco demo site..."
pushd "$DEMO_DIR" > /dev/null
dotnet new umbraco --force -n "Umbraco.Automate.DemoSite" --friendly-name "Administrator" --email "admin@example.com" --password "password1234" --development-database-type SQLite
popd > /dev/null

# Step 3.1: Install Clean starter kit
# Clean's major version does not match the CMS major — map explicitly and add
# new entries as majors are released. Floating patterns keep multi-version installs correct:
#   17 -> 7.*   (stable)
#   18 -> 8.*-* (the CMS-v18-compatible Clean, e.g. 8.0.0-rc1, is still prerelease)
case "$VERSION_MAJOR" in
    17) CLEAN_VERSION="7.*" ;;
    18) CLEAN_VERSION="8.*-*" ;;
    *)  CLEAN_VERSION="" ;;
esac
echo "Installing Clean starter kit..."
pushd "$DEMO_SITE_DIR" > /dev/null
if [ -n "$CLEAN_VERSION" ]; then
    dotnet add package Clean --version "$CLEAN_VERSION"
else
    echo "Warning: No Clean version mapping for v$VERSION_MAJOR, using latest stable."
    dotnet add package Clean
fi
popd > /dev/null

# Step 3.2: Set fixed port for consistent development
echo "Configuring fixed port (44380)..."
mkdir -p "$DEMO_SITE_DIR/Properties"
cp "$SCRIPT_DIR/templates/launchSettings.json" "$DEMO_SITE_DIR/Properties/launchSettings.json"

# Step 3.3: Add NamedPipeListenerComposer for HTTP over named pipes
echo "Adding NamedPipeListenerComposer for HTTP over named pipes..."
mkdir -p "$DEMO_SITE_DIR/Composers"
cp "$SCRIPT_DIR/templates/NamedPipeListenerComposer.cs" "$DEMO_SITE_DIR/Composers/NamedPipeListenerComposer.cs"

# Step 4: Create unified solution
echo "Creating unified solution..."
dotnet new sln -n "Umbraco.Automate.local" --force

# Helper function to add all projects from a product's src and tests folders
add_product_projects() {
    local product_folder="$1"
    local solution_folder="$2"

    local count=0
    for sub in src tests; do
        local sub_path="$product_folder/$sub"
        if [ -d "$sub_path" ]; then
            while IFS= read -r -d '' proj; do
                local proj_name=$(basename "$proj")
                echo "  Adding $proj_name"
                dotnet sln "Umbraco.Automate.local.slnx" add "$proj" --solution-folder "$solution_folder" 2>/dev/null || true
                ((count++))
            done < <(find "$sub_path" -name "*.csproj" -print0)
        fi
    done
    echo "  Added $count projects"
}

# Step 5: Add Core projects
echo "Adding Umbraco.Automate projects..."
add_product_projects "Umbraco.Automate" "Core"

# Step 6: Add OpenIddict projects
echo "Adding Umbraco.Automate.OpenIddict projects..."
add_product_projects "Umbraco.Automate.OpenIddict" "OpenIddict"

# Step 7: Add Slack projects
echo "Adding Umbraco.Automate.Slack projects..."
add_product_projects "Umbraco.Automate.Slack" "Slack"

# Step 8: Add demo site to solution
echo "Adding demo site to solution..."
dotnet sln "Umbraco.Automate.local.slnx" add "$DEMO_SITE_DIR/Umbraco.Automate.DemoSite.csproj" --solution-folder "Demo"

# Step 7: Add project references to demo site
echo "Adding project references to demo site..."
DEMO_PROJECT="$DEMO_SITE_DIR/Umbraco.Automate.DemoSite.csproj"

# Core references (Startup + Web.StaticAssets)
dotnet add "$DEMO_PROJECT" reference "Umbraco.Automate/src/Umbraco.Automate.Startup/Umbraco.Automate.Startup.csproj"
dotnet add "$DEMO_PROJECT" reference "Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Umbraco.Automate.Web.StaticAssets.csproj"

# OpenIddict add-on
if [ -f "Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.csproj" ]; then
    dotnet add "$DEMO_PROJECT" reference "Umbraco.Automate.OpenIddict/src/Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.csproj"
fi

# Slack add-on
if [ -f "Umbraco.Automate.Slack/src/Umbraco.Automate.Slack/Umbraco.Automate.Slack.csproj" ]; then
    dotnet add "$DEMO_PROJECT" reference "Umbraco.Automate.Slack/src/Umbraco.Automate.Slack/Umbraco.Automate.Slack.csproj"
fi

echo ""
echo "========================================="
echo "Setup Complete!"
echo "========================================="
echo ""
echo "Solution: Umbraco.Automate.local.slnx"
echo "Demo site: $DEMO_SITE_DIR"
echo ""
echo "Credentials:"
echo "  Email: admin@example.com"
echo "  Password: password1234"
echo ""
echo "Next steps:"
echo "  1. Open Umbraco.Automate.local.slnx in your IDE"
echo "  2. Build the solution"
echo "  3. Run the Umbraco.Automate.DemoSite project"
echo ""
