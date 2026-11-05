# Decision log: Library element actions

## 04-11-2026 · feature-explore
- **No v17 port.** CMS 17 has no `IElementPublishingService` (checked against the latest 17.x tag), so the feature can't exist there.
- **Discussion #510's section and permission questions are answered.** CMS 18 has the `library` section (`Constants.Applications.Library`) and the `Umb.Element.Update` / `Umb.Element.Publish` permissions, all present since 18.0.0. Nothing to ask.
- **Full set of six actions, not just update and publish.** Rick chose Get, Get Property, Create, Update Property, Publish and Unpublish over the two the G2 use case needs.
- **Culture and segment work like the content actions.** Elements vary by culture in CMS 18, so the settings stay consistent across content and element actions.
- **v18 only, no v19 port for now.** Rick's call; port up when someone asks.
- **Element permissions checked as strictly as content.** Library section plus the element permission, including folder-level permissions, so a service account can't write elements it couldn't touch in the backoffice.
- **Trashed elements count as missing for every account (from #507).** CMS 18 gives users a Library start node (`StartElementId`) and elements their own recycle bin, so #507's split between root and scoped accounts would come back here. Rick asked for it to be considered here too.

## 05-11-2026 · feature-explore (refinement)
- **Checked the PRs merged into `v18/dev` since 04-11.** Two change this brief:
  - #514 (issue #507) is merged. Its rule is a `Trashed` check in each action after loading the item, written into `docs/identity-ownership-permissions.md`. The element actions follow that rule, so "how #507 lands" is no longer an unknown.
  - #528 (issue #515) made Create Content and Create Media enforce allowed child types. Create Element must match, so it's now a "Done when" line. The CMS element create service already checks "Allow in Library" and folder filters.
- **Not relevant here:** #527 (Find Content skips trashed items) because there's no Find Element in scope. #519 and #529 (invariant numbers and ISO dates in bindings) already apply to any value bound into Update Element Property, so nothing extra is needed. #486, #520, #522, #524, #525, #530 and #534 don't touch actions on items.

## 05-11-2026 · feature-design
- **Six built-in actions in Core, one per content action**, not a single element action with a mode switch. Same settings, outputs and outcomes as the content action each one copies.
- **Element access goes through new members on `IAutomationActionAuthorizer`**, with default implementations that refuse access so a site's decorator keeps compiling. A separate element authorizer was rejected because every action already takes this interface.
- **Update Element Property saves with `IElementService.Save`**, not `IElementEditingService.UpdateAsync`, which needs the element's whole set of values to write one.
- **Create Element uses `IElementEditingService.CreateAsync`**, unlike Create Content: `IElementService` has no create method, and the editing service already does the trashed-folder and allowed-type checks.
- **New `ua-element-key-picker` UI**, because the CMS element picker stores an array and the settings fields hold one key as a string. Same reason `ua-media-key-picker` exists.
- **Corrected while checking:** the step picker doesn't disable actions for a missing section. Publishing the automation is refused instead (`AutomationService.cs:402`), so the spec says that.
- **Rick confirmed four design calls:** a new "Library" group in the step picker (not inside Content); Create Element keeps the `parentNotFound` key with new labels "Folder not found" and "Element type not found"; Get Element's output has no folder key (it would cost an extra lookup per step); Publish and Unpublish Element fail the step on a missing or trashed element, like their content versions, rather than getting a `notFound` exit.
- **Rick approved the three mockups** (step picker, Update Element Property settings, Create Element settings) as drawn.

## 05-11-2026 · feature-plan
- **The folder guess is settled from CMS source, not a build task.** `IElementPermissionService` looks up element keys only (`GetAllPaths([Element], …)`, `ElementPermissionService.cs:44` at 18.1.0 and now), so a folder key comes back `NotFound`. Create Element checks the folder through `IElementContainerPermissionService`, which looks up `ElementContainer` paths. ARCHITECTURE updated. The Automate integration test project has no CMS host, so the acceptance test "Folder permissions" (T14) proves it against a real site instead.
- **The authorizer (T2) comes before every action.** All six call it, and its fail-closed defaults decide what a site's decorator sees.
- **The six actions build in parallel (group B).** Each adds only its own files. Only Create Element touches `lang/en.ts`, and the shared `BuiltInActionOutcomeTests.cs` cases go to the wire task (T11), so no two parallel tasks edit the same file.
- **Acceptance specs are split by file across T11–T14.** T12 and T14 both edit `automation.library-elements.spec.ts`, so T14 waits for T12.
- **T14 stops and asks before adding cache refresh code.** If a page still shows the old value after Publish Element, that's the BRIEF's open question about the cache, and it needs Rick's call.
- **Corrected while writing specs: the CMS doesn't refuse creating an element in a trashed folder.** BRIEF and ARCHITECTURE said it did (`InTrash`, `ElementEditingService.cs:357`), but that check is only on move and restore. On create, 18.1.0 only checks that the folder exists (`:254`). Create Element now checks the folder's `Trashed` itself, the same #514 rule as everywhere else.
- **Get Element and Get Element Property don't check `Trashed`.** `IPublishedElement` has no such property, and the published cache already drops trashed elements.
- **Pending C# specs are wrapped in `#if LIBRARY_ELEMENT_ACTIONS`.** `bindable-settings` used reflection to keep its specs compiling, which doesn't scale to six new actions and the authorizer. Each task deletes its file's guard. Pending acceptance specs are `test.fixme`, as before.

## 05-11-2026 · feature-build
- **T2: a trashed folder hidden from a scoped account is "not found", unlike content and media parents, which fail.** STORY-1 AC11 asks for it, so Create Element can take `parentNotFound` for every account.
- **T2: "no folder" uses `IElementPermissionService.AuthorizeRootAccessAsync`**, the same call the CMS's create-element endpoint makes for the Library root. The container service's root check has the same body.
- **T2 note for the action tasks:** `AuthorizeElementOrFailAsync` and `AuthorizeElementParentOrFailAsync` turn "not found" into an Authentication failure, as the content pair does. Actions with a `notFound` or `parentNotFound` outcome call the interface member and branch on `IsNotFound` first, as the content actions do.
