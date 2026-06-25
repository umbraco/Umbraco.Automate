#!/bin/bash
# WorktreeCreate hook for Claude Code
#
# Replaces the default git worktree creation to:
#   1. Use vN/feature/<name> branch naming (version-prefixed convention)
#   2. Copy files specified in .worktreeinclude to the new worktree
#
# Input (JSON on stdin): { "name": "<slug>", "cwd": "<project-root>", ... }
# Output (stdout):       Absolute path to the created worktree directory
#
# All informational output goes to stderr to keep stdout clean for the path.
# Cross-platform: handles Windows (Git Bash) and Unix path conversions.

set -e

# --- Read input ---
INPUT=$(cat)

if ! command -v jq &>/dev/null; then
  echo "Error: jq is required for worktree hooks. Install it: https://jqlang.github.io/jq/download/" >&2
  echo "  Windows (winget): winget install jqlang.jq" >&2
  echo "  Windows (scoop):  scoop install jq" >&2
  exit 1
fi

NAME=$(echo "$INPUT" | jq -r '.name')
CWD=$(echo "$INPUT" | jq -r '.cwd')

# --- Cross-platform path handling ---
# Claude Code sends Windows paths (D:\Work\...) in JSON on Windows,
# but bash/find need Unix-style paths (/d/Work/...).
# cygpath is available in Git Bash on Windows.
to_unix_path() {
  if command -v cygpath &>/dev/null; then
    cygpath -u "$1"
  else
    echo "$1"
  fi
}

to_native_path() {
  if command -v cygpath &>/dev/null; then
    cygpath -w "$1"
  else
    echo "$1"
  fi
}

# --- Paths ---
# Convert CWD to Unix-style for internal use, fallback to git root
if [[ -n "$CWD" && "$CWD" != "null" ]]; then
  GIT_ROOT=$(to_unix_path "$CWD")
else
  GIT_ROOT=$(git rev-parse --show-toplevel)
fi

WORKTREE_DIR="$GIT_ROOT/.claude/worktrees"
WORKTREE_PATH="$WORKTREE_DIR/$NAME"

# --- Determine the version-line prefix (vN) ---
# All branches are version-prefixed (e.g. v18/dev). Worktrees inherit the prefix of the
# current line so generated branches satisfy the vN/feature/<name> convention.
CURRENT_BRANCH=$(git -C "$GIT_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || true)
PREFIX=""
if [[ "$CURRENT_BRANCH" =~ ^(v[0-9]+)/ ]]; then
  PREFIX="${BASH_REMATCH[1]}"
else
  # Fall back to the highest vN/dev on origin
  PREFIX=$(git -C "$GIT_ROOT" for-each-ref --format='%(refname:lstrip=3)' 'refs/remotes/origin/v*/dev' 2>/dev/null \
    | sed -n 's@/dev$@@p' | sort -V | tail -n1)
fi
PREFIX="${PREFIX:-v18}"

BRANCH_NAME="$PREFIX/feature/$NAME"

# --- Ensure .claude/worktrees is in .gitignore ---
if ! grep -qF '.claude/worktrees' "$GIT_ROOT/.gitignore" 2>/dev/null; then
  # Add a newline if file doesn't end with one
  if [[ -f "$GIT_ROOT/.gitignore" ]] && [[ -n "$(tail -c 1 "$GIT_ROOT/.gitignore")" ]]; then
    echo "" >> "$GIT_ROOT/.gitignore"
  fi
  echo ".claude/worktrees" >> "$GIT_ROOT/.gitignore"
  echo "Added .claude/worktrees to .gitignore" >&2
fi

# --- Determine base branch ---
# Prefer this line's vN/dev when it exists, then fall back to origin/HEAD,
# then probe common branch names.
DEFAULT_BRANCH=""
if git show-ref --verify --quiet "refs/remotes/origin/$PREFIX/dev" 2>/dev/null; then
  DEFAULT_BRANCH="$PREFIX/dev"
else
  DEFAULT_BRANCH=$(git symbolic-ref refs/remotes/origin/HEAD 2>/dev/null | sed 's@^refs/remotes/origin/@@') || true
  if [[ -z "$DEFAULT_BRANCH" ]]; then
    for candidate in "$PREFIX/main" main master; do
      if git show-ref --verify --quiet "refs/remotes/origin/$candidate" 2>/dev/null; then
        DEFAULT_BRANCH="$candidate"
        break
      fi
    done
  fi
fi
DEFAULT_BRANCH="${DEFAULT_BRANCH:-$PREFIX/dev}"

# --- Create worktree ---
mkdir -p "$WORKTREE_DIR"

if [[ -d "$WORKTREE_PATH" ]]; then
  echo "Worktree already exists: $WORKTREE_PATH" >&2
elif git show-ref --verify --quiet "refs/heads/$BRANCH_NAME" 2>/dev/null; then
  echo "Using existing branch: $BRANCH_NAME" >&2
  git worktree add "$WORKTREE_PATH" "$BRANCH_NAME" >&2
else
  echo "Creating branch: $BRANCH_NAME (from origin/$DEFAULT_BRANCH)" >&2
  git worktree add -b "$BRANCH_NAME" "$WORKTREE_PATH" "origin/$DEFAULT_BRANCH" >&2
fi

# --- Copy .worktreeinclude files ---
# .worktreeinclude uses gitignore syntax (globs, negation, directory patterns).
# We pass it directly to git's pattern matching engine via --exclude-from,
# so all gitignore rules work natively: *, **, !, trailing /, etc.
INCLUDE_FILE="$GIT_ROOT/.worktreeinclude"

if [[ -f "$INCLUDE_FILE" ]]; then
  file_list=$(git -C "$GIT_ROOT" ls-files --others --ignored --exclude-from="$INCLUDE_FILE" 2>/dev/null)

  if [[ -z "$file_list" ]]; then
    echo "No files matched .worktreeinclude patterns" >&2
  else
    count=$(echo "$file_list" | wc -l | tr -d ' ')
    echo "Copying $count file(s) from .worktreeinclude..." >&2

    # Bulk copy via tar (fast even for thousands of files, handles paths with spaces)
    git -C "$GIT_ROOT" ls-files -z --others --ignored --exclude-from="$INCLUDE_FILE" 2>/dev/null | \
      tar -C "$GIT_ROOT" --null -T - -cf - 2>/dev/null | \
      tar -C "$WORKTREE_PATH" -xf - 2>/dev/null

    # Summary: group by top-level directory, show root files individually
    echo "$file_list" | awk -F/ '{print ($2 ? $1"/" : $0)}' | sort | uniq -c | \
      while read -r cnt path; do
        if [[ "$cnt" -eq 1 && "$path" != */ ]]; then
          echo "  + $path" >&2
        else
          echo "  + $path ($cnt files)" >&2
        fi
      done
  fi
else
  echo "No .worktreeinclude file found - skipping file copy" >&2
fi

# --- Output the worktree path (this is what Claude Code reads) ---
# Convert to native path format so Claude Code (Node.js) can use it.
# On Windows: /d/Work/... -> D:\Work\...
# On Unix: passes through unchanged.
ABSOLUTE_PATH=$(cd "$WORKTREE_PATH" && pwd)
echo "$(to_native_path "$ABSOLUTE_PATH")"
