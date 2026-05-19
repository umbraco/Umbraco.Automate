# Umbraco.Automate — Initial Proposal

## Vision

Umbraco.Automate brings Zapier/n8n-style automation directly into Umbraco CMS, enabling editors, developers, and AI agents to build event-driven workflows without leaving the backoffice. It is the first CMS-embedded automation engine in the .NET ecosystem — filling a gap that Sitecore and Kentico have addressed in their platforms.

---

## Market Context

### What exists today

| Platform | Model | Strengths | Gaps |
|----------|-------|-----------|------|
| **Zapier** | Cloud SaaS, 8 000+ connectors | Massive connector library, ease of use | No self-hosting, expensive at scale, limited branching |
| **n8n** | Open-source, self-hostable | Best AI/LLM integration (~70 AI nodes), full branching/loops, extensible node SDK | Smaller connector library, steeper learning curve |
| **Make** | Cloud SaaS | Routers/iterators for complex logic, good visual UX | No self-hosting |
| **Power Automate** | Microsoft 365 ecosystem | Native approvals, enterprise governance (DLP, RBAC) | Microsoft-centric |
| **Sitecore XP** | CMS-embedded | Marketing automation with visual builder, contact enrollment, rules engine | Monolithic, expensive, marketing-focused only |
| **Kentico** | CMS-embedded | Cross-channel automation, AI-powered content lifecycle | Closed ecosystem |

### Our opportunity

No .NET open-source CMS has a built-in automation engine. Umbraco users currently rely on external platforms (Zapier, Power Automate) or custom code. Umbraco.Automate can:

1. **Reduce integration friction** — triggers fire from Umbraco events natively, no webhooks needed
2. **Keep data in-house** — no need to send content/member data to third-party automation platforms
3. **Enable the DXP story** — connect Forms, Commerce, Workflow, and Engage through a shared automation bus
4. **Support AI agents** — automations can be created, triggered, and monitored by Umbraco's AI agent framework
5. **Governance by default** — full audit trail built into the CMS, not bolted on

---

## Architecture Overview

### Runtime Engine: WorkflowCore

[WorkflowCore](https://github.com/danielgerlag/workflow-core) (v3.9.0) provides the execution backbone:

| Capability | WorkflowCore Support |
|------------|---------------------|
| Step execution with DI | `StepBody` / `StepBodyAsync` with constructor injection |
| Data flow between steps | Explicit `Input()`/`Output()` mappings on a typed data POCO |
| Persistence between steps | Custom `IPersistenceProvider` using Umbraco's EF Core infrastructure (see below) |
| HITL / Wait states | `WaitFor` (external events) and Activities (work queue) |
| Branching | `If`, `Branch` (multi-outcome), `Parallel`, `While`, `ForEach` |
| Error handling | Per-step: Retry, Suspend, Terminate, Compensate (Sagas) |
| Middleware | `IWorkflowStepMiddleware` (per-step), `IWorkflowMiddleware` (pre/post workflow) |
| JSON/YAML definitions | `WorkflowCore.DSL` for runtime-loaded definitions |
| Observability | OpenTelemetry built-in |

**Key limitation to mitigate**: WorkflowCore has no visual designer or REST API — we build both. Its polling-based execution model adds latency; we may need to optimize the poll interval or consider a push notification layer for time-sensitive automations.

**Risk**: Single-maintainer project. Mitigation: we depend on the stable 3.x API surface. If maintenance stalls, the library is small enough to fork/vendor. Elsa Workflows exists as a fallback but has a much larger footprint. See Appendix A for the full comparison.

**NuGet dependency**: We reference only the `WorkflowCore` core package (`netstandard2.0`), **not** any `WorkflowCore.Persistence.*` packages. This avoids the EF Core version conflict (WorkflowCore's EF provider uses EF 9.x; Umbraco 17 uses EF 10.x) and the Newtonsoft.Json dependency stays isolated to the engine layer.

### Persistence: Custom IPersistenceProvider

We implement WorkflowCore's `IPersistenceProvider` interface directly, using Umbraco's `IEFCoreScopeProvider` for database access. This bypasses WorkflowCore's own EF provider entirely.

**Why**: WorkflowCore's EF provider targets net8.0 with EF Core 9.x — incompatible with Umbraco 17 on .NET 10 / EF Core 10.x. A custom implementation avoids the version clash, integrates with Umbraco's migration and scope infrastructure, and uses the `UmbracoAutomate_` migration prefix convention.

**Interface surface** (25 members across 4 sub-interfaces):

| Sub-interface | Methods | Entities |
|--------------|---------|----------|
| `IWorkflowRepository` | 7 | `WorkflowInstance` (with `ExecutionPointer` child collection) |
| `ISubscriptionRepository` | 7 | `EventSubscription` |
| `IEventRepository` | 6 | `Event` |
| `IScheduledCommandRepository` | 2 + 1 property | `ScheduledCommand` |
| `IPersistenceProvider` (direct) | 2 | `ExecutionError` + `EnsureStoreExists()` |

**Implementation approach**:
- ~400 lines, modelled on WorkflowCore's `EntityFrameworkPersistenceProvider` (~350 lines) and `MemoryPersistenceProvider` (~225 lines) as references
- Define our own `Persisted*` entity types (5-6 models) with EF Core configuration under Umbraco's `DbContext`
- Each method creates and completes its own `IEFCoreScopeProvider` scope
- `object`-typed properties (`Data`, `PersistenceData`, `EventData`, etc.) serialized via Newtonsoft.Json with `TypeNameHandling` matching WorkflowCore's expectations — this is the one place Newtonsoft.Json is required
- Registered in DI, replacing WorkflowCore's default in-memory provider

**Two persistence layers**:

| Layer | What it stores | Implementation |
|-------|---------------|----------------|
| **WorkflowCore engine state** | Workflow instances, execution pointers, events, subscriptions, scheduled commands | Custom `IPersistenceProvider` via Umbraco's EF scope |
| **Automation run audit** | `AutomationRun`, `StepRun` (richer schema with input/output data, error categories, duration) | Our own repositories via Umbraco's EF scope |

The engine state layer is WorkflowCore's internal bookkeeping. The audit layer is our governance/observability data with a richer schema purpose-built for the Run Explorer.

### Visual Editor: React Flow (via Custom Element)

**Decision**: [React Flow](https://reactflow.dev) (`@xyflow/react`) — the most polished and configurable node-based editor available. ~500KB after tree-shaking (further reducible with gzip). Wrapped in a custom element to bridge into Umbraco's Lit-based backoffice.

**Why React Flow**:
- Most feature-rich: custom nodes/edges, minimap, controls, keyboard shortcuts, copy/paste, undo/redo
- Huge community (20k+ GitHub stars), excellent documentation, active maintenance
- Built-in `toObject()` serialization to JSON — maps directly to our persistence model
- Stress-tested at 450+ nodes; 100-node workflows are comfortable
- Extensive theming via CSS variables

**Integration approach**: React Flow renders inside a custom element (`<umb-automate-canvas>`) that bridges to the Lit host:
- **Inbound**: Lit passes automation data via properties/attributes on the custom element
- **Outbound**: React Flow dispatches custom DOM events (`automation-changed`, `node-selected`, etc.) that Lit components listen to
- **Styles**: React Flow CSS is injected into the shadow root; Umbraco design tokens are mapped to React Flow's CSS variables for visual consistency
- **Lifecycle**: React root is created in `connectedCallback()` and unmounted in `disconnectedCallback()`

This is a well-established pattern — React-in-custom-element is used in production by many mixed-framework applications. The React runtime is isolated to the canvas component only.

### Message Infrastructure: Custom Outbox

Trigger event dispatch and WorkflowCore's internal work queue are powered by a **custom lightweight outbox** built on EF Core. This replaces a previous dependency on `DotNetCore.CAP` — the custom implementation is simpler, has zero external dependencies, and eliminates timing/startup issues that arose from CAP's architecture.

| Capability | Implementation |
|-----------|-------------|
| Outbox persistence | Single `umbracoAutomateOutbox` table via EF Core |
| Retry with backoff | Exponential backoff (`BaseRetryDelay * 2^retryCount`), configurable max retries |
| Dead-lettering | Messages exceeding max retries are marked `DeadLettered` for inspection |
| Multi-instance safety | Optimistic concurrency via `ClaimedByInstance` column + stale claim recovery |
| Stale claim recovery | Claims older than `StaleClaimThreshold` are released for reprocessing |
| Cleanup | Completed messages purged after configurable retention period |

**The outbox is used for two purposes:**
1. **Trigger dispatch** — `ITriggerDispatcher` publishes trigger events, `TriggerEventHandler` does the fan-out
2. **WorkflowCore queue** — custom `IQueueProvider` backed by the outbox, so step execution is distributed across nodes

The outbox is an internal implementation detail — not exposed in any public API, interface, or model. All public-facing code interacts only with `ITriggerDispatcher` and WorkflowCore's standard interfaces.

**Configuration** (all optional, via `Umbraco:Automate:Outbox` in appsettings):

| Option | Default | Description |
|--------|---------|-------------|
| `PollInterval` | 500ms | How often the dispatcher polls for messages |
| `MaxRetries` | 3 | Delivery attempts before dead-lettering |
| `BaseRetryDelay` | 5s | Base delay for exponential backoff |
| `StaleClaimThreshold` | 5min | How long before a claimed message is considered stale |
| `CompletedRetention` | 1 day | How long completed messages are kept |

---

## Extensibility Model (Triggers & Actions)

There is no monolithic "provider" interface. Triggers and actions are **independent, self-describing types** that implement dedicated interfaces. They are registered individually — either explicitly via collection builders or dynamically by type scanning.

### Design

Follows the same patterns established in **Umbraco.AI**: single class per component, attribute-driven metadata, `LazyCollectionBuilderBase` for registration, auto-discovery via `TypeLoader.GetTypesWithAttribute<>()`, and generic base classes with infrastructure injection for typed settings.

### Attributes

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TriggerAttribute(string alias, string name) : Attribute
{
    public string Alias { get; } = alias;
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ActionAttribute(string alias, string name) : Attribute
{
    public string Alias { get; } = alias;
    public string Name { get; } = name;
}
```

### Interfaces & Base Classes

```csharp
// ── Triggers ──────────────────────────────────────────

// A trigger has two concerns:
//   1. DEFINITION (ITrigger) — metadata, settings schema, output schema
//   2. ACTIVATION — how it's wired to an event source
//
// Both are implemented in a single class. The trigger declares its definition
// via TriggerBase<TSettings, TOutput> and its activation via a marker interface
// (INotificationTrigger<T>, IScheduledTrigger, IWebhookTrigger, etc.).
// Infrastructure auto-discovers and wires both at startup.

// ── Definition ───────────────────────────────────────

public interface ITrigger
{
    string Alias { get; }
    string Name { get; }
    string? Description { get; }
    string? Group { get; }
    string? Icon { get; }
    Type? SettingsType { get; }    // Input: drives config UI
    Type? OutputType { get; }      // Output: drives binding autocompletion
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Describes the output properties this trigger produces, for binding autocompletion
    /// and validation. Auto-derived from OutputType by the base class — override only if needed.
    /// </summary>
    IReadOnlyList<TriggerOutputProperty> GetOutputProperties();
}

// Base class — reads alias/name from attribute, builds schema from TSettings,
// auto-derives output properties from TOutput via reflection.
public abstract class TriggerBase<TSettings, TOutput> : ITrigger
    where TSettings : class, new()
    where TOutput : class
{
    private readonly ITriggerInfrastructure _infrastructure;

    public string Alias { get; }
    public string Name { get; }
    public abstract string? Description { get; }
    public abstract string? Group { get; }
    public abstract string? Icon { get; }
    public Type SettingsType => typeof(TSettings);
    public Type OutputType => typeof(TOutput);

    protected TriggerBase(ITriggerInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
        var attr = GetType().GetCustomAttribute<TriggerAttribute>()
            ?? throw new InvalidOperationException("Missing [Trigger] attribute");
        Alias = attr.Alias;
        Name = attr.Name;
    }

    public EditableModelSchema? GetSettingsSchema()
        => _infrastructure.SchemaBuilder.BuildForType<TSettings>(Alias);

    // Auto-derived from TOutput — each public property becomes a TriggerOutputProperty.
    // Override if you need custom descriptions or nested property flattening.
    public virtual IReadOnlyList<TriggerOutputProperty> GetOutputProperties()
        => OutputPropertyReflector.FromType<TOutput>();
}

// ── Activation Interfaces (internal — infrastructure uses these for scanning) ──

// Base contract — all event-driven activation interfaces derive from this.
// Maps an external event to one or more TriggerEvents for dispatch.
public interface IEventTrigger<in TEvent>
{
    IEnumerable<TriggerEvent> MapEvent(TEvent @event);
}

// CMS notifications — e.g. ContentPublishedNotification
public interface INotificationTrigger<in TNotification> : IEventTrigger<TNotification>
    where TNotification : INotification { }

// Scheduled triggers — declares CRON expression
public interface IScheduledTrigger
{
    string GetCronExpression(object? settings);
}

// Webhook triggers — declares HTTP method/route
public interface IWebhookTrigger
{
    IEnumerable<string> AllowedMethods { get; }
}

// DXP add-on packages define their own (in their respective packages):
// public interface ICommerceTrigger<in TEvent> : IEventTrigger<TEvent> { }
// public interface IFormsTrigger<in TEvent> : IEventTrigger<TEvent> { }

// ── Convenience Base Classes (developer-facing API) ──────────────────

// Developers pick a base class — IntelliSense shows these when typing ": Trigger".
// Each combines TriggerBase (definition) + activation interface in one inheritance.
// The abstract method tells developers exactly what to implement.

public abstract class NotificationTriggerBase<TSettings, TOutput, TNotification>
    : TriggerBase<TSettings, TOutput>, INotificationTrigger<TNotification>
    where TSettings : class, new()
    where TOutput : class
    where TNotification : INotification
{
    protected NotificationTriggerBase(ITriggerInfrastructure infra) : base(infra) { }
    public abstract IEnumerable<TriggerEvent> MapEvent(TNotification notification);
}

public abstract class ScheduledTriggerBase<TSettings, TOutput>
    : TriggerBase<TSettings, TOutput>, IScheduledTrigger
    where TSettings : class, new()
    where TOutput : class
{
    protected ScheduledTriggerBase(ITriggerInfrastructure infra) : base(infra) { }
    public abstract string GetCronExpression(object? settings);
}

public abstract class WebhookTriggerBase<TSettings, TOutput>
    : TriggerBase<TSettings, TOutput>, IWebhookTrigger
    where TSettings : class, new()
    where TOutput : class
{
    protected WebhookTriggerBase(ITriggerInfrastructure infra) : base(infra) { }
    public abstract IEnumerable<string> AllowedMethods { get; }
}

// DXP add-on packages provide their own convenience base classes:
// public abstract class CommerceTriggerBase<TSettings, TOutput, TEvent>
//     : TriggerBase<TSettings, TOutput>, ICommerceTrigger<TEvent> { ... }
// public abstract class FormsTriggerBase<TSettings, TOutput, TEvent>
//     : TriggerBase<TSettings, TOutput>, IFormsTrigger<TEvent> { ... }

// ── Trigger Output Models ────────────────────────────────────────────

// Each trigger defines a strongly-typed POCO for its output.
// These are compile-time safe at the handler boundary, then flattened
// to a dictionary at the dispatcher for the dynamic binding engine.

public class ContentPublishedOutput
{
    public required string ContentName { get; init; }
    public required Guid ContentKey { get; init; }
    public required string ContentType { get; init; }
}

// ── Trigger Events (strongly-typed) ──────────────────────────────────

// Non-generic base — what the dispatcher interface accepts
public class TriggerEvent
{
    public required string TriggerAlias { get; init; }
    public required string InitiatorType { get; init; }  // "system", "user", "webhook", "ai"
    public string? InitiatorId { get; init; }
}

// Internal interface for the dispatcher to extract output without knowing TOutput
internal interface ITriggerEventWithOutput
{
    object GetOutput();
}

// Generic — carries strongly-typed output, created by MapEvent implementations
public class TriggerEvent<TOutput> : TriggerEvent, ITriggerEventWithOutput where TOutput : class
{
    public required TOutput Output { get; init; }
    object ITriggerEventWithOutput.GetOutput() => Output;
}

// ── Infrastructure (auto-wiring) ─────────────────────────────────────

// A single generic handler class wires ALL notification-based triggers.
// At startup, infrastructure scans for INotificationTrigger<T>, discovers
// the unique TNotification types, and registers this handler for each.
// No per-trigger handler classes needed.

internal class TriggerNotificationHandler<TNotification>(
    IEnumerable<INotificationTrigger<TNotification>> triggers,
    ITriggerDispatcher dispatcher) : INotificationAsyncHandler<TNotification>
    where TNotification : INotification
{
    public async Task HandleAsync(TNotification notification, CancellationToken ct)
    {
        foreach (var trigger in triggers)
        foreach (var evt in trigger.MapEvent(notification))
            await dispatcher.DispatchAsync(evt, ct);
    }
}

// Startup registration (simplified):
// foreach (var notificationType in discoveredNotificationTypes)
// {
//     var handlerType = typeof(TriggerNotificationHandler<>).MakeGenericType(notificationType);
//     builder.AddNotificationAsyncHandler(notificationType, handlerType);
// }
//
// Each DXP add-on package does the same for its event system — registers a
// generic handler for ICommerceTrigger<T>, IFormsTrigger<T>, etc.

// ── Example: Complete Trigger (single class) ─────────────────────────

// Developer picks NotificationTriggerBase, implements MapEvent — done.
// No separate handler class, no manual DI registration, no activation interface to find.

[Trigger("contentPublished", "Content Published")]
public class ContentPublishedTrigger
    : NotificationTriggerBase<ContentPublishedSettings, ContentPublishedOutput, ContentPublishedNotification>
{
    public override string Description => "Fires when content is published";
    public override string Group => "Content";
    public override string Icon => "icon-globe";

    public ContentPublishedTrigger(ITriggerInfrastructure infra) : base(infra) { }

    public override IEnumerable<TriggerEvent> MapEvent(ContentPublishedNotification notification)
    {
        return notification.PublishedEntities.Select(content =>
            new TriggerEvent<ContentPublishedOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = "system",
                Output = new ContentPublishedOutput
                {
                    ContentName = content.Name!,
                    ContentKey = content.Key,
                    ContentType = content.ContentType.Alias,
                }
            });
    }
}

// ── Actions ───────────────────────────────────────────

public interface IAction
{
    string Alias { get; }
    string Name { get; }
    string? Description { get; }
    string? Group { get; }
    string? Icon { get; }
    Type? SettingsType { get; }
    EditableModelSchema? GetSettingsSchema();

    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken ct);
}

// Base class — same pattern as triggers
public abstract class ActionBase<TSettings> : IAction
    where TSettings : class, new()
{
    private readonly IActionInfrastructure _infrastructure;

    public string Alias { get; }
    public string Name { get; }
    public abstract string? Description { get; }
    public abstract string? Group { get; }
    public abstract string? Icon { get; }
    public Type SettingsType => typeof(TSettings);

    protected ActionBase(IActionInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
        var attr = GetType().GetCustomAttribute<ActionAttribute>()
            ?? throw new InvalidOperationException("Missing [Action] attribute");
        Alias = attr.Alias;
        Name = attr.Name;
    }

    public EditableModelSchema? GetSettingsSchema()
        => _infrastructure.SchemaBuilder.BuildForType<TSettings>(Alias);

    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken ct);
}
```

### Settings (follows Umbraco.AI `[AIField]` pattern)

```csharp
// Attribute for settings fields — mirrors AIEditableModelFieldAttribute
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute : Attribute
{
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? EditorUiAlias { get; set; }  // e.g. "Umb.PropertyEditorUi.TextBox"
    public string? EditorConfig { get; set; }
    public int SortOrder { get; set; }
    public bool IsSensitive { get; set; }       // Encrypted at rest, masked in run logs
    public string? Group { get; set; }
}

// Settings POCO example
public class SlackMessageSettings
{
    [Field(IsSensitive = true)]
    [Required]
    public string? WebhookUrl { get; set; }

    [Field(EditorUiAlias = "Umb.PropertyEditorUi.TextArea",
               EditorConfig = """[{ "alias": "rows", "value": 5 }]""")]
    [Required]
    public string? Message { get; set; }
}
```

The `EditableModelSchemaBuilder` (mirrors `AIEditableModelSchemaBuilder`) auto-discovers properties via reflection, infers editor UIs from C# types, collects validation attributes, and extracts default values — exactly as Umbraco.AI does.

### Example: Complete Action

```csharp
[Action("sendSlackMessage", "Send Slack Message")]
public class SendSlackMessageAction : ActionBase<SlackMessageSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public override string? Description => "Sends a message to a Slack channel via webhook";
    public override string? Group => "Slack";
    public override string? Icon => "icon-chat";

    public SendSlackMessageAction(
        IActionInfrastructure infrastructure,
        IHttpClientFactory httpClientFactory)
        : base(infrastructure)
        => _httpClientFactory = httpClientFactory;

    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken ct)
    {
        var settings = context.GetSettings<SlackMessageSettings>();
        // ... send message via webhook
        return ActionResult.Success(new { MessageId = "..." });
    }
}
```

### Registration

Uses `LazyCollectionBuilderBase` with auto-discovery via `TypeLoader` — same pattern as `AIProviderCollectionBuilder`:

```csharp
// Collection builders
public class TriggerCollectionBuilder
    : LazyCollectionBuilderBase<TriggerCollectionBuilder, TriggerCollection, ITrigger>
{
    protected override TriggerCollectionBuilder This => this;
}

public class ActionCollectionBuilder
    : LazyCollectionBuilderBase<ActionCollectionBuilder, ActionCollection, IAction>
{
    protected override ActionCollectionBuilder This => this;
}
```

```csharp
// UmbracoBuilderExtensions.Collections.cs (in Umbraco.Automate.Extensions namespace)
public static TriggerCollectionBuilder Triggers(this IUmbracoBuilder builder)
    => builder.WithCollectionBuilder<TriggerCollectionBuilder>();

public static ActionCollectionBuilder Actions(this IUmbracoBuilder builder)
    => builder.WithCollectionBuilder<ActionCollectionBuilder>();
```

```csharp
// Main entry point — accepts optional WorkflowCore options callback
public static IUmbracoBuilder AddUmbracoAutomate(
    this IUmbracoBuilder builder,
    Action<WorkflowOptions>? configureWorkflow = null)
{
    builder.AddUmbracoAutomateCore();
    builder.AddUmbracoAutomatePersistence();
    builder.AddUmbracoAutomateWeb();
    builder.AddUmbracoAutomateWorkflowEngine(configureWorkflow);
    return builder;
}

// Auto-discovers all triggers and actions
internal static IUmbracoBuilder AddUmbracoAutomateCore(this IUmbracoBuilder builder)
{
    builder.Triggers()
        .Add(() => builder.TypeLoader.GetTypesWithAttribute<ITrigger, TriggerAttribute>(cache: true));

    builder.Actions()
        .Add(() => builder.TypeLoader.GetTypesWithAttribute<IAction, ActionAttribute>(cache: true));

    return builder;
}

// Registers EF Core persistence — resolved via DatabaseConnectionInfo (explicit opt-in)
internal static IUmbracoBuilder AddUmbracoAutomatePersistence(this IUmbracoBuilder builder)
{
    builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>(
        (serviceProvider, options, _, _) =>
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();

        // Resolve from dedicated umbracoAutomateDbDSN, OR umbracoDbDSN when
        // Umbraco:Automate:UseUmbracoDbDSN is explicitly set. Throws if neither is configured.
        var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(config);

        switch (providerName)
        {
            case Constants.ProviderNames.SQLServer:
                options.UseSqlServer(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.Persistence.SqlServer");
                    x.MigrationsHistoryTable("__UmbracoAutomate_MigrationsHistory");
                });
                break;

            case Constants.ProviderNames.SQLLite:
            case "Microsoft.Data.SQLite":
                options.UseSqlite(connectionString, x =>
                {
                    x.MigrationsAssembly("Umbraco.Automate.Persistence.Sqlite");
                    x.MigrationsHistoryTable("__UmbracoAutomate_MigrationsHistory");
                });
                break;
        }
    });

    return builder;
}

// Registers WorkflowCore with Umbraco's connection string as default
internal static IUmbracoBuilder AddUmbracoAutomateWorkflowEngine(
    this IUmbracoBuilder builder,
    Action<WorkflowOptions>? configureWorkflow = null)
{
    builder.Services.AddWorkflow(options =>
    {
        // Default: use Umbraco's connection string for persistence
        // Queue + lock default to in-memory (single-node)

        // Let the user override/extend
        configureWorkflow?.Invoke(options);
    });

    return builder;
}
```

Third-party packages register explicitly:

```csharp
public class SlackAutomateComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Actions()
            .Add<SendSlackMessageAction>()
            .Add<CreateSlackChannelAction>();
    }
}
```

### Built-in Triggers & Actions

**Core (ships with Umbraco.Automate)**:

| Group | Triggers | Actions |
|-------|----------|---------|
| **Core** | Manual, Scheduled (CRON), Webhook Received | HTTP Request, Delay, Log Message, Set Variable |
| **Content** | Content Published, Content Unpublished, Content Saved, Content Deleted, Content Moved | Publish Content, Unpublish Content, Create Content, Update Content, Delete Content |
| **Media** | Media Uploaded, Media Deleted | Upload Media, Delete Media |
| **Members** | Member Created, Member Approved, Member Locked Out | Create Member, Update Member, Assign Group |

**DXP Integration (separate NuGet packages)**:

| Package | Triggers | Actions |
|---------|----------|---------|
| **Umbraco.Forms.Automate** | Form Submitted, Form Entry Approved | Submit Form, Export Entries |
| **Umbraco.Commerce.Automate** | Order Placed, Order Status Changed, Payment Captured, Stock Low | Update Order Status, Send Order Email, Adjust Stock |
| **Umbraco.Workflow.Automate** | Approval Requested, Approval Completed, Approval Rejected | Request Approval, Approve Content, Reject Content |
| **Umbraco.Engage.Automate** | Segment Entered, Segment Exited, Persona Assigned | Assign Persona, Add to Segment, Trigger Personalization |

**Third-party examples** (community packages):

| Group | Triggers | Actions |
|-------|----------|---------|
| **Slack** | — | Send Message, Create Channel |
| **Email (SMTP)** | — | Send Email |
| **AI** | — | Generate Text, Classify Content, Summarize |
| **Webhooks** | Webhook Received | Send Webhook |

### Webhook Trigger (Incoming)

Umbraco's built-in webhook system is **outgoing only** — it sends HTTP POSTs to external URLs when CMS events fire. There is no built-in endpoint for receiving inbound webhook calls. Umbraco.Automate provides this.

**How it works:**

Each automation with a "Webhook Received" trigger gets a unique incoming URL:

```
POST /umbraco/automate/api/webhook/{automationId}
```

The endpoint:
1. Validates the automation exists and is enabled
2. Optionally validates a shared secret (via `X-Automate-Secret` header or HMAC signature)
3. Captures the full request (headers, body, query string) as the trigger output data
4. Starts an automation run with the payload available to downstream steps

**Settings on the trigger:**

```csharp
public class WebhookReceivedTriggerSettings
{
    [Field(IsSensitive = true)]
    public string? Secret { get; set; }  // Shared secret used by the selected strategy

    [Field(EditorUiAlias = "Umb.Automate.WebhookAuthenticatorPicker")]
    public string AuthenticatorAlias { get; set; } = "plain-secret";  // Authentication strategy

    [Field]
    public string? AllowedMethods { get; set; } = "POST";  // Comma-separated HTTP methods
}
```

**Trigger output** (available to downstream steps):

```json
{
    "method": "POST",
    "headers": { "content-type": "application/json", ... },
    "query": { "key": "value" },
    "body": { ... },
    "remoteIp": "203.0.113.42",
    "receivedUtc": "2026-03-09T14:30:00Z"
}
```

**Note on Umbraco CMS event triggers:** Our Content Published, Media Saved, etc. triggers listen directly to Umbraco's `INotification` system (the same mechanism that powers Umbraco's outgoing webhooks). They don't go _through_ the webhook system — they subscribe to the same underlying notifications. This means they fire in-process with zero latency, no HTTP round-trip.

---

## Cross-Cutting Patterns (from Umbraco.AI)

Several patterns from the Umbraco.AI codebase should be adopted directly — they solve problems we'll face in automation execution.

### Action Execution Middleware Pipeline

Mirrors Umbraco.AI's `IAIChatMiddleware` pipeline. Cross-cutting concerns (logging, error handling, timing, validation) wrap action execution without polluting action implementations.

```csharp
public interface IActionMiddleware
{
    Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken ct);
}

public delegate Task<ActionResult> ActionMiddlewareDelegate(ActionContext context, CancellationToken ct);
```

Registered via an ordered collection builder:

```csharp
builder.ActionMiddleware()
    .Append<StepRunLoggingMiddleware>()       // Captures input/output/duration per step
    .Append<ErrorHandlingMiddleware>()        // Retry, suspend, terminate logic
    .Append<SensitiveDataMaskingMiddleware>() // Masks [Field(IsSensitive=true)] values in logs
    .Append<ValidationMiddleware>();          // Validates settings before execution
```

Third parties can insert middleware:

```csharp
builder.ActionMiddleware()
    .InsertBefore<ErrorHandlingMiddleware, MyCustomMetricsMiddleware>();
```

### Automation Run Scope (AsyncLocal Ambient Context)

Mirrors `AIAuditScope`. Tracks the current automation run across async boundaries — child operations automatically associate with the parent run without explicit parameter passing.

```csharp
public sealed class AutomationRunScope : IDisposable
{
    private static readonly AsyncLocal<AutomationRunScope?> _current = new();
    public static AutomationRunScope? Current => _current.Value;

    public Guid RunId { get; }
    public Guid AutomationId { get; }
    public Guid? ParentStepRunId { get; }  // For sub-automations

    public AutomationRunScope(Guid runId, Guid automationId, Guid? parentStepRunId = null)
    {
        RunId = runId;
        AutomationId = automationId;
        ParentStepRunId = parentStepRunId;
        _current.Value = this;
    }

    public void Dispose() => _current.Value = null;
}
```

This means any code running inside a step can access `AutomationRunScope.Current` to know which run it belongs to — useful for logging, auditing, and sub-automation nesting.

### Background Task Queue for Run Persistence

Mirrors `AIAuditLogService`'s fire-and-forget pattern. Step-run logging must not block the execution hot path.

```csharp
// In StepRunLoggingMiddleware
var stepRun = StepRun.Started(context);
await _backgroundTaskQueue.QueueAsync(new BackgroundWorkItem(
    Name: "RecordStepRunStart",
    CorrelationId: stepRun.Id.ToString(),
    RunAsync: async (sp, ct) =>
    {
        var repo = sp.GetRequiredService<IStepRunRepository>();
        await repo.SaveAsync(stepRun, ct);
    }));

var result = await next(context, ct);

stepRun.Complete(result);
await _backgroundTaskQueue.QueueAsync(/* ... persist completion */);
```

### Automation Lifecycle Notifications

Uses Umbraco's `INotification` / `INotificationAsyncHandler` pattern for lifecycle events — allows other packages to react to automation events.

```csharp
// Notifications
public sealed class AutomationSavingNotification : CancelableEntityNotification<Automation> { }
public sealed class AutomationSavedNotification : EntityNotification<Automation> { }
public sealed class AutomationRunStartingNotification : CancelableEntityNotification<AutomationRun> { }
public sealed class AutomationRunCompletedNotification : EntityNotification<AutomationRun> { }
public sealed class StepRunCompletedNotification : EntityNotification<StepRun> { }

// Handler example — DXP packages can react to automation events
public class RunCompletedSlackNotifier : INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    public async Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken ct)
    {
        if (notification.Entity.Status == RunStatus.Failed)
        {
            // Send alert to ops channel
        }
    }
}
```

`Saving` notifications are **cancelable** — handlers can prevent an automation from being saved (e.g. validation, permission checks).

### Error Categorization

Mirrors `AIAuditLogErrorCategory`. Classifying failures makes the Run Explorer filterable and actionable.

```csharp
public enum StepRunErrorCategory
{
    Unknown,
    Validation,         // Settings invalid, missing required fields
    Authentication,     // API key expired, unauthorized
    RateLimiting,       // External service rate limit hit
    Timeout,            // Step exceeded timeout
    ServiceUnavailable, // External service down
    InvalidResponse,    // Unexpected response from external service
    Cancelled,          // User or system cancelled the run
    ConfigurationError  // Misconfigured automation (e.g. missing connection)
}
```

### Options Configuration

Mirrors Umbraco.AI's hierarchical `IOptions` binding:

```csharp
// appsettings.json
{
    "Umbraco": {
        "Automate": {
            "Enabled": true,
            "Execution": {
                "DefaultTimeout": "00:05:00",
                "DefaultRetryCount": 3,
                "MaxConcurrentRuns": 10,
                "PollInterval": "00:00:05"
            },
            "Governance": {
                "AuditLogEnabled": true,
                "AuditLogRetentionDays": 90,
                "SensitiveDataMasking": true
            }
        }
    }
}
```

```csharp
services.Configure<AutomationOptions>(config.GetSection("Umbraco:Automate"));
services.Configure<ExecutionOptions>(config.GetSection("Umbraco:Automate:Execution"));
services.Configure<GovernanceOptions>(config.GetSection("Umbraco:Automate:Governance"));
```

### Infrastructure Providers (Queue, Lock, Lifecycle)

WorkflowCore has three pluggable infrastructure abstractions: `IQueueProvider`, `IDistributedLockProvider`, and `ILifeCycleEventHub`. Swapping these is a **code-level decision** (requires installing a NuGet package), so it's configured in a Composer via `AddUmbracoAutomate()`, not in appsettings.

**Default behavior** (zero configuration):
- Queue: in-memory
- Lock: in-memory (single-node)
- Migrations tracked in `__UmbracoAutomate_MigrationsHistory` (separate from Umbraco core and other products)

**Database configuration is explicit** — Automate does not silently share another product's database, because the additional traffic from outbox messages, run history and WorkflowCore engine tables can affect the host's performance. Automate resolves whatever connection string `Umbraco:Automate:UseNamedConnectionString` points at; this setting defaults to `umbracoAutomateDbDSN`.

**Default — dedicated database** (recommended; follows the Umbraco Commerce connection-string convention):
```json
{
    "ConnectionStrings": {
        "umbracoAutomateDbDSN": "Server=...;Database=myUmbracoAutomateDb;...",
        "umbracoAutomateDbDSN_ProviderName": "Microsoft.Data.SqlClient"
    }
}
```

**Share an existing connection string** — point `UseNamedConnectionString` at any registered DSN (e.g. `umbracoDbDSN` to share the Umbraco CMS database, or a custom name shared across products). Useful for hosts like Umbraco Cloud where the CMS connection string is not user-editable so cannot be copied into `umbracoAutomateDbDSN`:
```json
{
    "Umbraco": {
        "Automate": {
            "UseNamedConnectionString": "umbracoDbDSN"
        }
    }
}
```
With this set, Automate resolves the named connection string at startup. Tables coexist in the target database, distinguished by the `umbracoAutomate*` table prefix and a separate `__UmbracoAutomate_MigrationsHistory` table.

```csharp
// Default — just works, uses Umbraco's connection string
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomate();
    }
}
```

**Distributed deployment** (user installs a WorkflowCore provider package):

```csharp
// Azure: install WorkflowCore.Providers.Azure
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomate(options =>
        {
            options.UseAzureSynchronization("DefaultEndpointsProtocol=https;...");
        });
    }
}

// Redis: install WorkflowCore.Providers.Redis
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomate(options =>
        {
            options.UseRedisQueues("localhost:6379", "automate");
            options.UseRedisLocking("localhost:6379", "automate");
        });
    }
}

// RabbitMQ + Redis (mix and match)
public class MyComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomate(options =>
        {
            options.UseRabbitMQ(new ConnectionFactory { HostName = "rabbitmq.internal" });
            options.UseRedisLocking("redis.internal:6379", "automate");
        });
    }
}
```

The `options` parameter exposes WorkflowCore's `WorkflowOptions` directly — no custom abstraction layer. Available providers (each a separate NuGet):

| Provider | Queue | Lock | Lifecycle Events | Package |
|----------|-------|------|-----------------|---------|
| **In-memory** | ✅ (default) | ✅ (default) | ✅ (default) | Built-in |
| **Redis** | ✅ | ✅ | ✅ | `WorkflowCore.Providers.Redis` |
| **Azure** | ✅ (Storage Queues) | ✅ (Blob Leases) | ✅ (Service Bus) | `WorkflowCore.Providers.Azure` |
| **AWS** | ✅ (SQS) | ✅ (DynamoDB) | ✅ (Kinesis) | `WorkflowCore.Providers.AWS` |
| **RabbitMQ** | ✅ | — | — | `WorkflowCore.QueueProviders.RabbitMQ` |
| **SQL Server** | ✅ (Service Broker) | ✅ (App Locks) | — | `WorkflowCore.QueueProviders.SqlServer` / `WorkflowCore.LockProviders.SqlServer` |

### Trigger Dispatcher

The trigger dispatcher sits between "trigger fires" and "workflow starts". Trigger handlers are **simple mappers** — they convert a CMS event into a `TriggerEvent` (trigger alias + data) and hand it to the dispatcher. The dispatcher owns **finding matching automations and starting workflows**. This separation means:

- Handlers have no dependency on `IAutomationService` — just `ITriggerDispatcher`
- Fan-out, deduplication, rate limiting, and filter evaluation are centralized in the processor
- Swapping to a message bus requires zero changes to any trigger handler

```csharp
public interface ITriggerDispatcher
{
    Task DispatchAsync(TriggerEvent triggerEvent, CancellationToken ct);
}

// TriggerEvent and TriggerEvent<TOutput> are defined above in the Interfaces section.
// The dispatcher accepts the non-generic base and extracts the output via pattern matching.
```

#### Load Balancing & Resilience

In a load-balanced Umbraco environment, trigger handlers fire on whichever node handled the request. We need to ensure automations run exactly once regardless of which node fires the trigger.

We use a **custom database-backed outbox** for reliable message dispatch. Messages are written to the `umbracoAutomateOutbox` table and consumed by a `BackgroundService` that polls, routes to handlers, and manages retry/dead-lettering.

The outbox is an **internal implementation detail** — not exposed in any public API, interface, or contract. All public-facing code interacts only with `ITriggerDispatcher`.

```
Any node:            Handler → ITriggerDispatcher → IOutbox.PublishAsync()
                                                         ↓
                                                    umbracoAutomateOutbox table (Status=Pending)
                                                         ↓
SchedulingPublisher: OutboxDispatcher → TriggerEventHandler → finds automations → StartWorkflow
```

**Dispatcher** (publishes to outbox — runs on any node):

```csharp
internal class OutboxTriggerDispatcher(IOutbox outbox) : ITriggerDispatcher
{
    public async Task DispatchAsync(TriggerEvent triggerEvent, CancellationToken ct)
    {
        var message = new TriggerEventMessage
        {
            TriggerAlias = triggerEvent.TriggerAlias,
            InitiatorType = triggerEvent.InitiatorType,
            InitiatorId = triggerEvent.InitiatorId,
        };

        if (triggerEvent is ITriggerEventWithOutput withOutput)
        {
            message.OutputData = JsonSerializer.Serialize(withOutput.GetOutput());
            message.OutputTypeName = withOutput.GetOutput().GetType().AssemblyQualifiedName;
        }

        await outbox.PublishAsync("umbraco.automate.trigger", message, ct);
    }
}
```

**Handler** (processes trigger events — outbox dispatcher handles retry, dead-lettering):

```csharp
// OutboxDispatcher routes messages to this handler by topic.
// Server role filtering ensures only appropriate nodes process events.
internal class TriggerEventHandler(
    IAutomationService automationService,
    IAutomationExecutor executor,
    IServerRoleAccessor serverRoleAccessor) : IMessageHandler
{
    public string Topic => "umbraco.automate.trigger";

    public async Task HandleAsync(string body, CancellationToken ct)
    {
        var message = JsonSerializer.Deserialize<TriggerEventMessage>(body);
        var automations = await automationService.GetAllAutomationsAsync(ct);
        var matching = automations.Where(a => a.IsEnabled && a.Status == Published
            && a.Trigger?.TriggerAlias == message.TriggerAlias);

        foreach (var automation in matching)
            await executor.ExecuteAsync(automation, message.InitiatorType,
                message.InitiatorId, triggerOutputData, ct);
    }
}
```

**Key resilience features:**

| Concern | Solution |
|---------|----------|
| **Consumer crash** | Database outbox — messages survive restarts, stale claims recovered automatically |
| **Poison messages** | Configurable retry count with exponential backoff, then dead-lettered |
| **Multi-instance safety** | Optimistic concurrency on `ClaimedByInstance` column |
| **Node failover** | Server role filtering — Umbraco handles role failover |
| **Scheduled triggers** | Fired by the processor node only — naturally single-fire |

All trigger handlers call `ITriggerDispatcher.DispatchAsync` — they never look up automations or interact with `IWorkflowHost` directly. The outbox, dispatcher, and handlers are internal implementation details behind `ITriggerDispatcher`.

#### WorkflowCore Execution

WorkflowCore uses a background polling thread to pick up workflow instances and execute steps. In a load-balanced environment, if `IWorkflowHost` runs on every node, multiple nodes could execute the same step simultaneously.

**Approach**: Since we already use the outbox for trigger dispatch, we also use it for WorkflowCore's internal work queue via a custom `IQueueProvider`. This means every node can run the WorkflowCore host — the outbox's optimistic claiming guarantees each queued step is consumed by exactly one node.

```csharp
// Outbox-backed queue provider for WorkflowCore
internal class OutboxQueueProvider(IOutbox outbox) : IQueueProvider
{
    public bool IsDequeueBlocking => false;

    public async Task QueueWork(string id, QueueType queue)
    {
        var topic = queue == QueueType.Workflow
            ? "umbraco.automate.workflow.queue"
            : "umbraco.automate.workflow.events";
        await outbox.PublishAsync(topic, new QueueMessage { Id = id, Queue = (int)queue });
    }

    public Task<string?> DequeueWork(QueueType queue, CancellationToken ct)
    {
        // Local ConcurrentQueue fed by IMessageHandler implementations
        return localQueues[queue].TryDequeue(out var id)
            ? Task.FromResult<string?>(id) : Task.FromResult<string?>(null);
    }
}

// Message handlers receive outbox messages and feed them into local queues
internal class WorkflowQueueHandler(OutboxQueueProvider provider) : IMessageHandler
{
    public string Topic => "umbraco.automate.workflow.queue";
    public Task HandleAsync(string body, CancellationToken ct)
    {
        var msg = JsonSerializer.Deserialize<QueueMessage>(body);
        provider.EnqueueLocally(QueueType.Workflow, msg.Id);
        return Task.CompletedTask;
    }
}
```

This gives us:

- **Single node**: behaves like WorkflowCore's default in-memory queue — minimal overhead (DB poll only)
- **Multi-node**: optimistic claiming on the outbox table — each step executes on exactly one node, no distributed locking needed
- **Same outbox infrastructure** as trigger dispatch — single table, single background service
- **No `SchedulingPublisher` gate** on WorkflowCore — all nodes participate in execution

WorkflowCore's `IWorkflowHost` starts on every node:

```csharp
internal class WorkflowEngineComponent(IWorkflowHost host) : IAsyncComponent
{
    public async Task InitializeAsync(CancellationToken ct) => await host.StartAsync(ct);
    public async Task TerminateAsync(CancellationToken ct) => await host.StopAsync(ct);
}
```

**Note on Umbraco's `IDistributedLockingMechanism`**: We deliberately do not use it. It is scope-bound (requires an active `IUmbracoDatabase` transaction via `IScopeAccessor`), uses integer lock IDs against the `umbracoLock` table, and solves a different problem (protecting database writes within a transaction). WorkflowCore's background processing runs outside Umbraco scopes and needs async, string-key-based coordination — which the outbox's optimistic claiming handles naturally.

#### Scheduled Triggers (CRON)

Scheduled triggers are fired by a recurring hosted service. In a load-balanced environment, this must only run on the `SchedulingPublisher` node — otherwise every node fires the same CRON trigger, producing duplicate runs.

```csharp
internal class ScheduledTriggerService(
    IServerRoleAccessor serverRoleAccessor,
    ITriggerDispatcher dispatcher) : RecurringHostedServiceBase
{
    protected override bool CanRun() =>
        serverRoleAccessor.CurrentServerRole is ServerRole.SchedulingPublisher or ServerRole.Single;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Evaluate all published automations with scheduled triggers
        // whose CRON expression matches the current time, dispatch each.
    }
}
```

This follows the same pattern as Umbraco's own `ScheduledPublishing` and `HealthCheckNotifier` — background tasks gated by server role.

#### Summary: Server Role Responsibilities

| Concern | SchedulingPublisher | Subscriber nodes |
|---------|:------------------:|:----------------:|
| **Trigger handlers** (notification → `TriggerEvent`) | Yes | Yes |
| **Outbox write** (persist trigger events) | Yes | Yes |
| **Outbox dispatch** (process trigger events → fan-out) | Yes | Yes |
| **WorkflowCore host** (execute workflow steps) | Yes | Yes |
| **Scheduled triggers** (CRON evaluation) | Yes | No |
| **HITL approval resume** | Any node (via outbox message) | Any node (via outbox message) |
| **Backoffice UI** (view runs, manage automations) | Yes | Yes |

The outbox's optimistic claiming ensures each message is consumed by exactly one node — both trigger fan-out and workflow step execution scale out automatically in multi-node deployments. The only concern still gated to `SchedulingPublisher` is **scheduled trigger evaluation** (CRON), which must fire once.

**In-flight runs during rolling deployments**: Workflow state is persisted to the database by WorkflowCore's `IPersistenceProvider`. When a node restarts, its WorkflowCore host resumes and picks up work from the outbox. Steps that were mid-execution when the node went down will have their stale claims recovered — action implementations should be idempotent where possible.

---

## Core Domain Model

```
Automation
├── Id: Guid
├── Alias: string (unique, URL-safe)
├── Name: string
├── Description: string
├── IsEnabled: bool
├── Status: Draft | Published | Inactive
├── PublishedVersion: int? (the version currently active for trigger evaluation)
├── GroupId: Guid? (for organizing)
├── CreatedUtc: DateTime
├── UpdatedUtc: DateTime
├── CreatedBy: Guid (user key)
├── Trigger: TriggerConfiguration
│   ├── TriggerAlias: string
│   └── Settings: Dictionary<string, object>
├── Steps: List<StepConfiguration>
│   ├── Id: Guid
│   ├── ActionAlias: string
│   ├── Name: string (user-defined label)
│   ├── ConnectionId: Guid? (references a named Connection — resolved to alias during Deploy transfer)
│   ├── Settings: Dictionary<string, object>
│   ├── InputMappings: Dictionary<string, string>
│   ├── Position: { X, Y } (canvas coordinates)
│   ├── ErrorBehavior: Retry | Suspend | Terminate | Compensate
│   ├── RetryInterval: TimeSpan?
│   └── MaxRetries: int?
├── Connections: List<StepConnection>
│   ├── SourceStepId: Guid
│   ├── SourceHandle: string? (React Flow handle ID, e.g. "success", "error", "default")
│   ├── TargetStepId: Guid
│   ├── TargetHandle: string? (React Flow handle ID)
│   ├── Outcome: string? (for branching)
│   └── Filter: FilterExpression? (conditional)
└── CanvasState: string (JSON — viewport, layout metadata)

AutomationRun
├── Id: Guid
├── AutomationId: Guid
├── AutomationVersion: int (snapshot)
├── Status: Pending | Running | Completed | Failed | Suspended | Cancelled
├── StartedUtc: DateTime
├── CompletedUtc: DateTime?
├── TriggerData: string (JSON — the event payload)
├── InitiatedBy: string (user | system | ai-agent | webhook)
├── CorrelationId: string? (for linking related runs)
├── Error: string?
└── StepRuns: List<StepRun>
    ├── Id: Guid
    ├── StepId: Guid
    ├── ActionAlias: string
    ├── Status: Pending | Running | Completed | Failed | Skipped | WaitingForInput
    ├── StartedUtc: DateTime
    ├── CompletedUtc: DateTime?
    ├── InputData: string (JSON)
    ├── OutputData: string (JSON)
    ├── Error: string?
    ├── ErrorCategory: StepRunErrorCategory?
    ├── RetryCount: int
    └── Duration: TimeSpan?

Connection
├── Id: Guid
├── Alias: string (unique, URL-safe, e.g. "slack-notifications", "production-smtp" — used for referencing)
├── Name: string (human-friendly display name, e.g. "Slack Notifications", "Production SMTP")
├── Type: string (e.g. "webhook", "smtp", "oauth2")
├── Version: int
├── Settings: Dictionary<string, object> (sensitive values encrypted via [Field(IsSensitive = true)])
├── CreatedUtc: DateTime
└── UpdatedUtc: DateTime

EntityVersion (generic — single table for all versioned entities, mirrors Umbraco.AI's `umbracoAIEntityVersion`)
├── Id: Guid
├── EntityType: string (e.g. "Automation", "Connection")
├── EntityId: Guid
├── Version: int
├── Snapshot: string (JSON — full serialized snapshot of the entity state)
├── CreatedUtc: DateTime
├── CreatedByUserId: Guid? (user key)
├── ChangeDescription: string? (optional change notes)

// Outbox message table (umbracoAutomateOutbox) — used for trigger dispatch and WorkflowCore queue.
// Managed by EF Core alongside all other Umbraco Automate tables.
```

### Entity Versioning System

Follows Umbraco.AI's **Strategy + Adapter** pattern for a unified versioning system:

**Marker interface** — entities declare themselves versionable:
```csharp
public interface IVersionableEntity : IAuditableEntity
{
    int Version { get; }
}
```

**Entity adapter** — each versionable entity provides a concrete adapter for entity-specific snapshot/restore/comparison logic:
```csharp
public interface IVersionableEntityAdapter
{
    string EntityTypeName { get; }  // discriminator: "Automation", "Connection"
    Type EntityType { get; }
    string CreateSnapshot(object entity);
    object RestoreFromSnapshot(string json);
    IReadOnlyList<ValueChange> CompareVersions(string fromJson, string toJson);
    Task RollbackAsync(Guid entityId, int version, CancellationToken ct);
    Task<object?> GetEntityAsync(Guid entityId, CancellationToken ct);
}

public abstract class VersionableEntityAdapterBase<TEntity> : IVersionableEntityAdapter
    where TEntity : IVersionableEntity
{
    // Strongly-typed abstract methods for implementations to override
    protected abstract string CreateSnapshot(TEntity entity);
    protected abstract TEntity RestoreFromSnapshot(string json);
    protected abstract IReadOnlyList<ValueChange> CompareVersions(string fromJson, string toJson);
}
```

**Adapter collection** — registered via `LazyCollectionBuilderBase`, discovered at startup:
```csharp
builder.VersionableEntityAdapters()
    .Add<AutomationVersionableEntityAdapter>()
    .Add<ConnectionVersionableEntityAdapter>();
```

**Central version service** — delegates to adapters for entity-specific logic:
```csharp
public interface IEntityVersionService
{
    Task SaveVersionAsync<T>(T entity, Guid? userId, string? changeDescription, CancellationToken ct)
        where T : IVersionableEntity;
    Task<T?> GetVersionSnapshotAsync<T>(Guid entityId, int version, CancellationToken ct);
    Task<IReadOnlyList<ValueChange>> CompareVersionsAsync(string entityType, Guid entityId, int fromVersion, int toVersion, CancellationToken ct);
    Task RollbackAsync(string entityType, Guid entityId, int version, CancellationToken ct);
    Task DeleteVersionsAsync(Guid entityId, string entityType, CancellationToken ct);
}
```

**Save flow** — services explicitly create a version snapshot before updating:
```csharp
public async Task<Automation> SaveAutomationAsync(Automation automation, CancellationToken ct)
{
    var userId = _backOfficeSecurityAccessor?.BackOfficeSecurity?.CurrentUser?.Key;
    var existing = await _repository.GetByIdAsync(automation.Id, ct);
    if (existing is not null)
    {
        // Snapshot CURRENT state before overwriting
        await _versionService.SaveVersionAsync(existing, userId, null, ct);
    }
    return await _repository.SaveAsync(automation, userId, ct);  // increments Version
}
```

**Rollback** creates a **new version** from the target snapshot (append-only history):
```csharp
public async Task<Automation> RollbackAutomationAsync(Guid id, int targetVersion, CancellationToken ct)
{
    var current = await _repository.GetByIdAsync(id, ct);
    await _versionService.SaveVersionAsync(current, userId, null, ct);  // save current before rollback
    var snapshot = await _versionService.GetVersionSnapshotAsync<Automation>(id, targetVersion, ct);
    // Restore properties from snapshot, save as new version
    return await _repository.SaveAsync(restored, userId, ct);
}
```

**Unified API** — single controller for all entity types:
```
GET  /api/automate/versions/{entityType}/{entityId}?skip=0&take=10
GET  /api/automate/versions/{entityType}/{entityId}/{version}
GET  /api/automate/versions/{entityType}/{entityId}/{fromVersion}/compare/{toVersion}
POST /api/automate/versions/{entityType}/{entityId}/{version}/rollback
```

**Version cleanup** — background job with configurable retention:
- Age-based: delete versions older than N days
- Count-based: keep only the most recent N versions per entity

**Versionable entities**: `Automation` (with draft/publish lifecycle) and `Connection` (immediate on save, no draft/publish). Sensitive values in connection version snapshots are stored masked to prevent historical credential leakage.

---

## Versioning & Publishing Lifecycle

Automations follow a **draft → publish** lifecycle, similar to most automation platforms (n8n, Make, Power Automate) and consistent with Umbraco's own content publishing model. Editing an automation does not immediately affect running triggers — changes must be explicitly published.

### States

| Status | Triggers fire? | Description |
|--------|---------------|-------------|
| **Draft** | No | Newly created or never published. Editable but does not trigger. |
| **Published** | Yes (if enabled) | Has a published version. Triggers evaluate against the published definition. |
| **Inactive** | No | Previously published, explicitly deactivated. Published version is retained for reference/rollback. |

`IsEnabled` is separate from `Status` — a published automation can be disabled (kill switch) without losing its published state. Re-enabling resumes trigger evaluation immediately.

### How it works

1. **Every save** creates a new `EntityVersion` record (entity type `"automation"`) with a full JSON snapshot of the definition (trigger, steps, connections, canvas state). `Version` auto-increments.
2. **Publishing** sets `PublishedVersion = Version` and compiles the definition into a WorkflowCore workflow registration. Only published automations register triggers with the engine.
3. **Editing a published automation** continues saving new draft versions without affecting the live version. The canvas shows a "unpublished changes" indicator (like Umbraco content).
4. **In-flight runs** always execute against the version they started with (`AutomationRun.AutomationVersion`). Publishing a new version does not affect running instances.

### Rollback

Since every version is a complete snapshot, rollback is straightforward:

```
Publish v3 → users report issues → rollback to v2
```

The management API exposes:
- `GET /api/automate/automation/{id}/versions` — list all versions with metadata
- `POST /api/automate/automation/{id}/rollback/{version}` — creates a new draft from the specified version's snapshot, then publishes it

Rollback creates a **new version** (v4 with v2's definition) rather than rewinding history — the version log is append-only.

### Interaction with Deploy

Deploy transfers the **published version** of an automation. On arrival, the automation is created as `Draft` (disabled) with the transferred definition as version 1. The operator reviews and explicitly publishes after configuring connections.

---

## Data Flow & Binding Syntax

### Runtime Data Model

WorkflowCore requires a shared `TData` POCO for all workflow data. Since automations are user-defined with variable steps, we use a generic data bag:

```csharp
public class AutomationRunData
{
    // Trigger output — written when the trigger fires
    public Dictionary<string, object?> Trigger { get; set; } = new();

    // Step outputs — keyed by step ID, written after each step completes
    public Dictionary<Guid, Dictionary<string, object?>> Steps { get; set; } = new();

    // User-defined variables — writable by Set Variable action, readable by any step
    public Dictionary<string, object?> Variables { get; set; } = new();
}
```

When a step completes, the action execution middleware writes its output to `Steps[stepId]`. Downstream steps can reference these outputs in their settings via bindings.

### Binding Syntax

Inspired by **UFM** (Umbraco Flavoured Markdown) — uses the familiar `${ }` interpolation syntax with `|` filter pipes. Evaluated **server-side** in C#.

#### Referencing data

```
${ trigger.contentName }              — trigger output property
${ trigger.contentType.alias }        — nested property access
${ steps.sendEmail.messageId }        — output from a named step
${ variables.counter }                — user-defined variable
```

Steps are referenced by their **user-defined name** (slugified to camelCase), not by GUID. This keeps bindings readable and stable across environments.

#### Filters (chainable)

```
${ trigger.body | truncate:100 }
${ trigger.publishedDate | formatDate:yyyy-MM-dd }
${ trigger.title | lowercase }
${ steps.generateText.result | stripHtml | truncate:200:... }
${ trigger.email | fallback:no-reply@example.com }
```

**Built-in filters:**

| Filter | Description | Example |
|--------|-------------|---------|
| `truncate:n:suffix` | Truncate to N chars | `${ text \| truncate:100:... }` |
| `lowercase` | To lower case | `${ name \| lowercase }` |
| `uppercase` | To upper case | `${ name \| uppercase }` |
| `formatDate:format` | Date formatting | `${ date \| formatDate:dd/MM/yyyy }` |
| `fallback:value` | Default if null/empty | `${ email \| fallback:N/A }` |
| `stripHtml` | Remove HTML tags | `${ body \| stripHtml }` |
| `json` | Serialize to JSON | `${ data \| json }` |

#### Where bindings are used

Bindings can appear in any **string-type** settings field on an action. The `EditableModelSchemaBuilder` marks string fields as binding-enabled by default. The middleware resolves bindings before passing settings to `ExecuteAsync`.

```csharp
// Settings POCO — bindings resolve before the action sees the values
public class SendSlackMessageSettings
{
    [Field]
    [Required]
    public string? Message { get; set; }  // "Published: ${ trigger.contentName }"
}
```

At runtime, the binding `"Published: ${ trigger.contentName }"` resolves to `"Published: Summer Sale Page"` before the action executes.

#### Extensible filters

Third parties register custom filters via collection builder:

```csharp
builder.BindingFilters()
    .Add<TruncateFilter>()
    .Add<FormatDateFilter>();

// Custom filter
[BindingFilter("currencyFormat")]
public class CurrencyFormatFilter : IBindingFilter
{
    public object? Apply(object? value, string[] args)
    {
        if (value is decimal d && args.Length > 0)
            return d.ToString($"C", new CultureInfo(args[0]));
        return value;
    }
}
// Usage: ${ steps.getOrder.total | currencyFormat:en-GB }
```

#### UFM Code Reuse Assessment

We investigated whether the CMS's **UFM (Umbraco Flavoured Markdown)** implementation could be reused. Key findings:

**UFM is entirely frontend.** There is zero server-side C# code — all parsing, expression evaluation, and filter execution lives in TypeScript (`src/Umbraco.Web.UI.Client/src/packages/ufm/`). The UFM expression engine uses `@heximal/expressions` (a browser-only JS expression evaluator) and rendering produces Lit web components. No C# parser, tokenizer, or filter system exists in the CMS codebase.

**What we adopt from UFM (design, not code):**

| UFM Concept | Automate Equivalent | Reuse Type |
|-------------|---------------------|------------|
| `${ expression }` syntax | Same tokenizer pattern (`/\$\{((?:[^{}]\|\{[^{}]*\})*)\}/`) | Pattern only — reimplemented in C# as binding engine |
| `\| filter:arg1:arg2` pipe syntax | Same pipe syntax | Pattern only |
| `UmbUfmFilterApi` interface (`filter(...args)`) | `IBindingFilter.Apply(object?, string[])` | Mirrored interface shape |
| Extension manifest registration (`ufmFilter` type) | `BindingFilterCollectionBuilder` | Umbraco DI equivalent |
| Built-in filters (truncate, fallback, lowercase, uppercase, stripHtml) | Same filter set, ported to C# | Behaviour ported |

**What we cannot reuse:**
- `@heximal/expressions` — JavaScript-only, browser-only evaluator
- `Marked.js` plugins — UFM extends a Markdown parser; we are not rendering Markdown
- Lit web components (`<umb-ufm-render>`, `<umb-ufm-js-expression>`) — browser rendering concerns

**Decision:** Build a lightweight C# binding engine that mirrors UFM's syntax and filter semantics but is purpose-built for server-side automation data binding. The tokenizer regex from UFM's `marked-ufmjs.plugin.ts` translates directly to .NET `Regex`. The filter interface is a 1:1 port. Users familiar with UFM expressions in the backoffice will find the automation binding syntax immediately recognizable.

#### Implementation

Server-side binding parser using a simple tokenizer (not a full JS engine). Resolves property paths against `AutomationRunData`, applies filters in sequence. **No arbitrary code execution** — bindings are pure data lookups with transformations, sandboxed by design.

The tokenizer is a direct port of UFM's regex pattern:

```csharp
// C# port of UFM's marked-ufmjs.plugin.ts tokenizer
private static readonly Regex BindingPattern = new(
    @"\$\{((?:[^{}]|\{[^{}]*\})*)\}",
    RegexOptions.Compiled);

// Filter pipe parsing
private static (string path, FilterCall[] filters) ParseBinding(string expr)
{
    // "trigger.name | truncate:100:... | uppercase"
    var segments = expr.Split('|').Select(s => s.Trim()).ToArray();
    var path = segments[0];
    var filters = segments.Skip(1)
        .Select(s => {
            var parts = s.Split(':');
            return new FilterCall(parts[0], parts.Skip(1).ToArray());
        }).ToArray();
    return (path, filters);
}
```

---

## Governance & Observability

Governance is a first-class concern, not an afterthought.

### Audit Trail

Every automation run produces a complete `AutomationRun` with per-step `StepRun` records capturing:
- Exact input/output data at each step
- Duration, retry count, error details
- Who/what initiated the run (user, system event, AI agent, webhook)
- Which automation version was executing (immutable snapshot)

### Run Explorer UI

A dedicated backoffice section showing:
- **Run list** — filterable by automation, status, date range, initiator
- **Run detail** — visual replay of the automation graph with step-by-step status overlay (green/red/yellow per node), expandable input/output data
- **Error drill-down** — stack traces, retry history, the exact step that failed
- **Duration metrics** — per-step and total run timing

### Access Control

Leverage Umbraco's existing user group / permission model:
- **View automations** — see definitions and runs (read-only)
- **Edit automations** — create, modify, enable/disable
- **Execute automations** — manually trigger
- **Administer automations** — delete, view all users' automations

### Safety Features

| Feature | Description |
|---------|-------------|
| **Rate limiting** | Configurable max runs per automation per time window |
| **Kill switch** | Global and per-automation emergency disable |
| **Automation versioning** | Each save creates an immutable version snapshot. Draft/publish lifecycle ensures edits don't affect live triggers. Running instances complete on their original version. Rollback restores any previous version. |
| **Sensitive data masking** | Mark settings fields as sensitive; values are encrypted at rest and masked in run logs |

### Failure Notifications

When an automation run fails, the system needs to notify relevant people — they won't always be staring at the backoffice.

**Pluggable notification channels** via `INotificationChannel`:

```csharp
public interface INotificationChannel
{
    string Alias { get; }
    string Name { get; }
    Type? SettingsType { get; }  // Channel-specific config (e.g. webhook URL, email address)
    Task NotifyAsync(AutomationRunNotification notification, CancellationToken ct);
}
```

**Built-in channels:**

| Channel | Description |
|---------|-------------|
| **Backoffice** | Umbraco's built-in notification center — shows a toast/badge for logged-in users. Always enabled. |
| **Email** | Sends failure details to configured recipients via Umbraco's email infrastructure |
| **Webhook** | POSTs a JSON payload to a configured URL (Slack incoming webhook, Teams connector, PagerDuty, etc.) |

**Configuration per automation:**

Each automation can configure its own notification preferences — which channels, who to notify, and when:

```
Automation
├── ...
└── NotificationSettings
    ├── NotifyOn: Failed | Suspended | FailedAndSuspended (default: Failed)
    └── Channels: List<ChannelConfiguration>
        ├── ChannelAlias: string (e.g. "email", "webhook")
        ├── Settings: Dictionary<string, object> (e.g. recipients, URL)
        └── IsEnabled: bool
```

**Global defaults** via `Umbraco:Automate:Governance` configuration:

```json
{
    "Umbraco": {
        "Automate": {
            "Governance": {
                "DefaultNotificationChannels": [
                    { "Channel": "backoffice" }
                ]
            }
        }
    }
}
```

**Integration with lifecycle notifications:** Failure notifications fire from a handler on `AutomationRunCompletedNotification` — this means third parties can also add their own notification channels via the same `INotificationChannel` collection builder:

```csharp
builder.NotificationChannels()
    .Add<BackofficeNotificationChannel>()
    .Add<EmailNotificationChannel>()
    .Add<WebhookNotificationChannel>();

// Third party adds a custom channel
builder.NotificationChannels()
    .Add<PagerDutyNotificationChannel>();
```

**Notification payload** includes: automation name, run ID, failed step name, error category, error message, link to the run in the backoffice, and timestamp. Enough context to triage without logging into the CMS.

---

## Human-in-the-Loop (HITL)

HITL is critical for AI agent integration and content governance workflows.

### Built-in HITL Action: "Request Approval"

A core action that:
1. **Suspends** the automation run using WorkflowCore's `WaitFor` mechanism
2. **Notifies** approvers via configurable channel (backoffice notification, email, webhook)
3. **Presents** a decision UI in the backoffice (approve / reject / request changes + comment)
4. **Resumes** the automation with the decision as output data, flowing to subsequent steps

### HITL for AI Agents

When an AI agent triggers or participates in an automation:
- Steps can be marked as **"requires human review"** — the agent proposes an action, a human approves
- The approval UI shows what the AI agent intends to do (e.g., "Publish 'Summer Sale' page to /promotions/summer-sale")
- Approval decisions are logged in the run audit trail with the approver's identity
- Configurable: which steps require approval can be set per-automation or globally via policy

### Integration with Umbraco Workflow Package

The `Umbraco.Workflow.Automate` provider bridges to Umbraco's existing content approval workflow:
- Trigger: "Approval Requested" fires when content enters the approval pipeline
- Action: "Request Approval" can route to Umbraco Workflow's approval groups
- This means automations can participate in existing approval processes rather than creating a parallel system

---

## AI Integration (via Umbraco.AI)

All AI capabilities come from **Umbraco.AI** — Umbraco.Automate does not integrate with LLM providers directly. The integration is bidirectional and ships as a separate package: `Umbraco.AI.Automate`.

### AI → Automate: Agents as Actions

Umbraco.AI agents are exposed as automation actions. This lets automations delegate complex reasoning tasks to AI.

```csharp
[Action("executeAIAgent", "Execute AI Agent")]
public class ExecuteAIAgentAction : ActionBase<ExecuteAIAgentSettings>
{
    // Resolves the agent from Umbraco.AI's agent registry and executes it
}

public class ExecuteAIAgentSettings
{
    [Field(EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.AIAgentPicker")]
    [Required]
    public string? AgentAlias { get; set; }

    [Field(EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? Prompt { get; set; }  // Supports data binding from previous steps

    [Field]
    public bool RequireHumanApproval { get; set; }  // HITL gate before agent executes
}
```

Example automations:
- Content Published → Execute "Translate Agent" → Publish translated variants
- Form Submitted → Execute "Lead Scoring Agent" → Route to sales (high score) or nurture (low score)
- Scheduled (weekly) → Execute "Content Audit Agent" → Email report to editors

### AI → Automate: AI Events as Triggers

AI lifecycle events from Umbraco.AI can trigger automations:

| Trigger | Fires when |
|---------|-----------|
| **Agent Execution Completed** | An AI agent finishes executing (success) |
| **Agent Execution Failed** | An AI agent fails |
| **Prompt Completed** | A chat/completion call finishes |
| **Prompt Failed** | A chat/completion call fails |

These listen to Umbraco.AI's existing notification/audit events. Example automations:
- Agent Failed → Send Slack alert with error details
- Prompt Completed (high token usage) → Log warning, notify admin
- Agent Completed (content generation) → Start content review workflow

### Automate → AI: Automations as Agent Tools

Automations can be exposed as **tools** that Umbraco.AI agents can invoke via tool calling. An automation with a "Manual" trigger and well-defined inputs/outputs becomes a callable tool.

```csharp
// On the Automation entity
public class Automation
{
    // ...
    public bool ExposeAsAITool { get; set; }         // Opt-in
    public string? AIToolDescription { get; set; }    // Description the agent sees
}
```

When `ExposeAsAITool` is true, the system registers the automation as an `IAITool` in Umbraco.AI's tool collection:
- **Tool name**: automation alias
- **Tool description**: `AIToolDescription` (or automation description as fallback)
- **Input schema**: derived from the automation's trigger settings type
- **Output**: the final step's output data
- **`IsDestructive`**: inferred from whether the automation contains actions that modify content (publish, delete, etc.)

Example: An agent sees a tool called `publish-campaign-content` — when invoked, it triggers a multi-step automation that publishes a batch of content items, sends notifications, and updates a CMS dashboard. The agent doesn't need to know the individual steps.

This closes the loop: **agents can orchestrate automations, and automations can orchestrate agents**.

### AI Observability

All AI-related runs are fully auditable:
- Runs initiated by an AI agent record `InitiatedBy: "ai-agent:{agentAlias}"`
- Agent execution steps record the full prompt, response, and token usage in step-run data
- HITL gates can be required for AI-initiated automations via `GovernanceOptions`:

```json
{
    "Umbraco": {
        "Automate": {
            "Governance": {
                "RequireApprovalForAIInitiatedRuns": true
            }
        }
    }
}
```

When enabled, any automation triggered by an AI agent automatically inserts an approval step before execution begins.

---

## Backoffice UI

### Section: Automations

A new backoffice **section** with a tree, dashboards, and workspaces — modelled on how Data Types works in Umbraco CMS.

#### Tree

The section tree displays all automations organized in folders:

```
Automations
├── 📁 Content
│   ├── Notify Editors on Publish
│   └── Auto-translate on Save
├── 📁 Commerce
│   ├── Order Confirmation Email
│   └── Low Stock Alert
├── Welcome New Member
└── Weekly Content Report
```

- **Folders** for organizing automations (drag-and-drop reordering, nested folders)
- Each **automation** is a tree item — icon reflects enabled/disabled state, shows last run status badge
- **Context menu** on automations: Enable/Disable, Duplicate, Move to Folder, Delete
- **Context menu** on folders: Create Automation, Create Folder, Rename, Delete
- Clicking an automation opens the **canvas editor workspace**
- Clicking a folder shows its contents (standard Umbraco folder behavior)

#### Dashboard: Overview

The section root shows a dashboard with actionable status and recent activity.

**Status cards** (top row, at-a-glance):
- **Broken automations** — enabled automations referencing unregistered triggers/actions or missing connections. Links directly to each broken automation. Most useful after deployments or package changes.
- **Failing automations** — automations with high failure rates in last 24h, with failure count and last error. Click through to the run explorer for that automation.
- **Stuck runs** — runs in `Running` state beyond the expected timeout. Action button to retry or cancel.
- **Pending approvals** — runs waiting for HITL approval (Phase 2).

**Activity** (below):
- Recent runs (last 10, with status indicators)
- Quick stats: total automations, active, disabled, runs today, success rate

This surfaces the same concerns as health checks but in context where editors and admins actually work. The Umbraco health check system still covers infrastructure-level concerns (engine status, queue depth, data retention) that are more relevant to ops.

#### Workspace: Automation Editor

Opened by clicking an automation in the tree. Uses workspace apps (tabs) following the Umbraco.AI pattern — two apps:

**Workspace App: Workflow** (primary tab, `icon-nodes`)
The canvas editor for building the automation.

- **Top bar**: Automation name, alias, save, **publish** (with split button for "save draft"), enable/disable toggle. Shows "unpublished changes" badge when draft is ahead of published version.
- **Full-width canvas**: React Flow node graph — no sidebars, maximum canvas space
  - Trigger node (single, at the top)
  - Action nodes with typed input/output handles
  - Connection lines between nodes (with optional filter badges)
  - Each node shows a **pencil icon** — clicking opens the settings modal
  - **Add button** (on canvas or toolbar) opens a picker modal to insert a new trigger/action

**Node Picker Modal** (Umbraco modal):
- Triggered by the add button or when connecting to an empty handle
- Categorized by group (Content, Core, Slack, etc.) with search
- Shows icon, name, and description for each trigger/action
- Selecting an item places it on the canvas

**Node Settings Modal** (Umbraco modal):
- Triggered by clicking the pencil icon on a node
- Auto-generated form from the action's Settings POCO using `[Field]` attributes via `EditableModelSchemaBuilder`
- Includes node name (user-defined label), error behavior config, and per-step settings
- Standard Umbraco modal with save/cancel

**Workspace App: Info** (secondary tab, `icon-info`)
Follows the Umbraco.AI two-column layout (`<umb-automate-workspace-editor-layout>`):

- **Left column**: Version history table
  - Version number, author, timestamp, published/draft badge per row
  - Clicking a version opens a read-only canvas view of that snapshot
  - "Restore" button creates a new draft from the selected version (rollback)
  - Phase 3+: diff link between any two versions
- **Right column** (aside): Info box
  - **Id** — automation unique ID (or "Unsaved" tag for new)
  - **Alias** — URL-safe alias
  - **Status** — Draft / Published / Inactive badge
  - **Published version** — version number currently live
  - **Date Created** — formatted timestamp
  - **Date Modified** — formatted timestamp
  - **Created By** — user who created the automation

#### Workspace: Run Explorer

Accessible from the automation editor (tab or split view) and from a top-level "Runs" dashboard.

- Run list with filtering (automation, status, date range, initiator)
- Run detail with visual graph replay — the same canvas but read-only, with step-by-step status overlay
- Step-by-step data inspection (expand any step to see input/output JSON, error, duration)

#### Settings Panel
- Global automation settings (rate limits, default error behavior)
- Registered triggers/actions catalogue (discovered from DI)
- AI configuration (which models are available for AI actions)

---

## Umbraco Deploy Integration

### Deployment Strategy: Content Transfer

Automations are user-created entities — closer to content than to document types. They should use Umbraco Deploy's **content transfer** approach (queue and deploy over the wire), not the file-based artifact (UDA) approach.

Rationale:
- Automations are created and modified in the backoffice by editors/admins, not defined in code
- They reference dynamic data (trigger/action aliases, folder structure) that doesn't belong in source control
- They may differ between environments intentionally (e.g., a staging-only test automation)
- Content transfer gives explicit control over what moves and when

### What Transfers

| Data | Transfers? | Notes |
|------|-----------|-------|
| Automation definition (name, alias, steps, connections, canvas layout) | ✅ Yes | The core entity |
| Folder structure | ✅ Yes | Preserves tree organization |
| Step settings (non-sensitive) | ✅ Yes | Action configuration, filters, mappings |
| **Sensitive settings (credentials)** | ❌ **No** | See below |
| Enabled state | ⚠️ Transfers as **disabled** | Must be explicitly enabled in target environment |
| Run history | ❌ No | Runs are environment-specific |
| Webhook endpoint URL | ⚠️ Regenerated | The `{automationId}` stays the same (GUID), but the base URL is environment-specific |

### The Credential Problem

This is the hard part. An automation's steps may reference secrets (API keys, webhook URLs, OAuth tokens) that are **environment-specific by design**. Staging Slack webhook ≠ production Slack webhook. Dev API key ≠ prod API key.

#### Approach: Named Connections

Rather than embedding credentials directly in step settings, sensitive integrations reference a **named connection** — a separately managed entity that maps an alias to environment-specific credentials.

```
Step settings (in database):
  ConnectionId: "a1b2c3d4-..."              ← Guid reference, used at runtime
  Channel: "#content-updates"

Connection (per-environment, does NOT transfer):
  Id: "a1b2c3d4-..."
  Alias: "slack-notifications"
  Name: "Slack Notifications"
  Type: "webhook"
  Credentials: { WebhookUrl: "https://hooks.slack.com/..." }  ← encrypted via [Field(IsSensitive = true)]

Deploy artifact (serialized for transfer):
  ConnectionAlias: "slack-notifications"    ← swapped from ID to alias during export
  Channel: "#content-updates"
```

On transfer:
1. During **export**, the Deploy connector resolves `ConnectionId` → `ConnectionAlias` in the artifact (same pattern as content key → UDI)
2. During **import**, the connector resolves `ConnectionAlias` → `ConnectionId` by looking up the alias in the target environment
3. The **connection entity itself does NOT transfer** — it must be configured in the target environment
4. If the alias doesn't exist on arrival, the automation is created as Draft (disabled) and startup validation flags: _"Automation 'Notify on Publish' requires connection 'slack-notifications' which is not configured"_
5. The admin configures the connection with production credentials, then publishes the automation

This approach:
- **Never moves credentials between environments** — credentials are always configured locally
- **Preserves automation portability** — the definition references an alias, not a raw secret
- **Validates on arrival** — missing connections are surfaced immediately, not discovered at runtime when a run fails
- **Credentials encrypted in-place** — sensitive connection settings are encrypted via Data Protection, environment-specific by nature

#### Connection Entity

```
Connection
├── Id: Guid
├── Alias: string (unique, URL-safe, e.g. "slack-notifications", "production-smtp" — used for referencing)
├── Name: string (human-friendly display name)
├── Type: string (e.g. "webhook", "smtp", "oauth2")
├── Settings: Dictionary<string, object> (sensitive values encrypted via [Field(IsSensitive = true)])
├── CreatedUtc: DateTime
└── UpdatedUtc: DateTime
```

Connections are managed in the Automations section settings panel. They are **never included in Deploy transfers** — each environment maintains its own set.

#### Inline Secrets (Fallback)

For simple cases where a named connection is overkill (e.g., a one-off HTTP Request with a bearer token), `[Field(IsSensitive = true)]` settings are **stripped on export/transfer** and replaced with a placeholder:

```json
{
    "settings": {
        "url": "https://api.example.com/notify",
        "bearerToken": "<<SENSITIVE_VALUE_REMOVED>>"
    }
}
```

On import, the admin is prompted to fill in the missing sensitive values before the automation can be enabled.

### Deploy Connector Implementation

Ships as `Umbraco.Deploy.Automate` — a separate package that registers Umbraco Deploy artifacts and value connectors:

```csharp
// Registers automation as a deployable entity type
public class AutomationArtifact : IDeployArtifact
{
    // Serializes automation definition for transfer
    // Strips sensitive settings, forces disabled state
    // Preserves connection references by name
}
```

The connector handles:
- Serialization/deserialization of automation definitions
- Stripping of sensitive values
- Dependency tracking (e.g., automation references connection "slack-notifications")
- Conflict resolution on the target (match by alias — update existing or create new)
- Post-transfer validation (flag missing connections)

### Cross-Product Dependency Risk

**This is a significant risk.** Automations can reference entities from other Umbraco products — a Forms form GUID, a Commerce store ID, a content node key, a Workflow approval group. Umbraco Deploy tracks these as **dependencies** and expects to resolve and synchronize IDs on the target environment.

All Umbraco DXP products (Forms, Commerce, Workflow, Engage) support Deploy, so this is not a concern for first-party providers. However, **third-party packages** that contribute triggers/actions may reference entities from products that lack Deploy support. If an automation step references an entity by GUID and no Deploy connector exists for that entity type, the GUID is meaningless on the target environment and the automation is silently broken.

Automate amplifies this risk because a single automation can reference entities from many products simultaneously — including community packages that may not have considered deployment scenarios.

#### How dependencies break

```
Automation: "Form Submission → Approval → Publish"
├── Trigger: Form Submitted (references Form GUID: abc-123)
├── Step 1: Request Approval (references Workflow Group ID: def-456)
└── Step 2: Publish Content (references Content Key: ghi-789)
```

On transfer to production:
- Content Key `ghi-789` — ✅ CMS Deploy resolves this
- Workflow Group `def-456` — ✅ Umbraco Workflow has Deploy support
- Form GUID `abc-123` — ✅ Umbraco Forms has Deploy support

But if a third-party action references an entity from a community package without Deploy support, the dependency can't be resolved and the automation arrives broken.

#### Mitigation Strategy

**1. Dependency declaration on actions/triggers**

Actions and triggers declare what external entity types they reference. This is metadata — it doesn't require the entity's product to be installed.

```csharp
[Action("submitForm", "Submit Form")]
public class SubmitFormAction : ActionBase<SubmitFormSettings>
{
    // Declares that this action references a Forms form entity
    public override IEnumerable<EntityDependency> GetDependencies(StepConfiguration step)
    {
        var settings = step.GetSettings<SubmitFormSettings>();
        if (settings?.FormId is not null)
            yield return new EntityDependency("umbraco-forms-form", settings.FormId.Value);
    }
}
```

**2. Pre-transfer validation**

Before initiating a transfer, the Deploy connector walks all steps, collects dependencies via `GetDependencies()`, and checks each against Deploy's registered dependency resolvers. If a dependency type has no resolver, the transfer is **blocked with a clear error**:

> _"Cannot transfer automation 'Form Submission → Approval → Publish': step 'Submit Form' references entity type 'umbraco-forms-form' (ID: abc-123) but no Deploy connector is registered for this entity type. Install Umbraco.Forms.Deploy or remove this step before transferring."_

This fails loudly before transfer rather than silently breaking on arrival.

**3. Dependency resolution levels**

| Level | Behavior | When to use |
|-------|----------|-------------|
| **Resolved** | Deploy tracks and synchronizes the entity ID across environments | Product has full Deploy support for this entity type |
| **Named reference** | Step references entity by name/alias instead of GUID; resolved by name on target | Product doesn't have Deploy support but has stable aliases |
| **Unresolvable** | Transfer blocked with actionable error message | No Deploy support and no stable alias available |

**4. Encourage named references where possible**

Action settings should prefer **aliases/names** over GUIDs where the referenced product supports stable identifiers. For example, reference a Workflow approval group by name rather than ID. Named references survive environment transfers without Deploy support — they just need the same-named entity to exist on the target.

```csharp
public class RequestApprovalSettings
{
    [Field]
    public string? ApprovalGroupName { get; set; }  // ← preferred: survives transfer

    // NOT: public Guid? ApprovalGroupId { get; set; }  // ← breaks without Deploy
}
```

**5. Documentation requirement for third-party providers**

Third-party provider packages must document which external entity types their actions reference and whether Deploy support exists for each. The `dotnet new umbraco-automate-actions` template should include guidance on implementing `GetDependencies()` and preferring named references over GUIDs.

---

## Security & Reliability

These concerns are baked into the architecture, not bolted on later.

### Phase 1 Critical

| Concern | Approach |
|---------|----------|
| **Secrets management** | Settings marked `[Field(IsSensitive = true)]` are automatically encrypted at rest using ASP.NET Core Data Protection (already in Umbraco's dependency tree) — mirroring Umbraco.AI's `[AIField(IsSensitive = true)]` pattern. Sensitive values are encrypted in-place on the entity (Connection settings, action settings) via EF Core value converters. No separate secrets table — the Connection entity itself centralises shared credentials. |
| **Step-type permission gating** | Triggers and actions declare `RequiredSections` (and content actions `RequiredPermissions`, the CMS permission letters) on the `[Trigger(...)]` / `[Action(...)]` attribute. `ISectionAccessChecker` enforces section access at four points: the catalogue picker endpoint filters out items the workspace's service account cannot use; `AutomationService.ValidateForPublishAsync` rejects publishes that would violate them; `TriggerEventHandler` skips dispatch when the trigger requirement is no longer satisfied; `BackOfficeIdentityMiddleware` fails the step at runtime when the action requirement drifts. `IAutomationActionAuthorizer` wraps Umbraco's `IContentPermissionService` / `IMediaPermissionService` to enforce node-level access (start node + granular Browse/Update/Publish) inside each content/media action. `AutomationPermissionDriftHealthCheck` surfaces published automations whose service accounts no longer satisfy the requirements. See [identity-ownership-permissions.md](identity-ownership-permissions.md#step-type-permission-model) for the full model. |
| **SSRF prevention** | HTTP Request action validates URLs against a configurable allowlist/denylist. Blocks internal IPs (`10.x`, `172.16.x`, `192.168.x`), link-local (`169.254.x`), and localhost by default. |
| **Step-level timeout enforcement** | `CancellationToken` linked to a `CancellationTokenSource` with the configured timeout. Enforced by the middleware pipeline — actions that ignore cancellation are terminated after a grace period. |
| **Trigger deduplication** | Optional `IdempotencyKey` on trigger events. `TriggerService` deduplicates within a configurable window (default 5 minutes). Incoming webhooks use `X-Request-Id` header or body hash. |
| **Optimistic concurrency** | EF Core concurrency token (`[Timestamp]`) on `Automation` entity. Management API returns HTTP 409 Conflict when saving a stale version. |
| **Stuck workflow recovery** | Startup sweep identifies workflow instances in `Running` state that haven't progressed within 2x `DefaultTimeout`. Options: re-queue, mark as failed. Configurable behavior. |
| **Graceful shutdown** | `IHostedService.StopAsync()` calls `WorkflowHost.StopAsync()` with a configurable drain timeout. In-flight steps complete or are cleanly suspended. |
| **Health checks** | Suite of `HealthCheck` classes registered with Umbraco's health check system (see details below). |
| **Data retention purge** | Recurring hosted service deletes `AutomationRun` + `StepRun` records older than `AuditLogRetentionDays`. Configurable per-automation for regulatory needs. |
| **Queue depth limits** | `MaxQueueDepth` option. When exceeded, new runs are rejected with a "system busy" status. Warning logged at 80% capacity. |
| **Basic observability** | Metrics emitted via OpenTelemetry: `automate.runs.total`, `automate.runs.failed`, `automate.step.duration` histogram. Compatible with Prometheus, Application Insights, etc. |
| **Input validation** | Middleware validates action settings against POCO attributes before execution. Webhook payloads are size-limited (configurable, default 1MB). |

### Health Checks (Umbraco Health Check System)

Registered as standard Umbraco health checks — for ops/infrastructure monitoring. Automation-specific concerns (broken automations, failures, stuck runs, missing connections) are surfaced on the **section dashboard** instead, where editors and admins actually work.

#### Phase 1

| Health Check | What it verifies | Severity |
|-------------|-----------------|----------|
| **Engine Status** | WorkflowCore host is running and processing. Queue provider is reachable. | Error |
| **Queue Depth** | Work queue depth is below `MaxQueueDepth` threshold. Early warning at 80%. | Warning |
| **Data Retention** | Purge job has run successfully within the expected interval. | Info |
| **Automation Service-Account Permissions** | Every published automation's workspace service account still satisfies the trigger/action `RequiredSections`. | Warning |

#### Phase 2

| Health Check | What it verifies | Severity |
|-------------|-----------------|----------|
| **Connection Health** | All enabled connections have valid credentials. OAuth2 connections have non-expired tokens. | Warning |

### Phase 2

| Concern | Approach |
|---------|----------|
| **OAuth2 connection manager** | `Connection` entity encapsulating OAuth2 authorization code flow + automatic token refresh. Actions reference a connection rather than raw credentials. |
| **Dead letter queue** | Unprocessable workflow events parked in a dead letter table with raw payload for manual inspection and replay. |
| **PII handling / right to erasure** | `CorrelationId` links runs to member/user keys. API endpoint for "purge all run data associated with member X." |
| **Run replay** | "Retry" button in Run Explorer creates a new run with the same trigger data from a failed run. |
| **Startup validation** | On boot, validate all enabled automations against registered action/trigger collections. Log warnings for unresolvable aliases. Optionally auto-disable broken automations. |
| **Pluggable webhook authentication** | `IWebhookAuthenticator` so provider packages can register provider-specific validation (GitHub `X-Hub-Signature-256`, Stripe `Stripe-Signature`, etc.). |
| **Distributed tracing** | Propagate trace context through the middleware pipeline so external API calls appear as child spans in APM tools. |
| **Synchronous audit mode** | Configurable option for compliance-sensitive deployments that writes step-run records synchronously (before returning from the step). Trades latency for guaranteed audit completeness. |

### Phase 3+

| Concern | Approach |
|---------|----------|
| **Audit log immutability** | Append-only table with no DELETE/UPDATE at SQL level, or cryptographic chaining (each record hashes the previous). |
| **Automation version diff** | Diff view in the UI for "what changed between v3 and v4" — definitions already stored as full JSON snapshots in `EntityVersion`. |
| **Run history archival** | Move runs older than N days to an archive table or blob storage. Keeps the active table lean for query performance. |

---

## Technical Implementation Phases

### Phase 1: Foundation (MVP)

**Goal**: A working, secure automation engine with basic triggers/actions, canvas editor, and run logging.

**Engine**:
- Domain models (Automation, EntityVersion, Step, Connection, Run, StepRun)
- EF Core persistence (DbContext, migrations for SQL Server + SQLite)
- WorkflowCore integration layer:
  - `AutomationWorkflowCompiler` — converts an `Automation` definition into a WorkflowCore `IWorkflow` at runtime
  - `ActionStepBody` — generic StepBody that resolves and executes the appropriate `IAction`
  - `TriggerService` — listens for Umbraco events and starts workflow instances
- Trigger/action collection builders (`LazyCollectionBuilderBase`) with `TypeLoader` auto-discovery
- Action execution middleware pipeline (`IActionMiddleware`) with ordered collection builder
- `AutomationRunScope` (AsyncLocal ambient context for run/step tracking)
- Background task queue for non-blocking run/step-run persistence
- Draft/publish lifecycle with version snapshots and rollback
- Automation lifecycle notifications (Saving/Saved, Publishing/Published, RunStarting/RunCompleted)
- `AutomationOptions` / `ExecutionOptions` / `GovernanceOptions` configuration

**Triggers & actions**:
- Core triggers: Manual, Scheduled, Webhook Received
- Core actions: Log Message, HTTP Request (with SSRF protection), Delay
- Content triggers: Content Published
- Content actions: Publish Content

**Security & reliability** (see table above):
- `[Field(IsSensitive = true)]` encryption via Data Protection
- Step-level timeout enforcement via `CancellationToken`
- Trigger deduplication (idempotency window)
- Optimistic concurrency on automation definitions
- Stuck workflow recovery on startup
- Graceful shutdown with drain timeout
- Health check endpoint
- Data retention purge job
- Queue depth limits
- Basic OpenTelemetry metrics
- Input validation middleware

**API**:
- Management API: CRUD for automations, list runs, get run detail
- Incoming webhook endpoint with HMAC validation

**Frontend**:
- Section with tree (automations + folders)
- React Flow canvas editor wrapped in custom element (`<umb-automate-canvas>`)
- Node picker modal and node settings modal
- Dashboard with recent runs and error summary
- Basic run explorer (list + detail)

**Developer experience**:
- `ActionTestHarness` for unit testing actions in isolation
- `dotnet new umbraco-automate-actions` project template

### Phase 2: HITL, Branching & Hardening

- Request Approval action (WaitFor-based)
- Backoffice approval UI (notification + decision panel)
- Conditional connections (filters on edges)
- If/Switch step types in the canvas
- Email notification action
- Failure notification channels (email, webhook)
- **Named Connections** entity (alias → environment-specific credentials, encrypted via `[Field(IsSensitive = true)]`)
- OAuth2 support in connections (authorization code flow + automatic refresh)
- Connection management UI in settings panel
- **Umbraco Deploy connector** (`Umbraco.Deploy.Automate`) — content transfer with credential stripping, disabled-on-arrival, missing connection validation
- Dead letter queue
- Run replay ("Retry" button)
- Startup validation of automation definitions (including missing connections)
- Pluggable webhook authentication (`IWebhookAuthenticator`)
- Import/export (JSON) — same serialization as Deploy, usable standalone
- Automation templates (5-10 built-in)

### Phase 3: AI Integration

- `Umbraco.AI.Automate` package
- AI agents exposed as actions (Execute AI Agent)
- AI lifecycle events as triggers (Agent Completed/Failed, Prompt Completed/Failed)
- Automations exposed as `IAITool` for agent tool calling
- HITL gates for AI-initiated runs
- AI observability in run explorer

### Phase 4: DXP Providers

- Umbraco.Forms.Automate
- Umbraco.Commerce.Automate
- Umbraco.Workflow.Automate
- Umbraco.Engage.Automate

### Phase 5: Advanced Features

- Parallel execution paths
- Sub-automations (reusable fragments)
- Automation version diff
- Audit log immutability (cryptographic chaining)
- Run history archival
- Rate limiting and kill switch
- Distributed tracing
- Synchronous audit mode option

---

## Key Decisions Needed

| # | Decision | Options | Recommendation |
|---|----------|---------|----------------|
| 1 | **Visual editor library** | Rete.js (Lit-native) vs React Flow (wrapped) | ✅ **Decided: React Flow** — most polished/configurable, ~500KB tree-shaken, wrapped in custom element |
| 2 | **WorkflowCore persistence** | Use WorkflowCore's own EF tables vs our own tables | ✅ **Decided: Custom `IPersistenceProvider`** — implements WorkflowCore's interface but uses Umbraco's `IEFCoreScopeProvider` for engine state. Our own tables for runs/audit (richer schema). Bypasses WorkflowCore's EF package entirely, avoiding the EF Core version mismatch (WC targets EF 9.x, Umbraco 17 uses EF 10.x). |
| 3 | **Workflow definition storage** | Code-compiled vs JSON/YAML DSL | ✅ **Decided: JSON in DB, compiled to WorkflowCore at runtime.** Enables user-defined automations via the canvas editor. |
| 4 | **Settings UI generation** | Auto-generate from POCO attributes vs hand-crafted per action | ✅ **Decided: `[Field]` attribute + `EditableModelSchemaBuilder`** — mirrors Umbraco.AI's `[AIField]` pattern exactly |
| 5 | **Messaging infrastructure** | Direct dispatch vs message bus vs custom outbox | ✅ **Decided: Custom database-backed outbox.** Single `umbracoAutomateOutbox` EF Core table handles both trigger dispatch and WorkflowCore queue. Zero external dependencies. Optimistic concurrency for multi-instance safety. Exponential backoff retry + dead-lettering. Initially used DotNetCore.CAP but replaced — CAP's timing constraints, sealed APIs, and startup ordering issues added more friction than value. The outbox is an internal implementation detail behind `ITriggerDispatcher`. |
| 6 | **Multi-node / clustering** | Single-node only vs distributed from start | ✅ **Decided: Database-backed outbox with optimistic claiming.** Multi-node safe out of the box — all nodes share the same database. No external infrastructure needed. |
| 7 | **AI provider abstraction** | Direct SDK calls vs Umbraco AI abstraction | ✅ **Decided: Via Umbraco.AI.** All AI integration ships as `Umbraco.AI.Automate` and depends on Umbraco.AI's abstractions. Deferred to Phase 3. |
| 8 | **Newtonsoft.Json** | Accept dual dependency vs replace | ✅ **Decided: Accept it.** WorkflowCore has a hard dep on Newtonsoft.Json. Isolated to the engine layer — our domain model and API use System.Text.Json. |
| 9 | **Workflow data model** | Typed TData POCO vs generic dictionary bag | ✅ **Decided: `AutomationRunData` dictionary bag.** User-defined automations have variable steps, so a generic `Dictionary<string, object?>` data bag is the right fit. Steps read/write via typed accessors. |
| 10 | **Binding syntax** | Reuse UFM code vs port UFM design | ✅ **Decided: Port UFM design to C#.** UFM is entirely frontend (TypeScript/Lit/Marked.js) — no server-side code exists. We adopt the `${ }` syntax, `\| filter:args` pipes, and filter interface shape, but implement a purpose-built C# tokenizer and evaluator. Users familiar with UFM will find the syntax immediately recognizable. |

---

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| WorkflowCore maintenance stalls | High | Medium | Pin to stable 3.x; library is small enough to fork. Monitor Elsa Workflows as alternative. |
| React-in-Lit integration friction | Low | Medium | Well-established pattern (React-in-custom-element). Isolate React to canvas only. Event bridging is straightforward via CustomEvent. |
| Performance at scale (100s of automations, 1000s of runs/day) | High | Low (initially) | WorkflowCore supports distributed execution. Optimize poll intervals. Data retention purge + archival. Queue depth limits. |
| SSRF via HTTP Request action | High | Medium | URL allowlist/denylist blocking internal IPs and metadata endpoints. Enforced by default, configurable. |
| Credential exposure in run logs | High | Medium | `[Field(IsSensitive = true)]` encrypts values at rest. `SensitiveDataMaskingMiddleware` strips sensitive values from step-run data before persistence. |
| Duplicate runs from duplicate events | Medium | High | Trigger deduplication with configurable idempotency window. Webhook dedup via `X-Request-Id` or body hash. |
| Stuck workflows after crash | High | Medium | Startup recovery sweep. Health check monitors for stuck instances. Graceful shutdown drains in-flight steps. |
| Third-party action takes down host | High | Low | Step-level timeout enforcement. Queue depth limits as backpressure. Document resource guidelines for action authors. |
| Trigger/action API stability | High | Medium | Design `ITrigger`/`IAction` interfaces carefully. Version the contract. Startup validation catches broken references. |

---

## Success Metrics

1. **Editors** can create a "Content Published → Send Slack Message" automation without developer help
2. **Developers** can create a custom provider with a new trigger and action in under an hour
3. **Every run** has a complete audit trail — input data, output data, errors, duration, initiator
4. **AI agents** can trigger automations and have their actions reviewed by humans before execution
5. **DXP products** can contribute providers without depending on each other

---

## Appendix A: Workflow Engine Decision Record — WorkflowCore vs Elsa 3

### Context

Umbraco.Automate needs a .NET workflow execution engine. The two candidates are WorkflowCore (v3.9.0+) and Elsa Workflows (v3.6.0). Umbraco CMS is MIT-licensed and has strong architectural opinions about DI, persistence, API, and UI patterns.

### Decision

**WorkflowCore** — used as a pure execution engine, with Umbraco.Automate owning all other layers.

### Comparison

#### Licensing

| | WorkflowCore | Elsa 3 |
|---|---|---|
| **License** | MIT (entire project) | MIT (core), but emerging Elsa+ commercial tier |
| **Commercial embedding** | No restrictions | Core is fine, but feature boundary between MIT and Elsa+ may shift |
| **Dependency licensing** | Clean | MassTransit v9+ is commercial; FastEndpoints has its own trajectory |

Both are MIT-compatible today. WorkflowCore has a simpler, cleaner story with no commercial ambitions.

#### Sustainability

| | WorkflowCore | Elsa 3 |
|---|---|---|
| **GitHub stars** | 5,827 | 7,653 |
| **NuGet downloads** | 4.2M | 3.5M |
| **Primary maintainer** | Daniel Gerlag (78% of commits) | Sipke Schoorstra (75% of commits) |
| **Bus factor** | **1** | **1-2** (company backing) |
| **Company behind it** | No | Elsa Digital + nexxbiz partnership |
| **Last release** | v3.17.0 (Oct 2025) | v3.6.0 (Mar 2026) |
| **Release cadence** | 2-3/year | Very active, multiple RCs |
| **Open issues** | 209 | 799 |

Elsa has better sustainability fundamentals (company, funding, release cadence). WorkflowCore's bus factor of 1 is the primary risk.

#### Umbraco Integration Fit (the deciding factor)

| Concern | WorkflowCore | Elsa 3 |
|---|---|---|
| **DI registration** | `services.AddWorkflow()` — minimal, unobtrusive | `services.AddElsa(...)` — registers API endpoints, DbContexts, auth, background services |
| **API layer** | None — we use Umbraco Management API | FastEndpoints — `UseFastEndpoints()` called globally, conflicts with Umbraco's API pattern |
| **EF Core / persistence** | Own `DbContext` but can be replaced with custom `IPersistenceProvider` | Own DbContexts (management + runtime) that bypass Umbraco's `IEFCoreScopeProvider` |
| **Auth** | None — we use Umbraco's | Own identity system — conflicts with Umbraco backoffice auth |
| **UI** | None — we build our own (React Flow) | Elsa Studio (Blazor WASM) — dead weight, we don't use it |
| **Dependencies** | ~13 on core package | 43+ on EF package. FastEndpoints, Hangfire, Humanizer, LinqKit, etc. |
| **Use as pure engine** | **Yes** — designed as an embeddable engine | **No** — designed as a semi-autonomous subsystem |
| **What we'd bypass** | Persistence providers, JSON DSL | API, UI, auth, persistence, background jobs, clustering — the majority |

**WorkflowCore stays out of the way.** We own DI (`IComposer`), API (Management API), persistence (`IEFCoreScopeProvider`), and UI (React Flow). WorkflowCore is just the step execution engine underneath.

**Elsa fights us.** We'd spend significant effort suppressing or working around Elsa's opinions about API, auth, persistence, and background processing — all things Umbraco already handles. We'd be paying for 43+ dependencies while using a fraction of the features.

#### Feature Comparison

| Feature | WorkflowCore | Elsa 3 |
|---|---|---|
| Step execution with DI | ✅ | ✅ |
| Data flow between steps | ✅ (shared TData POCO) | ✅ (Inputs/Outputs/Variables — more powerful, more complex) |
| Wait states / external events | ✅ (WaitFor + Activities) | ✅ (Bookmarks — more sophisticated) |
| Branching / conditions / loops | ✅ (If, Branch, While, ForEach, Parallel) | ✅ (Switch, Fork/Join, ForEach, While) |
| Saga / compensation | ✅ Built-in | ❌ Not built-in |
| Error handling (retry, suspend, terminate) | ✅ Per-step | ✅ Incidents system |
| Middleware pipeline | ✅ Step + workflow level | ✅ Workflow level |
| JSON workflow definitions | ✅ (WorkflowCore.DSL) | ✅ (native JSON) |
| Workflow versioning | ✅ Basic | ✅ More mature |
| Expression engine (C#/JS/Python) | ❌ | ✅ |
| Built-in REST API | ❌ | ✅ (but conflicts with Umbraco) |
| Built-in visual designer | ❌ | ✅ (but we don't use it) |
| Multitenancy | ❌ | ✅ |
| Clustering | Via providers (Redis, etc.) | Built-in |

WorkflowCore covers all execution features we need. Elsa's extras (expression engine, designer, API, clustering) are either unnecessary or conflict with our architecture.

#### Forkability

| | WorkflowCore | Elsa 3 |
|---|---|---|
| **Core codebase size** | Small (~few thousand LOC) | Large (100+ projects) |
| **Practical to vendor-fork** | **Yes** | **No** |
| **Practical to maintain a fork** | Yes, with modest effort | Impractical |

### Risk Mitigation for WorkflowCore's Bus Factor

1. **Clean abstraction layer** — our `IAction`/`ITrigger` model does not leak WorkflowCore types. The `AutomationWorkflowCompiler` is the only code that touches WorkflowCore's API directly.
2. **Prepared to vendor-fork** — MIT license + small codebase makes this practical. If Daniel Gerlag stops maintaining, we fork and maintain the execution core.
3. **Monitor Elsa as a long-term alternative** — if we ever outgrow WorkflowCore's capabilities AND Elsa resolves its integration friction (unlikely without architectural changes), migration would be possible through the abstraction layer.

### Alternatives Considered

| Alternative | Why not |
|---|---|
| **Elsa 3** | Too opinionated for embedding; fights Umbraco's patterns (see above) |
| **MediatR + custom engine** | Reinventing the wheel; WorkflowCore's state persistence, WaitFor, and saga support are non-trivial |
| **Durable Task Framework** | Azure-centric, designed for Azure Functions/Service Bus, not embeddable in a CMS |
| **Temporal.io (.NET SDK)** | Requires external Temporal server; too heavy for an embedded engine |
| **No engine (raw background services)** | Loses persistence, recovery, WaitFor, sagas — the hard problems WorkflowCore solves |
