# Build log

- **T1** (05-10-2026): #443's branch was already on the `v18/dev` tip (`00783f8`), so no rebase was needed. Added the plan folder, the pending specs and the `CLAUDE.md` plan-folder line. Verified the branch builds, and the 7 pending unit specs compile and are skipped.
- **T2** (05-10-2026, `07f705f`): `EditableModelValueKind` plus `ValueKind` on field descriptors. 7 specs active and green, and the full unit project is 1958/1958. Reviewer: PASS. Smoke: the real `GET catalogue/actions` on the demo site returns Publish Content `contentKey: "String"`, HTTP Request `bodyMode: "Scalar"` (an enum) and `headers`/`formFields: "Collection"`.
