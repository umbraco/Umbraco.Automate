# Umbraco.Automate Vocabulary

## TL;DR

| Term | What it is | Who creates it |
|---|---|---|
| **Provider** | A package that contributes triggers and actions | Developer |
| **Action** | A reusable unit of work (e.g. "Send Slack Message") | Provider author |
| **Trigger** | An event that starts an automation (e.g. "Content Published") | Provider author |
| **Settings** | A POCO model on an action/trigger that drives the config UI | Provider author |
| **Automation** | A user-defined trigger + steps sequence (not "Workflow") | Backoffice user |
| **Step** | A configured instance of an action within an automation | Backoffice user |
| **Inputs** | Runtime data flowing into a step (from settings + mapped outputs) | System |
| **Outputs** | Runtime data produced by a step for downstream steps | System |
| **Filter** | Conditional logic controlling step execution / branching | Backoffice user |
| **Run** | A single execution of an automation | System |

---

This document defines the standard terminology for the Umbraco.Automate package. These terms should be used consistently across code, UI, documentation, and API surfaces.

## Overview

Umbraco.Automate is a provider-driven automation system built on [WorkflowCore](https://github.com/danielgerlag/workflow-core). Third-party packages can extend the system by registering providers that expose triggers and actions. Users compose these into automations via the backoffice UI.

## Terms

### Provider

A package or plugin that contributes triggers and actions to the system. A provider typically represents an integration with an external service or an Umbraco subsystem.

- **Level:** Package
- **Who creates it:** Third-party developer
- **Examples:** `Umbraco.Automate.Slack`, `Umbraco.Automate.Cms`
- **WorkflowCore mapping:** N/A (application-level concept)

### Action

A reusable unit of work that a provider exposes. An action declares a settings model that describes what it needs to be configured. Actions are the building blocks that users select when composing automations.

- **Level:** Definition
- **Who creates it:** Provider author
- **Examples:** "Send Slack Message", "Create Content", "Send Email"
- **WorkflowCore mapping:** `StepBody` / `StepBodyAsync` class

### Trigger

A special type of action that starts an automation in response to an event or condition. A trigger also declares a settings model and produces output data that downstream steps can consume.

- **Level:** Definition
- **Who creates it:** Provider author
- **Examples:** "Content Published", "Form Submitted", "Scheduled (Cron)"
- **WorkflowCore mapping:** Entry step / external event via `WaitFor`

### Settings

A POCO model declared by an action or trigger that describes its configurable properties. Settings models are decorated with field attributes to drive UI rendering (labels, descriptions, editor aliases, validation, sensitivity, grouping). The schema builder reflects over the settings type to produce a schema, and the resolver hydrates stored values back into a typed instance at runtime.

- **Level:** Definition
- **Who creates it:** Provider author
- **Attribute:** `[EditableModelField]` on properties
- **Examples:** `SendSlackMessageSettings { Channel, Message }`, `ContentPublishedTriggerSettings { ContentType }`
- **WorkflowCore mapping:** Step data / inputs

### Automation

A user-defined sequence consisting of a trigger and one or more steps. An automation is the top-level entity that users create, name, enable, and manage in the backoffice.

The term "Automation" is used deliberately to avoid conflict with the existing [Umbraco Workflow](https://umbraco.com/products/umbraco-workflow/) package, which uses "Workflow" for content approval processes.

- **Level:** Instance
- **Who creates it:** Backoffice user
- **Examples:** "Notify editors on publish", "Sync content to CDN"
- **WorkflowCore mapping:** `IWorkflow` / workflow definition

### Step

A configured instance of an action within an automation. A step has its settings values populated (either static values entered by the user, or mapped from a previous step's outputs). Steps are ordered within an automation and execute sequentially unless branching is configured.

- **Level:** Instance
- **Who creates it:** Backoffice user (by selecting an action and configuring it)
- **WorkflowCore mapping:** A step registered via `.StartWith<T>()` / `.Then<T>()` with `.Input()` bindings

### Inputs

The runtime data flowing into a step. Inputs are derived from the step's configured settings values and may include static values or dynamic mappings from the trigger output or a previous step's outputs.

- **Level:** Runtime
- **WorkflowCore mapping:** `.Input()` bindings on step registration

### Outputs

The runtime data produced by a step after execution. Outputs are made available to subsequent steps for mapping into their inputs.

- **Level:** Runtime
- **Examples:** A "Create Content" step outputs the created content node ID
- **WorkflowCore mapping:** `.Output()` bindings on step registration

### Filter

Conditional logic that controls whether a step executes or which path an automation takes. Filters evaluate bindings against the current data context.

- **Level:** Instance
- **WorkflowCore mapping:** `If` / `Branch` / `Decision` control structures

### Run

A single execution of an automation, initiated when its trigger fires. A run tracks the status of each step and the overall outcome.

- **Level:** Runtime
- **Examples:** "Run #42 - succeeded", "Run #43 - failed at step 3"
- **WorkflowCore mapping:** Workflow instance / execution pointer

## Conceptual Model

```
Provider (package)
  registers -->  Trigger (definition, with Settings model)
  registers -->  Action  (definition, with Settings model)

Automation (user-created)
  has one   -->  Trigger (configured instance, with Settings values)
  has many  -->  Step    (configured instance of an Action, with Settings values)
                   each step has --> Inputs  (from settings + mapped outputs)
                   each step has --> Outputs (produced at runtime)

Run (single execution)
  tracks    -->  Status per step
  tracks    -->  Overall outcome
```

## Comparison with Other Platforms

| Umbraco.Automate | Zapier | Node-RED | n8n | Power Automate | Make.com |
|---|---|---|---|---|---|
| Provider | App | Node (palette) | Node (integration) | Connector | App |
| Action | Action | Node | Node | Action | Module |
| Trigger | Trigger | Input Node | Trigger Node | Trigger | Trigger |
| Settings | Fields | Config | Parameters | Inputs | Parameters |
| Automation | Zap | Flow | Workflow | Flow | Scenario |
| Step | Step | Node (instance) | Node (instance) | Step | Module (instance) |
| Inputs / Outputs | Fields | msg.payload | Items (JSON) | Dynamic content | Bundles |
| Filter | Filter / Path | Switch Node | IF Node | Condition | Filter / Router |
| Run | Task | - | Execution | Run | Operation |

## Design Notes

- **Settings vs Inputs/Outputs:** Settings describe the static configuration shape of an action (schema-driven, UI-rendered). Inputs and outputs describe the runtime data flowing between steps during a run. A step's inputs are derived from its settings values, which may include dynamic mappings to previous step outputs.
- **Action vs Step:** An action is a definition (what it does). A step is an instance (a specific usage of an action within an automation, with configured settings values). This mirrors the class vs instance distinction.
- **Trigger vs Action:** Triggers are a specialisation of the action concept. They share the same settings model pattern but are distinguished by their role as the entry point of an automation.
- **Provider-driven architecture:** The system is extensible by design. Providers register actions and triggers via a collection builder pattern, consistent with Umbraco's existing composition model. The settings UI is generated automatically from the settings model using the EditableModels infrastructure (shared with Umbraco.AI).
