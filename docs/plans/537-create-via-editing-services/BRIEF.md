# Brief: Create Content and Create Media follow the CMS create rules

Create Content and Create Media skip the CMS's own create rules. A site that registers an `IContentTypeFilter` to stop a content type at the root, or under a parent, can still get that item created by an automation, although the backoffice and the Management API refuse it (#537). This hits site developers who rely on content-type filters, and automation authors who expect a create step to behave like the backoffice.

> [!IMPORTANT]
> **2 guesses need your OK.**
>
> 1. Update Content Property and Update Media Property (#551) stay a separate follow-up, not folded into this plan. They need `UpdateAsync`, which replaces every property on the item, so they need their own design (see "Not doing").\
>    *If not:* the plan grows by two actions and a read-every-value-then-write design, and the PR doubles in size.
> 2. "Release notes" means a behaviour-change section in the v18 and v17 PR descriptions, plus a follow-up docs PR to UmbracoDocs for the two actions' pages. The repo has no docs page for these actions, and the changelog is generated from commit titles.\
>    *If not:* name where the release notes should go (a GitHub release body, the marketplace readme, somewhere else).

## Why now
- Found in an exploratory test pass (#537). Only sites that register their own content-type filter are affected, but for them it's a way round a rule the site set on purpose.
- #528 (and #548 on v17) added hand-written root and allowed-child checks. They copy two of the CMS rules and miss the rest: content-type filters, and anything the CMS adds later.
- Move, Publish and Unpublish already go through the editing services. Create is the odd one out.

## Done when
- Create Content and Create Media are refused wherever the backoffice refuses the same create, including under a registered `IContentTypeFilter`.
- An automation author can branch on a new "Validation failed" exit, which names the properties that failed.
- Property values in the Properties JSON reach the CMS in the same format the backoffice and the Management API send.
- Create Media's downloaded file reaches the item the way a backoffice upload does.
- Code that builds either action with its old constructor still compiles and runs.
- The same change is on v17/dev.
- The PR descriptions say what changes for saved automations.

## Must work with
- Umbraco CMS 18.x on `v18/dev` (18.1.0 today), then CMS 17.x on `v17/dev` (floor 17.6.0), ported separately with `change-backport`.
- The approach agreed by Rick on #537 ("Notes for the fix", option 1): `IContentEditingService.CreateAsync` and `IMediaEditingService.CreateAsync`, the CMS statuses mapped onto the existing outcomes, #528's hand-written checks removed, a "validation failed" outcome with the failing aliases, values run through the property editors' conversion, and an `[Obsolete]` constructor overload.
- Both actions shipped in tag `2026.10.1` and still have the same constructors in `2026.10.4`, so public signatures stay backwards compatible within each major.
- Existing authorisation through `IAutomationActionAuthorizer` (service-account start nodes and permissions). The editing services don't check user permissions.
- Bound settings (`${...}`) in the name, parent, culture, Source URL and Properties JSON.
- No dependency upgrades.

## Unknowns
- How far saved automations' Properties JSON already uses the stored-value format for pickers, which the editors' conversion drops. There's no telemetry, so the design has to protect those values or the release notes have to warn clearly.
- Whether the CMS's checks on a downloaded file (allowed extensions, file safety) refuse files that Create Media accepts today.

## Not doing
- **Running content-type filters by hand beside #528's checks (option 2 on #537).** Ruled out by Rick: it copies CMS rules instead of using them.
- **Update Content Property and Update Media Property.** Tracked in #551. See guess 1.
- **Changing the CMS.** The fix has to work with CMS 18.1 and 17.6 as they are.

## Repro
1. In the demo site, register an `IContentTypeFilter` (a scratch composer, not committed) whose `FilterAllowedAtRootAsync` removes the page type, and whose `FilterAllowedChildrenAsync` removes it under the home page.
2. In the backoffice, try to create that page type at the root and under the home page. Both are refused.
3. Run a Manual automation with a Create Content step for that type at the root, then one under the home page.
4. **Today:** both steps take "Created", and the pages exist.
5. **Fixed:** both steps fail with a validation error that says the type isn't allowed there, and no page is created.
