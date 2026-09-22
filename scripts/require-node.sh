#!/bin/bash
# Shared Node.js toolchain gate.
#
# Source it (don't execute it) so any PATH fix applies to the calling script:
#     REQUIRE_NODE_REPO_ROOT="$REPO_ROOT" . "$SCRIPT_DIR/require-node.sh"
#
# The required version comes from package.json's engines.node, so this stays in
# lockstep with the npm-side enforcement and the .nvmrc. If the Node on PATH is
# missing or too old but nvm-for-windows already has a suitable version installed,
# that version is prepended to PATH for THIS PROCESS ONLY. 'nvm use' is
# deliberately not used: it switches the default for the whole machine.

REQUIRE_NODE_REPO_ROOT="${REQUIRE_NODE_REPO_ROOT:-$REPO_ROOT}"

REQUIRED_NODE_RANGE=$(grep -oE '"node"[[:space:]]*:[[:space:]]*"[^"]+"' "$REQUIRE_NODE_REPO_ROOT/package.json" | head -1 | grep -oE '"[^"]+"$' | tr -d '"')
REQUIRED_NODE_MAJOR=$(echo "$REQUIRED_NODE_RANGE" | grep -oE '[0-9]+' | head -1)
if [ -z "$REQUIRED_NODE_MAJOR" ]; then
    echo "ERROR: Could not parse engines.node ('$REQUIRED_NODE_RANGE') from package.json." >&2
    exit 1
fi

NODE_VERSION_RAW=""
if command -v node >/dev/null 2>&1; then
    NODE_VERSION_RAW=$(node --version | sed 's/^v//')
fi
NODE_MAJOR="${NODE_VERSION_RAW%%.*}"

if [ -n "$NODE_VERSION_RAW" ] && [ "${NODE_MAJOR:-0}" -ge "$REQUIRED_NODE_MAJOR" ]; then
    echo "Node $NODE_VERSION_RAW detected (satisfies '$REQUIRED_NODE_RANGE')."
else
    # Look for an already-installed nvm-for-windows version that satisfies the range.
    NVM_ROOT=""
    if [ -n "$NVM_HOME" ]; then
        if command -v cygpath >/dev/null 2>&1; then
            NVM_ROOT=$(cygpath -u "$NVM_HOME")
        else
            NVM_ROOT="$NVM_HOME"
        fi
    elif [ -d "/c/ProgramData/nvm" ]; then
        NVM_ROOT="/c/ProgramData/nvm"
    fi

    NVM_CANDIDATE=""
    if [ -n "$NVM_ROOT" ] && [ -d "$NVM_ROOT" ]; then
        NVM_CANDIDATE=$(
            for dir in "$NVM_ROOT"/v*; do
                [ -f "$dir/node.exe" ] || [ -x "$dir/bin/node" ] || continue
                ver=$(basename "$dir")
                ver=${ver#v}
                major=${ver%%.*}
                case "$major" in '' | *[!0-9]*) continue ;; esac
                [ "$major" -ge "$REQUIRED_NODE_MAJOR" ] || continue
                printf '%s\t%s\n' "$ver" "$dir"
            done | sort -t. -k1,1n -k2,2n -k3,3n | tail -1 | cut -f2
        )
    fi

    if [ -n "$NVM_CANDIDATE" ]; then
        PATH="$NVM_CANDIDATE:$PATH"
        export PATH
        NODE_VERSION_RAW=$(node --version | sed 's/^v//')
        echo "Node $NODE_VERSION_RAW found at $NVM_CANDIDATE; added to PATH for this process only (satisfies '$REQUIRED_NODE_RANGE')."
    else
        if [ -n "$NODE_VERSION_RAW" ]; then
            echo "ERROR: Node $NODE_VERSION_RAW detected; package.json requires '$REQUIRED_NODE_RANGE'." >&2
        else
            echo "ERROR: Node.js is not installed or not on PATH. package.json requires '$REQUIRED_NODE_RANGE'." >&2
        fi
        echo "Run 'nvm install $REQUIRED_NODE_MAJOR && nvm use $REQUIRED_NODE_MAJOR' (or equivalent) before re-running." >&2
        exit 1
    fi
fi
