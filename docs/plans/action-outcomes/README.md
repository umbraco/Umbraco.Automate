# Action outcomes (multiple output edges)

Read these in order:

1. [x] [BRIEF.md](./BRIEF.md) — Let any action declare named outcomes so the canvas can branch straight from it, without a follow-up If/Switch step.
2. [x] [ARCHITECTURE.md](./ARCHITECTURE.md) — Outcomes are declared on the step type (static or from settings), the same way output schema is. WorkflowCore routing is reused untouched, and old unnamed lines keep working as "Any result".
3. [x] [SPEC.md](./SPEC.md) — Catalogue gains outcomes plus a resolve endpoint, publish blocks stale lines, run records the exit taken, and ActionNode draws one exit per outcome.
4. [x] [STORIES.md](./STORIES.md) — 9 stories in two epics: actions branch on their own outcomes (slice 1), and the built-in actions get real exits (slice 2).
5. [x] [PLAN.md](./PLAN.md) — 22 tasks. The first parallel group (T1 to T3) is the contract, the binding helper and the output-schema 400 fix.
6. [ ] [BUILD-LOG.md](./BUILD-LOG.md) — <pending>

See [DECISION-LOG.md](./DECISION-LOG.md) for why things changed along the way.
