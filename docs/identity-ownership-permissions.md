# Identity, Ownership & Permissions

> Supplement to [engineering-spec.md](engineering-spec.md) — covers workspace-based access control, execution identity, CMS audit trail integration, and permissions.

---

## Workspaces

Automations are organised into **workspaces** — admin-configured containers that define who can work on automations within them and what resources (connections, service accounts) are available. The workspace is the primary access boundary and the "owner" of the automations within it.

```
Workspace
├── Id: Guid
├── Alias: string (unique, URL-safe — used for Deploy transfer)
├── Name: string
├── ServiceAccountKey: Guid            ← required — the identity all automations in this workspace run as
├── UserGroups: List<Guid>       ← user groups with access to this workspace
├── AllowedConnections: List<Guid>     ← connections available within this workspace
```

### How It Works

- An **admin** creates workspaces and configures their service account, member groups, and allowed connections
- Every workspace has a **required service account** — this is the identity all automations in the workspace execute as. The service account's user group permissions define what automations in this workspace are allowed to do.
- Automations live inside a workspace — folders can still exist within a workspace for organisation
- All members of a workspace can **view, edit, and publish** automations within it — equal standing
- Automation creators never choose a service account — it's inherited from the workspace
- The workspace constrains which connections are available to its automations

### Why Workspaces

- **Admin-controlled**: Prevents privilege escalation — only an admin decides which credentials and service accounts are available in a workspace
- **Easy to audit**: One place to see who has access to what, rather than checking each automation individually
- **Low management overhead**: Configure the workspace once, all automations within it inherit the access rules
- **Simple onboarding**: Add a user group to a workspace and its members immediately have access to all automations within it

**No default workspace**: A workspace must be created by an admin before anyone can create automations. This is part of initial setup — the same way document types must exist before content can be created. If a user has section access but isn't a member of any workspace, they can view the dashboard and run explorer but cannot create automations.

### Accountability Without Ownership

There is no `OwnedBy` field on automations. Accountability is tracked through existing mechanisms:

| Question | Answer | Source |
|----------|--------|--------|
| Who created this automation? | `CreatedBy` (immutable) | Automation entity |
| Who last modified it? | User key on latest version | Version history |
| Who published the current version? | User key on the publish event | Version history |
| Who triggered this specific run? | `InitiatedBy` | AutomationRun entity |
| What identity executed the CMS operations? | Service account | Workspace configuration + CMS audit trail |

If an automation causes problems, the version history provides a full timeline of who changed what and when. The workspace defines collective responsibility — all members share accountability for automations within it.

### Draft Indicator (Entity Sign)

Automations with unpublished draft changes display an entity sign on the tree item — the same pattern Umbraco content uses for pending changes. Implemented via a custom `IFlagProvider` and `entitySign` manifest using the CMS's built-in entity sign system.

---

## Service Accounts (Execution Identity)

Every workspace has a **required service account** — a `UserKind.Api` user in Umbraco that defines the identity and permission boundary for all automations in that workspace. Automation creators never choose or think about service accounts — the admin configures it once on the workspace.

### Why Service Accounts

- **Permission scoping**: The service account's user group permissions define what automations in the workspace can do. An automation in the "Marketing" workspace can't publish content outside `/campaigns/` if the workspace's service account doesn't have permission to.
- **CMS audit trail**: All CMS operations show the named API user (e.g., "marketing-automate-bot"), not a generic `SYSTEM` identity.
- **Least privilege**: The admin scopes the service account to the minimum permissions the workspace needs. No SYSTEM fallback, no unrestricted access.
- **No privilege escalation**: Automation creators can only build automations that operate within the workspace's service account permissions. They can't choose a more powerful identity.

### How They Work

Service accounts are standard Umbraco `UserKind.Api` users. They:

- Are created and managed through Umbraco's existing user management UI/API
- Cannot log into the backoffice (API-only)
- Are assigned to user groups with specific permissions (e.g., "can publish content under /campaigns/")
- Appear in the CMS audit trail with their own identity
- Can be shared across workspaces with similar permission needs

Create service accounts by **permission boundary** (e.g., "marketing-automate-bot", "member-management-bot"), not one per workspace. Multiple workspaces can reference the same service account if they need the same permissions.

### Runtime Behaviour

All automations in a workspace execute as the workspace's service account:

- CMS operations (Publish Content, Create Member, etc.) execute subject to the service account's user group permissions
- If the service account lacks permission for an operation (e.g., can't publish under `/restricted/`), the step **fails** with an `Authentication` error category
- If the service account is disabled, **all automations in the workspace fail** with a clear error — the dashboard surfaces this prominently
- Non-CMS actions (HTTP Request, Log Message, etc.) don't interact with Umbraco's permission system but still execute in the context of the workspace's service account for audit purposes

---

## Access Management

Connections are controlled at the **workspace level**. The service account is a required workspace-level setting. Admins configure which connections are available within each workspace, and workspace membership determines who can use them.

### Connection Access

| Resource | Admin configures on workspace | Picker shows to automation creator | Default |
|----------|------------------------------|-----------------------------------|---------|
| **Connections** | Which connections are available in this workspace | Connections allowed in the automation's workspace | None (explicit opt-in) |

New workspaces start with **no connections** — an admin must explicitly add them. This prevents accidental exposure of credentials.

### Management UI

Workspace management lives in the Automations section settings:

- **Workspaces** — create/edit workspaces, assign service account, configure member groups, and allowed connections
- **Connections** — create/edit connections (credentials, type, settings). Workspace assignment is managed on the workspace, not the connection.

### Validation Points

| When | What's checked |
|------|---------------|
| **Create workspace** | Service account is assigned (required field) |
| **Add connection to step** | Connection is allowed in the automation's workspace |
| **Publish** | All connections re-validated against workspace. Service account is still active. |
| **Workspace connection removal** | If admin removes a connection from a workspace, affected published automations are flagged on the dashboard |
| **Service account disabled** | All automations in the workspace are flagged on the dashboard |

---

## Permissions

### Section Access (Baseline)

Access to the Automations section is controlled by user group → section assignment, following the standard Umbraco pattern (same as Umbraco.AI):

```csharp
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(AutomateAuthorizationPolicies.SectionAccessAutomate, policy =>
    {
        policy.RequireClaim(Constants.Security.AllowedApplicationsClaimType, AutomateConstants.Sections.Automate);
    });
});
```

### Workspace Membership

Within a workspace, all members have equal standing. Workspace membership (via user groups) grants the ability to view, create, edit, publish, and execute automations within that workspace.

### Permissions Summary

| Permission | Level | Purpose |
|------------|-------|---------|
| Automations section access | Section | Access to the section. Without workspace membership, the user sees an empty state with a prompt to contact an administrator. |
| Workspace membership | Workspace | Full access to automations within the workspace — create, edit, publish, delete, execute, and view run data (including step input/output payloads) |
| Administration access | See below | Workspace management, connection management, service account assignment |

Run data visibility is scoped to workspace membership. If you're in the workspace, you can see full run detail including step payloads. If you're not, you can't see the automations at all. No separate permission needed.

Within a workspace, all members have equal standing — no per-user roles. If finer-grained access is needed, create separate workspaces.

### Administration UI Location (Undecided)

Workspace management, connection management, and service account assignment need a home. The persona performing these tasks is typically the same person who configures document types, user groups, and other CMS infrastructure. Three options under consideration:

| Option | Where admin lives | Gate | Pros | Cons |
|--------|------------------|------|------|------|
| **Settings section** | Tree node under Settings | Settings section access | Same place as doc types, data types — familiar for CMS admins | Automate admin split from Automate usage |
| **Users section** | Tree node under Users | Users section access | Workspaces are primarily access control — natural fit alongside user groups | Connections are infrastructure, not users |
| **Automations section** | Separate area within the Automations section | Requires permission filtering within the section | Everything in one place | Needs additional permission logic to hide admin UI from non-admins |

This is a UX/information architecture decision that may benefit from mockups and stakeholder input. The permission model works with any option — the admin surface just needs to be gated to the appropriate users.

---

## CMS Audit Trail Integration

When automation actions modify CMS entities (publish content, create members, etc.), the operations appear in the CMS's native audit trail with full context.

### Audit Entry Fields

Umbraco's audit model has three free-form text fields (1024 chars each) with **no foreign key constraints** — entries survive user deletion:

| Field | What we write |
|-------|--------------|
| `PerformingUserId` | Service account's user ID |
| `PerformingDetails` | `"Umbraco Automate: '{automationName}' (Run {runId})"` |
| `EventDetails` | Structured context: `{"automationId": "...", "runId": "...", "stepId": "...", "workspace": "...", "initiatedBy": "user:admin@example.com"}` |

### Identity Resolution

| Run initiated by | `PerformingUserId` | `PerformingDetails` includes |
|-------------------|-------------------|------------------------------|
| Manual trigger (backoffice user) | Service account ID | Automation name + run ID + triggering user |
| Scheduled (CRON) | Service account ID | Automation name + run ID + "scheduled" |
| Webhook | Service account ID | Automation name + run ID + "webhook" |
| AI agent | Service account ID | Automation name + run ID + agent alias |

### Cross-Referencing

An editor viewing a content node's audit history sees:

> Published by **content-publisher-bot** — _"Umbraco Automate: 'Notify Editors on Publish' (Run abc-123)"_

The automation name and run ID in `PerformingDetails` allow cross-referencing to the Run Explorer for full step-by-step detail. The `EventDetails` JSON provides structured data for programmatic lookups.

### Layers of Accountability

Every content operation performed by an automation carries:

| Layer | What it answers | Where it's recorded |
|-------|----------------|-------------------|
| **Execution identity** | Which bot did it? | `PerformingUserId` (CMS audit trail) |
| **Automation context** | Which automation and run? | `PerformingDetails` + `EventDetails` (CMS audit trail) |
| **Change history** | Who built/modified this automation? | Version history (Automate) |
| **Initiator** | What triggered this specific run? | `AutomationRun.InitiatedBy` + `EventDetails` |

---

## Deploy Implications

Workspaces and service accounts have environment-specific aspects that affect how automations transfer between environments via Umbraco Deploy.

### What Transfers

| Data | Transfers? | Resolution on target |
|------|-----------|---------------------|
| Automation definition | Yes | Steps, connections by alias, canvas state |
| `CreatedBy` | Preserved | Nullable GUID — referenced user may not exist on target (no FK constraint) |
| `WorkspaceId` | **By alias** | Workspace alias stored in artifact. Matched on target. Missing → import blocked until workspace is created. |
| `ServiceAccountKey` (on workspace) | **By username** | API user username stored in artifact (follows CMS user picker convention). Resolved by username lookup on target. Missing → workspace flagged, all its automations blocked from publishing until assigned. |
| Connection references | **By alias** | Existing behaviour from engineering-spec.md |

### How It Works

**Export**: The Deploy connector serializes `WorkspaceId` → workspace alias, workspace `ServiceAccountKey` → API user username, and connection references → connection aliases.

**Import**: The connector resolves aliases/usernames on the target environment:
- Workspace: matched by alias. Missing → import blocked until a workspace with the matching alias is created on the target.
- Service account (on workspace): matched by username. Missing → workspace flagged, automations blocked from publishing.
- Connections: matched by alias. Missing → draft state + dashboard warning (existing behaviour from engineering-spec.md).

**User groups on workspaces**: User groups are **not** transferable entities in Deploy. Workspace membership (`UserGroups`) is stripped from the artifact on export. The workspace arrives on the target with its service account and allowed connections resolved, but with no member groups — the admin configures membership as part of environment setup.

### Post-Import Checklist

The dashboard surfaces a clear checklist for imported automations:

- Missing workspace service account → "Workspace '{alias}' requires a service account before its automations can be published"
- Missing connections → "Configure connection '{alias}' before publishing"
- Missing workspace → "Create workspace '{alias}' before importing automation '{name}'"

---

## Updated Domain Model

Additions to the automation entity from [engineering-spec.md](engineering-spec.md):

```
Automation
├── ...existing fields...
├── CreatedBy: Guid (user key, immutable — who originally created the automation)
├── WorkspaceId: Guid (workspace this automation belongs to)

Workspace
├── Id: Guid
├── Alias: string (unique, URL-safe — used for Deploy transfer)
├── Name: string
├── ServiceAccountKey: Guid (required — UserKind.Api user, execution identity for all automations)
├── UserGroups: List<Guid>
├── AllowedConnections: List<Guid>

Connection (unchanged from engineering-spec.md — access controlled at workspace level)
```

---

## Resolved Decisions

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| 1 | Connection/service account access default | **No access** (explicit opt-in at workspace level) | Prevents accidental credential exposure. Admin must consciously add resources to a workspace. |
| 2 | Draft state visibility | **Entity sign** — same as CMS content | Draft indicator on tree items using the CMS's built-in entity sign system. |
| 3 | Shared editing model | **Workspaces** (admin-configured, user group-based) | Admin controls the boundary — who can access automations and which resources are available. All workspace members have equal standing. |
| 4 | Service account assignment | **Required on workspace, not per-automation** | Admin assigns a service account when creating a workspace. All automations in the workspace inherit it. Automation creators never choose a service account — eliminates "pick the most powerful one" behaviour. No SYSTEM fallback. |
| 5 | Ownership model | **No explicit owner — workspace + version history** | `CreatedBy` + version history provide full accountability. The workspace is the collective owner. Eliminates ownership transfer complexity. |

## Open Questions

1. Where should the administration UI live — Settings section, Users section, or within the Automations section?
