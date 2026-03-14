# Umbraco.Automate — CTO Brief

## What is it?

Umbraco.Automate is a Zapier/n8n-style automation engine embedded directly in the Umbraco backoffice. Editors build event-driven workflows visually — a trigger ("Content Published") plus a chain of actions ("Send Slack Message", "Update CRM") — without leaving the CMS or relying on external platforms.

It is the first CMS-embedded automation engine in the .NET ecosystem.

---

## Why now?

**Market gap**: No .NET open-source CMS has built-in automation. Sitecore and Kentico offer this in their commercial platforms. Umbraco users currently rely on Zapier, Power Automate, or custom code — all with significant trade-offs:

| Current approach | Problem |
|-----------------|---------|
| External platforms (Zapier, Power Automate) | Data leaves the CMS, expensive at scale, no native CMS event triggers |
| Custom code (notification handlers, hosted services) | Developer-only, no UI, no audit trail, not reusable |
| No automation | Manual repetitive tasks, no cross-product connectivity |

**DXP enabler**: Automate provides the shared bus that connects Forms, Commerce, Workflow, and Engage. Each product contributes triggers and actions without depending on each other — a core piece of the DXP integration story.

**AI readiness**: Bidirectional integration with Umbraco.AI — agents can trigger automations, and automations can invoke agents. Human-in-the-loop gates ensure AI-initiated actions are reviewed before execution.

---

## Architecture Decisions

### Workflow Engine: WorkflowCore (MIT)

[WorkflowCore](https://github.com/danielgerlag/workflow-core) provides the step execution backbone — we own everything else (UI, API, persistence, triggers, actions). Selected over Elsa Workflows due to licensing clarity (MIT vs custom SSPL-like), smaller footprint, and no architectural conflicts with Umbraco's patterns.

**Risk**: Single maintainer. **Mitigation**: MIT-licensed, ~5K LOC core — practical to fork if needed. Clean abstraction layer means the engine is swappable.

See Appendix A in the full proposal for the detailed decision record.

### Visual Editor: React Flow

[React Flow](https://reactflow.dev) — the most mature node-graph editor available (20K+ GitHub stars). Wrapped in a custom element to bridge into Umbraco's Lit-based backoffice. React is isolated to the canvas component only; the rest of the frontend is standard Umbraco Lit.

### Messaging: Custom Outbox

A lightweight custom outbox built on EF Core handles both trigger event dispatch and workflow step distribution. Single `umbracoAutomateOutbox` table, polled by a `BackgroundService`. This is an internal implementation detail — not exposed in any public API. Zero external dependencies.

**Why this matters for operations:**
- **Single node**: Database-backed outbox with polling. No external infrastructure needed.
- **Multi-node / load balanced**: Optimistic concurrency via `ClaimedByInstance` column ensures exactly-once consumption across nodes. All nodes participate in processing.
- **Resilience**: Exponential backoff retry, dead-lettering, stale claim recovery (handles crashed instances) — all built in.

### Database

Uses Umbraco's existing database (SQL Server or SQLite) by default. Optionally configurable to a separate database via `umbracoAutomateDbDSN` connection string (follows the Umbraco Commerce convention). EF Core migrations prefixed with `UmbracoAutomate_`.

---

## Extensibility Model

Triggers and actions are the extension points. Third-party developers (and DXP packages) create them as NuGet packages.

**Developer experience**: One class per trigger, one class per action. Attribute-driven metadata, auto-generated settings UI, auto-discovery at startup. A developer can create and test a custom action in under an hour.

**DXP integration**: Each product contributes its own triggers and actions via a dedicated package:

| Package | Owner | Examples |
|---------|-------|----------|
| `Umbraco.Forms.Automate` | Forms team | Form Submitted trigger, Export Entries action |
| `Umbraco.Commerce.Automate` | Commerce team | Order Placed trigger, Update Order Status action |
| `Umbraco.Workflow.Automate` | Workflow team | Approval Requested trigger, Approve Content action |
| `Umbraco.Engage.Automate` | Engage team | Segment Entered trigger, Assign Persona action |
| `Umbraco.AI.Automate` | AI team | AI agents as actions, AI events as triggers, automations as agent tools |
| `Umbraco.Automate.Deploy` | Automate team | Deploy integration for transferring automations between environments |

**Naming convention**: `{OwningProduct}.Automate` — follows the established DXP pattern (e.g. `Umbraco.Commerce.Deploy`). Each product team owns their Automate integration because they know their events and domain model best. Products depend only on `Umbraco.Automate.Core`, not on each other.

---

## AI Integration (via Umbraco.AI)

All AI capabilities come from Umbraco.AI. The integration is bidirectional:

- **AI → Automate**: Automations can execute AI agents as steps (e.g. Content Published → Translate Agent → Publish variants)
- **Automate → AI**: Automations can be exposed as tools that AI agents invoke (e.g. an agent sees a "publish-campaign-content" tool that triggers a multi-step automation)
- **AI events as triggers**: Agent lifecycle events (completed, failed) can start automations
- **Human-in-the-loop**: Configurable gates require human approval before AI-initiated automation steps execute

Deferred to Phase 3.

---

## Governance & Observability

- **Full audit trail**: Every run records inputs, outputs, errors, duration, and initiator (user, system, AI, webhook) at each step
- **Run Explorer**: Backoffice UI for investigating runs — filterable list, visual replay on the canvas, step-by-step data inspection
- **Draft/publish lifecycle**: Automations follow Umbraco's content model — draft, published, inactive. Version history with rollback. In-flight runs complete on the version they started with.
- **Access control**: Leverages Umbraco's existing user group/permission model
- **Failure notifications**: Configurable channels (email, webhook) per automation
- **Safety**: Rate limiting, kill switch (global + per-automation), timeout enforcement, trigger deduplication

---

## Load Balancing

Works out of the box in load-balanced Umbraco environments:

| Deployment | How it works |
|-----------|-------------|
| **Single node** | In-memory message transport. Zero external infrastructure. |
| **Multi-node** | All nodes poll the shared outbox table. Optimistic concurrency ensures exactly-once consumption. All nodes participate in trigger processing and step execution. |

Only scheduled (CRON) triggers are restricted to the SchedulingPublisher node. Everything else runs on any node.

---

## Phased Delivery

### Phase 1: Foundation (MVP)
Core engine, basic triggers/actions (Manual, Scheduled, Webhook, Content Published), canvas editor, run logging, draft/publish lifecycle, security hardening, developer tooling (test harness, project template).

### Phase 2: HITL, Branching & Hardening
Approval workflows, conditional branching (If/Switch), named connections with OAuth2, Deploy integration, failure notifications, dry run mode, automation templates.

### Phase 3: AI Integration
`Umbraco.AI.Automate` — agents as actions, AI events as triggers, automations as agent tools, HITL gates for AI-initiated runs.

### Phase 4: DXP Providers
Forms, Commerce, Workflow, and Engage integration packages.

### Phase 5: Advanced Features
Parallel execution, sub-automations, version diff, distributed tracing.

---

## Key Decisions

| Decision | Options considered | Chosen | Why |
|----------|-------------------|--------|-----|
| **Workflow engine** | WorkflowCore (MIT) vs Elsa Workflows (SSPL-like) | WorkflowCore | MIT licensing, smaller footprint (~5K LOC), no architectural conflicts with Umbraco. Elsa has a custom license restricting competing products, ships its own API/UI/designer that would conflict with ours. Full decision record in proposal Appendix A. |
| **Visual editor** | React Flow (React, wrapped) vs Rete.js (Lit-native) | React Flow | Most mature (20K+ stars), best UX, extensible. Rete.js is Lit-native but less polished. React isolated to one component via custom element. |
| **Messaging / resilience** | Custom outbox vs DotNetCore.CAP vs MassTransit vs Wolverine | Custom outbox | Zero external dependencies, uses existing EF Core infrastructure. Single table with optimistic concurrency, exponential backoff, dead-lettering. CAP had sealed internals requiring workarounds; MassTransit too heavy; Wolverine no SQLite. |
| **Trigger architecture** | Subscribe method on triggers vs activation interfaces | Activation interfaces | Triggers are definitions (metadata + output schema). Activation (which event to listen to) is declared via typed base classes (`NotificationTriggerBase`, `ScheduledTriggerBase`, etc.). Infrastructure auto-wires at startup. Subscribe method doesn't work because Umbraco notification handlers must be registered at DI composition time. |
| **Settings UI** | Hand-crafted UI per action vs auto-generated from POCO | Auto-generated | `[Field]` attribute on settings POCO properties drives config UI. Matches Umbraco.AI's established `[AIField]` pattern. Developers write a POCO, get a form. |
| **Binding syntax** | Reuse UFM code vs port UFM design | Port design | UFM is entirely frontend TypeScript — no server code to reuse. We adopt the `${ }` syntax and filter pipes, implemented as a purpose-built C# tokenizer/evaluator. |
| **Persistence** | Use WorkflowCore's EF tables vs custom tables | Custom `IPersistenceProvider` | WorkflowCore's EF provider targets EF 9.x — incompatible with Umbraco 17 (EF 10.x). Custom implementation uses Umbraco's EF scope, avoids version clash. |
| **Package naming** | `Umbraco.Automate.{Product}` vs `Umbraco.{Product}.Automate` | `{Product}.Automate` | Follows established DXP convention (`Umbraco.Commerce.Deploy`). Product team owns their integration — they know their events and domain model best. |
| **Load balancing** | SchedulingPublisher-only vs all-node execution | All nodes via outbox | Optimistic concurrency on `ClaimedByInstance` column guarantees exactly-once consumption. All nodes run WorkflowCore and process triggers. Only CRON triggers gated to SchedulingPublisher. |
| **Distributed locking** | Use Umbraco's `IDistributedLockingMechanism` vs independent | Independent (via outbox) | Umbraco's mechanism is scope-bound (requires active DB transaction) and uses integer lock IDs. WorkflowCore runs outside scopes, needs async string-key-based coordination. Outbox claim-based delivery handles this naturally. |

---

## Key Dependencies

| Dependency | License | Purpose | Risk |
|-----------|---------|---------|------|
| [WorkflowCore](https://github.com/danielgerlag/workflow-core) 3.9.x | MIT | Workflow step execution engine | Single maintainer. Small codebase (~5K LOC), forkable. Clean abstraction layer. |
| [React Flow](https://reactflow.dev) | MIT | Visual node-graph canvas editor | Large community (20K+ stars), active maintenance. Isolated to one component. |
| Custom outbox | — | Transactional outbox / messaging | Zero external dependencies. Built on EF Core. Single table, ~200 LOC. |
| Umbraco.AI | Umbraco | AI agent framework | Internal dependency, Phase 3 only. |

---

## Risk Summary

| Risk | Impact | Mitigation |
|------|--------|------------|
| WorkflowCore maintenance stalls | High | MIT, small codebase, forkable. Elsa monitored as alternative. |
| Performance at scale | High | Outbox-based distributed execution. Data retention purge. Queue depth limits. |
| Credential exposure | High | Encrypted at rest via `[Field(IsSensitive = true)]`, masked in logs, stripped from Deploy transfers |
| SSRF via HTTP Request action | High | URL allowlist/denylist blocking internal IPs. Enforced by default. |
| Trigger/action API stability | High | Careful interface design. Versioned contracts. Startup validation. |

---

## What You're Approving

1. **The product**: An automation engine embedded in Umbraco CMS, shipping as a commercial add-on
2. **The architecture**: WorkflowCore (execution) + React Flow (UI) + custom outbox (messaging) — all MIT / zero-dependency
3. **The phased approach**: MVP first (engine + canvas + basic triggers), then HITL, then AI, then DXP providers
4. **The extensibility model**: Triggers and actions as the extension API — third parties and DXP packages build on this

The full technical proposal with implementation details is available in [`docs/engineering-spec.md`](engineering-spec.md). A non-technical functional overview for broader stakeholders is in [`docs/functional-overview.md`](functional-overview.md).
