---
name: demo-site-management
description: Manages the Umbraco.Automate demo site for development. Handles starting with the DemoSite profile, per-worktree port lookup via git config, and stopping. Use when starting, stopping, or checking the demo site.
argument-hint: [start|stop|status|restart|open]
allowed-tools: Bash, Read, TaskOutput, TaskStop
---

# Demo Site Management

Manage the Umbraco.Automate demo site. Each worktree gets its own stable dev port, assigned once by [Umbraco.Community.WorktreeDevPort](https://github.com/mattbrailsford/Umbraco.Community.WorktreeDevPort) and stored in that worktree's own git config — no named pipe, socket, or discovery endpoint to query.

## Command: $ARGUMENTS

Execute the requested demo site operation.

### Available commands

- **start**: Start demo site with the DemoSite profile
- **stop**: Stop the running demo site
- **status**: Check if site is running and show its port
- **restart**: Stop and restart the demo site
- **open**: Open the demo site in default browser

## Current Environment

- Working directory: !`pwd`
- Git branch: !`git branch --show-current 2>/dev/null || echo "not in git repo"`
- Background tasks: !`echo "Check with /tasks command for active background tasks"`

## Implementation Guide

### For "start"

1. Check if already running using multi-method detection:
    - Try reading the port (see "Get the worktree's dev port" section) and connecting to it
    - Check if background tasks exist with "DemoSite" in description
    - If running, report and exit
2. Detect demo site path:
    - Read `Directory.Packages.props` and extract the major from the `Umbraco.Cms.Core` version (range lower bound or fixed, e.g. `[18.0.0, …)` or `18.0.0` → `18`)
    - Demo site path: `demos/v{major}/Umbraco.Automate.DemoSite`
3. Ensure the frontend assets exist (a fresh clone or worktree has none, and the Automate section then renders blank with no error):
    - Run `pwsh -NoProfile -File scripts/build-frontend.ps1` (Windows) or `bash scripts/build-frontend.sh` (macOS/Linux) from the repo root
    - Safe to run every start: it is a no-op when both `wwwroot` folders are already populated
    - On a fresh worktree it runs `npm ci` + `npm run build` at the repo root and can take a few minutes - allow a long timeout and never run npm by hand instead
    - It handles the Node version itself (reads `engines.node`, and prepends an installed nvm-for-windows version to PATH for that process only - never run `nvm use`)
    - If it exits non-zero, stop and report its message: starting the site anyway gives a blank section with no error
4. If not running, start in background: `cd demos/v{major}/Umbraco.Automate.DemoSite && dotnet run --launch-profile DemoSite`
5. Wait 15-20 seconds for startup (the package picks a free port on first run in this worktree, or reuses the one it already picked)
6. Read the port (see "Get the worktree's dev port" section)
7. Report:
    - Task ID for later stopping (save this for future commands)
    - Port number
    - Site URL (`https://127.0.0.1:<port>`)

### For "stop"

1. Find background tasks related to demo site:
    - Look for tasks with "DemoSite" or "demo-site" in name
    - Extract task ID from task list

2. If task found:
    - Use TaskStop with the task ID to stop gracefully
    - Wait 2-3 seconds for cleanup

3. If no task found:
    - Report that no running demo site was found
    - Suggest checking with `/demo-site-management status`

4. Verify shutdown:
    - Try connecting to the last known port (should fail)
    - Check if background task is gone

5. Report results:
    - Success: "Demo site stopped (task ID: {id})"
    - Failure: "Could not find running demo site"
    - Note: the assigned port is remembered in git config and reused on the next start — nothing to clean up

### For "status"

Use multi-method detection to determine site status:

1. **Read the port and probe it**: see "Get the worktree's dev port" section
    - If a port is set and reachable, site is running
    - If no port is set yet, the site has never been started in this worktree
    - If a port is set but unreachable, the site isn't currently running

2. **Check background tasks**: Look for tasks with "DemoSite" or "demo-site" in name/output
    - If found, extract task ID

3. **Report comprehensive status**:
    - Running: yes/no
    - Task ID: if background task found
    - Port: from git config (if set)
    - Git context: branch name, worktree name, or "not in git repo"
    - Suggestion: How to start if not running, or how to connect if running

### For "restart"

Execute stop operation, wait 3 seconds, then execute start operation.

### For "open"

1. Check if demo site is running and get its port (see "Get the worktree's dev port" section)
    - If no port is set or it's unreachable, report error: "Demo site not running. Start it with `/demo-site-management start`"
2. Launch default browser with discovered URL:
    - Windows: `powershell.exe -Command "Start-Process 'https://127.0.0.1:<port>'"`
    - Linux: `xdg-open https://127.0.0.1:<port>`
    - macOS: `open https://127.0.0.1:<port>`
3. Report:
    - Browser launched
    - URL opened
    - Note about certificate warning (self-signed HTTPS)
    - Credentials reminder: admin@example.com / password1234

## Get the Worktree's Dev Port

The demo site's port is assigned once (by `Umbraco.Community.WorktreeDevPort` on first run) and stored in this worktree's own git config — a plain read, no server round-trip needed to discover it:

```bash
git config --worktree --get wdp.port 2>/dev/null
```

Empty/no output means the site has never been started in this worktree yet. (Before the first assignment in a clone, git also complains that `extensions.worktreeConfig` isn't enabled — the package turns that on when it assigns the first port, hence the `2>/dev/null`.) A value means that's the port to use — probe `https://127.0.0.1:<port>` to confirm the site is actually up right now (the config value persists across restarts, so its presence alone doesn't mean the process is currently running).

The main checkout (not a linked worktree) gets `44380` when it's free, so a human working normally always finds the site at the familiar address. Linked worktrees skip straight to the auto-assigned pool (`44300` upward) and never take `44380`. That main-checkout port is set by `install-demo-site` as `WorktreeDevPort:MainWorktreePort` in the demo site's `appsettings.Development.json`.

This works identically whether you're in the main checkout or a linked worktree — git scopes `--worktree` config to whichever one you're currently in.

## Common Issues

### No port set yet

- The demo site has never been started in this worktree
- Solution: `/demo-site-management start`

### Port set but connection refused

- The value is stale from a previous run; the process isn't currently up
- Check with `/demo-site-management status`, then start it if needed — the same port will be reused

### Multiple worktrees

- Each worktree gets its own port automatically, with no collisions (a free port is verified before being assigned)
- Removing a worktree (`git worktree remove`) removes its saved port with it — nothing to clean up by hand

## Success Criteria

**After start**: Report task ID, port, and URL
**After stop**: Confirm process stopped successfully
**After status**: Show running state and port
