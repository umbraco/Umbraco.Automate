# Brief: Library element actions

Automation builders on Umbraco 18 can't read, create, change or publish a Library element from an automation. The content actions only work on documents, so a shared element that many pages pick still has to be edited by hand.

Source: GitHub discussion [#510](https://github.com/umbraco/Umbraco.Automate/discussions/510), widened to the full set of element actions.

## Why now
- umbraco.com is adding shared JSON-LD snippets as Library elements. One holds the G2 review rating and count that many pages copy by hand today. A daily automation (Scheduled → HTTP Request to G2 → Update Element Property → Publish Element) would update every page at once, but the last two steps don't exist.

## Done when
- Six element actions exist, each the counterpart of a content action: **Get Element**, **Get Element Property**, **Create Element**, **Update Element Property**, **Publish Element** and **Unpublish Element**.
- An automation builder picks a Library element as the target, or binds the key from an earlier step, as they can with the content actions.
- The actions take a culture, and Update Element Property also takes a segment, the same way the content actions do.
- The umbraco.com scenario runs end to end on a schedule: after a run, every page that picks the snippet shows the new rating with no manual edit.
- A missing element ends in the `notFound` outcome and a missing property alias ends in property-not-found, matching the content actions.
- An element in the Library recycle bin counts as missing for every service account, whatever its Library start node. The same goes for a trashed Library folder as the target of Create Element. #514 (issue #507) made this the rule for every action that loads an item (`docs/identity-ownership-permissions.md`), after root-start-node accounts were found writing to trashed content.
- Create Element refuses an element type the backoffice wouldn't create in that place (not "Allow in Library", or not allowed in that folder), failing the step with a validation error. #528 (issue #515) fixed the same gap in Create Content and Create Media, which had skipped the backoffice's allowed-type checks.
- The service account needs the Library section and the matching element permission (Read, Create, Update, Publish or Unpublish), including permissions set on a Library folder. Without them the step fails with the same kind of permission error the content actions give.
- The run view shows what each step read or changed, as it does for the content actions.

## Must work with
- Umbraco CMS 18.x, on the v18 line only. The element services exist from CMS 18.0.0, and the package's floor is 18.1.0 (`Directory.Packages.props:19`).
- SQL Server and SQLite.

## Unknowns
- Automate's permission checker only knows content and media (`AutomationActionAuthorizer.cs:38`, `:77`). Elements have their own granular and folder permissions in CMS 18, so this needs a new check path that hasn't been tried.
- Whether publishing an element is enough for pages that pick it to show the new value straight away, or whether their cache needs something more.
- Whether the backoffice element picker can sit in an Automate settings field with a binding expression, the way the content picker does since #443.
- `IElementEditingService.UpdateAsync` doesn't refuse a trashed element (`ElementEditingService.cs:174`, CMS 18.1.0), so the trashed check has to be Automate's. The CMS doesn't refuse creating in a trashed folder either (it only checks the folder exists, `:254`; the `InTrash` check at `:357` is for move and restore), but it does enforce "Allow in Library" and folder type filters on create (`:111`, `:120`). Not yet checked: whether element publish and unpublish refuse trashed elements, and whether the published element cache drops them the way the content cache does.
- What Create Element needs from the builder that Create Content doesn't: a Library folder instead of a parent page, and only element types with "Allow in Library" turned on.

## Not doing
- **Porting to v17.** CMS 17 has no `IElementPublishingService`, so there is nothing to build on.
- **Porting to v19 for now.** v18 only; port up when someone asks.
