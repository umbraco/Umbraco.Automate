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
- **T5: the element field is labelled "Element"**, as SPEC and the approved mockup have it, not "Element Key" after "Content Key". The descriptions still follow the content actions. The other element actions use the same label.
- **T5: element actions pass `RequiredPermissions` to the authorizer through the internal `ToSet`.** The content actions use a public `IReadOnlyList` overload of `AuthorizeContentAsync`. An element equivalent would be new public API, so it's left as a cleanup for the whole-feature review.
- **T5: Get Element writes a success run-log line (R16).** Get Content doesn't, so this is new for the element actions only.
- **T7: Update Element Property inherits the content action's culture rule, a bug that was already there.** Update Content Property and Update Media Property choose the culture from the content type's variation, not the property's. So on a culture-variant type, writing an invariant property (or a segment on a property that doesn't vary by segment) makes `Property.SetValue` throw `NotSupportedException`. The element action copies this so all three behave the same. Fixing it is a follow-up for all three actions, not this feature. It needs Rick's call, because the G2 snippet would hit it if its type varies by culture and `customSchema` doesn't.
- **T9: Unpublish Element maps `CannotPublishInvariantWhenVariant` and `CannotPublishVariantWhenNotVariant` to Validation**, where Unpublish Content leaves them as Unknown. The action picks null or `"*"` itself, so the only way to get either is an author naming a non-default culture on an invariant element, which is a settings mistake. No spec pins this yet.
- **T10: Rick chose one Property Values format for every action.** Passing values to `IElementEditingService.CreateAsync` runs them through each editor's `FromEditor`, which expects the backoffice format. So list and picker values written as Create Content takes them were silently dropped. Create Element now creates the element with `CreateAsync` (keeping the "Allow in Library" and folder type checks), then sets the values and saves through `IElementService.Save`, as Create Content and Update Element Property do. The cost is a second save, and values aren't validated on the way in, as in Create Content. SPEC R10 and STORY-7 AC17 changed to match. Rejected: parsing the JSON into the backoffice editor format, which keeps one save and the validation warning but gives pickers and lists a different format from Create Content.
- **T11: the step picker reads Content, Control Flow, Core, Library, Media.** Groups sort by name (`node-picker-modal.element.ts:160`), so Library sits between Content and Media as SPEC says, but not directly after Content as the approved mockup drew it. Putting it there would take a product change to the group order, which no task asks for. Rick confirmed on 06-11-2026 that the Content, Core, Library, Media order is fine.
- **T12: the element picker opens the CMS pickers itself.** The CMS `umb-input-element` opens its picker through a modal route, which needs a router context the step settings panel doesn't have, so Choose did nothing. `ua-element-key-picker` now has its own Choose button and opens `UMB_ELEMENT_PICKER_MODAL` / `UMB_ELEMENT_FOLDER_PICKER_MODAL`, showing the pick with `umb-entity-item-ref`.
- **T12: Element Type uses a new `ua-document-type-picker` (`Umb.Automate.DocumentTypePicker`).** The CMS document type picker ignores "required", so an empty Element Type saved. The wrapper adds the required check, as `ua-media-key-picker` does for media. Create Content's Content Type field uses the same CMS picker and probably has the same gap. That's a follow-up, not this feature.
- **T12: the Element Type picker lists page types greyed out and unpickable instead of hiding them.** That's how the CMS's `umb-input-document-type` handles `onlyPickElementTypes`: it limits what can be picked, not what's listed. Rick confirmed on 06-11-2026 that this is fine. STORY-8 AC8 now reads "only element types can be picked".
- **T14 answered the BRIEF's cache question: no extra cache refresh is needed.** A page that picks an element and has already rendered it shows the new value on the next request after Publish Element, on the LocalDB demo site at CMS 18.1. Tested once, against one page with one picker and no output cache.
- **Whole-feature review, refactor round:**
  - **`AuthorizeElementParentOrFailAsync` is removed.** It had no callers once Create Element branched on not-found itself, and an unused public method would have been permanent API. T2's note above refers to it.
  - **Public `AuthorizeElementAsync` / `AuthorizeElementParentAsync` extensions taking `IReadOnlyList<string>`** sit beside the content one. The element actions use them instead of the internal `ToSet`, so a third-party element action can do the same. This settles the overload T5 deferred.
  - **Create Element's two writes now share one scope.** Checked at CMS 18.1.0: nested scopes join the outer one, a failed child rolls the whole thing back, and save notifications and cache refreshes wait for the outermost scope to complete. A failed property save therefore leaves no element with only its name.
  - **Not done here:** one shared helper for the per-action private copies (`NormaliseCulture`, `DescribeUpdate`, the save-result and publish-status mappers, the acting-user key). The content and media actions keep the same copies, so it's a follow-up across all of them.
- **Rick went through the seven "Your call" items on PR #574 (06-11-2026).**
  - **Kept:** the public element authorize overloads; `ua-document-type-picker`; the element picker opening the CMS modals itself; Create Element's single scope; Unpublish Element's Validation mapping.
  - **The culture-from-type bug** goes to one follow-up for all three Update Property actions, not this PR.
  - **Changed:** Get Element and Get Element Property no longer write a success run-log line, matching Get Content. SPEC R16 and STORY-2 AC6 were updated.
- **Follow-up for both unpublish actions, not this feature:** `CannotUnpublishWhenReferenced` falls through to Unknown. With the CMS's `DisableUnpublishWhenReferenced` on, a Library element that pages pick is exactly what gets refused, so Validation would describe it better.
- **T7: no variation context on writes.** `IElement.GetValue`/`SetValue` key values on nullable culture and segment and don't read the variation context, so the #205 "Parameter 'key2'" problem (published-cache reads) can't happen here. That matches Update Content Property.

## 06-11-2026 · feature-explore (triggers folded in)
- **Library element triggers join #574.** Rick asked whether there were element triggers. There weren't, so a separate brief was started and then folded into this feature at his request. Its separate plan folder (`library-element-triggers`, never committed) was deleted.
- **Driver: parity with content and media.** No single customer scenario.
- **Rick's scope:**
  - three events, the content set: Saved, Published and Unpublished, each with a batch variant;
  - the content triggers' two settings (an Element Types filter, and skip on cycle by default);
  - the content triggers' small output, with no property values;
  - no folder filter;
  - v18 only.
- **Trashed and Deleted triggers** (which media has) are left out for now.
- **The brief's action unknowns are answered and removed.** The answers are in the feature-plan and feature-build entries above: the element access path, the page cache, the picker in settings, the trashed checks.

## 06-11-2026 · feature-design (triggers)
- **Six triggers named Element Saved / Published / Unpublished and Element Batch …**, not "Library Element …", to match the actions in the same Library group.
- **Dispatch reuses `NodeScopedTriggerDispatchAuthorizer`** with a new internal `IElementScopedTriggerOutput` marker and one case calling #574's `AuthorizeElementAsync` with Read. A separate element dispatch authorizer was rejected: the existing one already narrows batches.
- **Rick: Element Saved skips moves, trashing and restoring**, matching Content Saved. Found while designing: CMS 18.1.0 raises `ElementSavedNotification` for element moves, trash and restore, where content moves skip the Saved notification.
- **Rick: Element Unpublished fires when a restore unpublishes an element**, because the CMS really does unpublish it. The docs will say so.
- **Loop protection is generic**, through `AutomationOriginMiddleware`. The element actions are covered with no extra work.
- **`ContentCultureHelpers` widens to `IPublishableContentBase`** rather than gaining element copies.
- **Rick approved the two trigger mockups** (trigger picker, Element Saved settings) as drawn.

## 06-11-2026 · feature-plan (triggers)
- **Rick dropped Element Batch Published and Element Batch Unpublished.** Writing the specs showed that CMS 18.1.0 raises one Published or Unpublished notification per element. Content and elements share `PublishableContentServiceBase`, and content only gets a many-item notification for re-published descendants, which elements don't have. So those two would always carry one item. **Checked whether to raise a CMS issue: no.** Elements behave exactly as content does; content Batch Published only batches descendants. Element Batch Saved stays: a bulk save does raise one notification for many elements.
- **Matched the content triggers on three points the first trigger spec draft got wrong (parity is the driver):**
  - an invariant element's `cultures` is null, not `["*"]` (R23, STORY-9 AC9);
  - a filtered batch fires with all its items when any matches (R20, STORY-9 AC12);
  - the move check skips only existing elements, because a new element's ParentId reads as dirty (ARCHITECTURE guess, T17).
- **06-11-2026, feature-build (T19):** SPEC R19 now says restoring a Library folder fires Element Unpublished once per published element inside, and that content restores raise no event. This follows from Rick's "fire, and document it" call; the CMS unpublishes each element (`ElementContainerService.cs:316` at 18.1.0).
