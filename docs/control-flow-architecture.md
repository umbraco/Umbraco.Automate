# Decision record: custom control-flow layer

**Status:** Accepted (reconstructed after the fact — the original decision was never recorded)
**Applies to:** `Umbraco.Automate.Core/Execution/ControlFlow/` and `WorkflowCompiler`
**Related:** [engineering-spec.md](engineering-spec.md) · [workflowcore-feature-gaps.md](workflowcore-feature-gaps.md)

> This record was written retroactively (mid-2026) to capture the rationale behind an
> existing design that had no written justification. The control-flow step bodies shipped
> on 2026-03-14 (`7085f94`, `4f5b87f`); the commit messages describe *what* was built but
> not *why custom*. This document is the missing "why". If any statement here conflicts
> with the code, the code wins and this record should be corrected.

## Context

An automation is a **data-driven node graph**: the user authors trigger + steps + connections
in the backoffice, and that graph is persisted (`Automation`, `StepConfiguration`,
`StepConnection`) and compiled to a WorkflowCore `WorkflowDefinition` **at runtime** by
`WorkflowCompiler`. Two properties of that graph drive this decision:

1. **The structure is not known at compile time.** It is loaded from the database per
   automation and per version. There is no C# class describing a specific workflow's shape.
2. **Edges can carry filters that reference iteration context.** A connection leaving a
   loop can be gated by a filter expression that reads `loop.item` / `loop.index`
   (`StepConnection.Filter`, evaluated via `ContainerBranchEdge`). Collection sources are
   binding expressions (`${ steps.x.output }`) that resolve to a JSON string at runtime, not
   in-memory `IEnumerable<T>` properties.

WorkflowCore offers two ways to define a workflow:

- **The fluent builder** (`IWorkflowBuilder<T>` with `.ForEach()`, `.While()`, `.Parallel()`,
  `.If()`, `.Switch()`, `.CancelCondition()`). This requires the workflow to be a statically
  authored `IWorkflow<TData>` with compile-time lambdas — it cannot express a graph loaded
  from the database, and its loop/branch primitives have no hook for per-edge `loop.*` filters.
- **The definition model** (`WorkflowDefinition` / `WorkflowStep` / `Outcomes` / `Children` /
  `ExecutionResult.Branch`), which is what a compiler targets when the shape is dynamic.

## Decision

`WorkflowCompiler` builds the `WorkflowDefinition` directly against the model API, and the
loop/branch/conditional nodes are implemented as **custom `IStepBody` container bodies**:
`ForEachContainerStepBody`, `WhileContainerStepBody`, `ParallelContainerStepBody`,
`IfStepBody`, `SwitchStepBody`.

**What still comes from WorkflowCore (this is not a re-engine):**

- The containers extend WorkflowCore's `ContainerStepBody` and reuse its re-entry mechanics:
  `ExecutionResult.Branch`, `IteratorPersistenceData`, `IsBranchComplete`, outcome routing,
  `ContextItem`/`Scope` propagation.
- The executor, persistence pointer graph, event/subscription handling, retry/error behaviour
  (`WorkflowErrorHandling`), scheduling and the host are all WorkflowCore.
- `ForEachContainerStepBody` explicitly mirrors `WorkflowCore.Primitives.Foreach`'s
  branch → wait-for-drain → advance pattern.

**What is custom, and why the native primitive could not be used as-is:**

| Concern | Native primitive | Why it doesn't fit |
| --- | --- | --- |
| Collection source | `Foreach.Collection` is an input-mapped `IEnumerable` set at compile time | Ours is a runtime binding expression resolving to a JSON string that must be parsed/materialised |
| Per-item / per-edge filters | none — `Foreach` branches unconditionally to `Children` | Edges leaving a container can be gated by filters reading `loop.*` |
| Binding integration | `ContextItem` holds the raw object only | Iteration must expose `loop.item`/`loop.index` to the binding-data model for downstream steps |
| Run tracking | none | Each container records `StepRun` rows with iteration index/total |

## Consequences

- **The bug and performance tail of control flow lives in this layer**, not in WorkflowCore.
  Each container re-implements iteration/persistence/output bookkeeping, so each can
  independently regress. The run-cancellation, forEach-O(n²), and large-output-offload work
  (PRs #132/#133/#135) are all fixes *inside* this layer.
- Divergence from native semantics is possible and has happened (e.g. the collection was once
  re-evaluated per iteration — fixed in #133 back toward the once-per-loop semantics of native
  `Foreach`).
- New engine features that ride on the fluent builder (e.g. `Saga`/`CompensateWith`) are **not
  automatically available** — surfacing them means teaching the compiler + adding a container
  body, not just calling a builder method.

## Reconsideration triggers

Revisit this decision (spike delegating more to native primitives, or a thinner adapter) if:

- the control-flow container bodies keep accruing a bug/perf tail (this record was prompted by
  the third such fix in a row);
- WorkflowCore gains a data-driven / model-level loop primitive that accepts a runtime
  collection and per-branch predicates;
- a required engine feature (Saga, Recur, Schedule) proves impractical to reach through the
  current compiler-plus-custom-body approach.

## The standing rule this record serves

Per the root `CLAUDE.md` **"WorkflowCore First"** principle: prefer the engine's built-ins and
documented extension points; build a custom implementation only when there is a concrete reason
the engine cannot meet the requirement — **and record that reason** (comment, commit, or design
doc). This document is that record for the control-flow layer; the custom `IPersistenceProvider`
records its own reason in [engineering-spec.md](engineering-spec.md) (EF Core version conflict).
