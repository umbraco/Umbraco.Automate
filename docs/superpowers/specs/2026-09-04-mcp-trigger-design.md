# MCP Trigger — Design Spec

Date: 2026-09-04
Status: Draft, pending review

## Summary

Add a new built-in trigger, `McpTrigger`, that lets a published automation act as
its own [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server.
An AI agent (Claude, ChatGPT, or any MCP-speaking client) can connect to the
automation's own URL, see it advertised as a single callable tool, call it with
typed arguments, and get the automation's real result back — not just an
acknowledgement that it started.

This sits alongside `ManualTrigger`, `ScheduledTrigger`, and `WebhookTrigger` as
a fourth built-in trigger. It reuses the on-demand execution path
(`IAutomationExecutor.ExecuteAsync`, `ISupportsManualRun`) that already backs
"Run now", and mirrors the webhook trigger's shape almost exactly: one trigger
per automation, one URL per automation, one secret per automation.

## Goals

- An automation author can pick "MCP Trigger" the same way they'd pick
  "Webhook Trigger" today, with no new package to install.
- The automation becomes one MCP tool, addressable by its own URL, with a
  name, description, and typed input fields the author defines.
- Calling the tool waits for the automation to finish and returns its real
  outcome, within a bounded timeout.
- No new top-level entity (no new Connection type, no workspace-level MCP
  concept). Everything needed lives on the trigger's own settings, like a
  webhook's secret does today.

## Non-goals

- Aggregating multiple automations behind one MCP address ("router" model,
  as seen in n8n's MCP Trigger node). Rejected for v1 in favor of matching the
  webhook trigger exactly — see "Alternatives considered."
- OAuth-based authentication for MCP clients. `Umbraco.Automate.OpenIddict`
  today is an OAuth *client* (for outbound connections like Slack), not a
  server; building OAuth server infrastructure is out of scope here.
- Automate acting as an MCP *client* (steps that call out to external MCP
  servers). A plausible future feature, but a separate design.
- Long-running progress streaming back to the agent while a run is in
  flight. The agent either gets the finished result or a "still running"
  message; nothing in between.

## Architecture

All new code lives in the core product, not a separate package, because a
built-in trigger needs its endpoint to be built-in too:

| Component | Location | Mirrors |
|---|---|---|
| `McpTrigger` | `Umbraco.Automate.Core/Triggers/BuiltIn/McpTrigger.cs` | `WebhookTrigger.cs` |
| `McpTriggerSettings` | same folder | `WebhookTriggerSettings.cs` |
| `McpTriggerOutput` | same folder | `WebhookTriggerOutput.cs` |
| `McpEndpointController` | `Umbraco.Automate.Web/Api/Mcp/Controllers/` | `WebhookEndpointController.cs` |

The MCP protocol plumbing (JSON-RPC framing, the Streamable HTTP transport)
uses Microsoft's official `ModelContextProtocol.AspNetCore` package rather
than a hand-rolled implementation.

## Data model

### `McpTriggerSettings`

```csharp
public sealed class McpTriggerSettings
{
    // Shown to the AI agent as the tool's name.
    public string ToolName { get; set; }

    // Shown to the AI agent; tells it when to use this tool.
    public string ToolDescription { get; set; }

    // Defines the tool's input schema. Each field becomes a property
    // in the JSON schema the agent sees, and a property on McpTriggerOutput.
    public List<McpToolInputField> InputFields { get; set; } = [];

    // Shared secret the caller sends as a bearer token. Generated on
    // creation, masked in API responses (IsSensitive = true), same
    // pattern as PlainSecretWebhookAuthenticatorSettings.Secret.
    public string? Secret { get; set; }

    // Seconds to wait for the run to finish before returning a
    // "still running" result instead of the real one. Default 30.
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class McpToolInputField
{
    public string Name { get; set; }
    public McpToolInputFieldType Type { get; set; } // Text, Number, Boolean
    public string? Description { get; set; }
    public bool Required { get; set; }
}
```

### `McpTriggerOutput`

A dictionary-shaped output, keyed by the declared field names, exactly the
way `WebhookTriggerOutput` hands steps the raw request shape:

```csharp
public sealed class McpTriggerOutput
{
    public Dictionary<string, object?> Arguments { get; init; } = [];
}
```

Steps bind to `Arguments["customerEmail"]` etc., the same way they bind to
`Body`/`Headers`/`Query` on a webhook today.

`McpTrigger` implements `ISupportsManualRun` so "Run now" in the backoffice
still works, standing in with a sample set of arguments the author can edit
— the same role `TestRequestBody` plays for `WebhookTriggerSettings`.

## Endpoint & protocol

- Route: `automate/mcp/{automationId}` (mirrors `automate/webhook/{automationId}`).
- Transport: Streamable HTTP only — no SSE, no stdio. n8n's docs flag that
  SSE needs sticky sessions to one server in multi-instance setups; avoiding
  it avoids that failure mode entirely.
- `tools/list` on this URL always returns exactly one tool: this automation,
  built from `ToolName`, `ToolDescription`, and the JSON schema derived from
  `InputFields`.
- `tools/call` validates the incoming arguments against `InputFields`
  (required fields present, types match), then executes.

## Authentication

- Bearer token in the `Authorization` header, checked against
  `McpTriggerSettings.Secret`.
- Optional: an author can leave `Secret` blank to run the endpoint without
  auth, same as a webhook's authenticator can be configured that way today.
  This is a deliberate author choice, not a default.

## Execution & wait semantics

1. `tools/call` arrives, arguments validate against `InputFields`.
2. The endpoint builds `McpTriggerOutput.Arguments` from the validated
   arguments and calls `IAutomationExecutor.ExecuteAsync` directly —
   targeting this one automation, the same pattern
   `TriggerAutomationController` and the webhook endpoint already use, not
   the fan-out dispatcher.
3. The endpoint then polls `IAutomationRunService` for the run's status on a
   short interval until it reaches a terminal status or `TimeoutSeconds`
   elapses.
   - **Polling, not the in-process `AutomationRunCompletedNotification`.**
     Automate can run on more than one app instance; the notification fires
     on whichever instance's `RunFinalizer` actually completes the run,
     which may not be the instance holding the MCP request open. Polling
     the persisted run status works regardless of which instance finishes
     it. n8n's own docs confirm this is the right call — their SSE-based
     live-signal approach is exactly what breaks under multiple replicas.
4. On terminal status: build the MCP tool result from the run's final
   status and step outputs.
   - `Completed` → success result with the automation's meaningful output.
   - `Failed` / `Rejected` → `isError: true`, with the run's error message.
5. On timeout: return a result telling the agent the run is still going,
   including the run ID, rather than blocking indefinitely or erroring.

## Error handling

| Condition | Behavior |
|---|---|
| Automation not published, or its trigger isn't `McpTrigger` | Request fails before anything runs (404, matching the webhook endpoint) |
| Missing/wrong secret | 401, nothing runs |
| Circuit breaker has auto-disabled the automation | Refused before starting, same as "Run now" today |
| Required input field missing, or wrong type | Tool-level error (`isError: true`) with a message identifying the field — the agent can retry with corrected input, no exception surfaced |
| A step fails during the run | Tool-level error with the run's own error message |
| Run doesn't finish within `TimeoutSeconds` | Not an error — a result saying the run is still in progress, with its run ID |
| High call volume | Same rate-limiting policy as the webhook endpoint |

## Testing

- **Unit:** `McpTriggerSettings`/`McpTriggerOutput` shape, `InputFields` →
  JSON schema mapping, argument validation logic. Follow
  `ManualTriggerTests` / `WebhookEndpointControllerTests` conventions.
- **Integration:** end-to-end run through a real automation (e.g. an
  `McpTrigger` automation with a `LogMessageAction` step), covering:
  - a normal call that returns the real result before timeout
  - a bad/missing secret
  - a missing required argument
  - a failing step surfacing as a tool-level error
  - a slow automation hitting the timeout and returning "still running"

  `ManualTriggerLogMessageTests` and `ManualTriggerRunScriptTests` already
  show the pattern for standing up a real automation and running it end to
  end; the MCP tests follow the same shape.

## Alternatives considered

**Router model (n8n-style):** one MCP entry point lists several automations
as tools, so an AI client adds Automate once and discovers everything.
Considered after reviewing n8n's MCP Trigger node, which works this way.
Rejected for v1: it introduces a new "entry point" concept with no existing
analog in Automate (webhooks don't aggregate either), more upfront design
and code, for a setup-convenience win that matters most at large tool
counts. The one-trigger-one-tool model can still be revisited later without
touching the parts of this design that don't change (settings shape,
execution/wait logic, error handling) — only "how many tools does one URL
return" would need rework.

**Workspace-scoped Connection holding a shared key:** considered early,
before settling on the trigger-only model. Rejected because it introduces a
new Connection type (the first built-in one) and a workspace-level surface
for something that, once the router model was also rejected, has no reason
to span more than one automation.

**In-process wait via `AutomationRunCompletedNotification`:** simpler than
polling, but only correct on a single-instance deployment. Rejected —
covered under "Execution & wait semantics" above.
