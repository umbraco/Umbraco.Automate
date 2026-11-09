# Brief: Library element actions and triggers

Automation builders on Umbraco 18 can't read, create, change or publish a Library element from an automation, and can't start one when an editor saves or publishes an element. The content actions and triggers only work on documents, so a shared element that many pages pick still has to be edited by hand.

Source: GitHub discussion [#510](https://github.com/umbraco/Umbraco.Automate/discussions/510), widened to the full set of element actions, then to element triggers.

## Why now
- umbraco.com is adding shared JSON-LD snippets as Library elements. One holds the G2 review rating and count that many pages copy by hand today. A daily automation (Scheduled → HTTP Request to G2 → Update Element Property → Publish Element) would update every page at once, but the last two steps don't exist.
- Parity with content and media for triggers. Elements are first-class in Umbraco 18, so the Library group should have triggers as the Content and Media groups do.

## Done when

**Actions**
- Six element actions exist, each the counterpart of a content action: **Get Element**, **Get Element Property**, **Create Element**, **Update Element Property**, **Publish Element** and **Unpublish Element**.
- An automation builder picks a Library element as the target, or binds the key from an earlier step, as they can with the content actions.
- The actions take a culture, and Update Element Property also takes a segment, the same way the content actions do.
- The umbraco.com scenario runs end to end on a schedule: after a run, every page that picks the snippet shows the new rating with no manual edit.
- A missing element ends in the `notFound` outcome and a missing property alias ends in property-not-found, matching the content actions.
- An element in the Library recycle bin counts as missing for every service account, whatever its Library start node. The same goes for a trashed Library folder as the target of Create Element. #514 (issue #507) made this the rule for every action that loads an item (`docs/identity-ownership-permissions.md`).
- Create Element refuses an element type the backoffice wouldn't create in that place (not "Allow in Library", or not allowed in that folder), failing the step with a validation error, as #528 (issue #515) made Create Content and Create Media do.
- The service account needs the Library section and the matching element permission (Read, Create, Update, Publish or Unpublish), including permissions set on a Library folder. Without them the step fails with the same kind of permission error the content actions give.
- The run view shows what each step read or changed, as it does for the content actions.

**Triggers**
- Four triggers exist in the Library group: **Element Saved**, **Element Published**, **Element Unpublished**, and **Element Batch Saved**, which fires once with every element in a bulk save, matching the content triggers.
- An editor saving, publishing or unpublishing a Library element in the backoffice starts the automations that use the matching trigger, with the element's key in the output, so a following Get Element or Update Element Property step can use it.
- Each trigger has the content triggers' two settings: an **Element Types** filter, so it fires only for chosen types, and **When triggered by another automation**, which skips on a cycle by default.
- The output matches the content triggers', in element terms: the element's key, name, type key and alias, the cultures involved, and for Saved whether the element is new. Cultures are reported the way the content triggers report them.
- A trigger only fires for elements the workspace's service account can read: the Library section, its Library start node, and permissions set on a Library folder. A batch trigger leaves out the elements the account can't read, as the content and media triggers do.
- A change made by an automation step (for example Update Element Property) doesn't loop back into the same automation, as with the content triggers.

## Must work with
- Umbraco CMS 18.1.0 and later, on the v18 line only (`Directory.Packages.props:19`). The element services and notifications exist from 18.0.0; the culture details the element notifications carry arrived in 18.1.0.
- SQL Server and SQLite.

## Unknowns
- Automate decides who receives a trigger event through `NodeScopedTriggerDispatchAuthorizer`, which knows content and media only (`Dispatch/Authorization/NodeScopedTriggerDispatchAuthorizer.cs:129-138`). Whether it can take elements without changing how dispatch works.
- Whether moving an element to or from the Library recycle bin raises a Saved notification, and whether the trigger should fire for it. The content triggers' behaviour is the reference.
- Whether the "skip on cycle" check recognises an element change made by Create Element or Update Element Property the same way it recognises content changes.

## Not doing
- **Trashed and Deleted triggers.** Rick chose the three content events only.
- **Batch Published and Batch Unpublished.** CMS 18.1.0 raises one notification per element publish or unpublish (content only batches re-published descendants, and elements have none), so they would always carry one item. Rick dropped them.
- **A folder filter on the triggers.** The content triggers have no parent filter either.
- **Property values in the trigger output.** A following Get Element step reads them.
- **Porting to v17.** CMS 17 has no Library and no `IElementPublishingService`.
- **Porting to v19 for now.** v18 only; port up when someone asks.
