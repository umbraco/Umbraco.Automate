# Bindable settings: pick or bind

Read these in order:

1. [x] [BRIEF.md](./BRIEF.md) — Settings fields with a non-text editor, such as content and media pickers, can't take a binding. This slice lets any such field be picked or bound, which closes #83 and settles #206 vs #443.
2. [x] [ARCHITECTURE.md](./ARCHITECTURE.md) — #443's wrapper owns the switch and #206 is folded in. Content keys use CMS's document picker, and media items get a thin Automate picker with a per-field folder filter. The server adds `valueKind`, so only string fields are wrapped, and `BindingMustResolve`, so a bound optional parent can't silently resolve to the root.
3. [x] [SPEC.md](./SPEC.md) — `valueKind` (string / scalar / collection) on field descriptors, failing on empty parent bindings, the wrapper's routing (decided once, on load) and switch behaviour, the media key picker, the 15 adopting core fields, and the acceptance tests.
4. [x] [STORIES.md](./STORIES.md) — 6 stories in three epics: pick or bind one value (STORY-1 to STORY-3), core fields adopt it (STORY-4 content, with parent bindings that must resolve; STORY-5 media), and the v17 port (STORY-6).
5. [x] [PLAN.md](./PLAN.md) — 12 tasks on #443's branch. After the rebase, group A runs in parallel: the `valueKind` server change, the `BindingMustResolve` check and the media key picker. A human gate comes before #443 and #206 are touched, and the v17 port is last.
6. [x] [BUILD-LOG.md](./BUILD-LOG.md) — v18 build done: 11 of 12 tasks across 20 commits, reviewed and smoke-tested, PR #443 updated. T11 (the v17 port) follows once #443 merges.

See [DECISION-LOG.md](./DECISION-LOG.md) for why things changed along the way.
