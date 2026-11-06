[Discussion #510](https://github.com/umbraco/Umbraco.Automate/discussions/510) | [Plan folder](https://github.com/umbraco/Umbraco.Automate/tree/e317db6bdb9040b75e3b6f2c3929781e88530958/docs/plans/library-element-actions)

> [!NOTE]
> **Rick reviewed the 7 choices made during the build.** Kept as built:
>
> - Public `AuthorizeElementAsync` / `AuthorizeElementParentAsync` overloads taking a list of permissions.
> - `ua-document-type-picker`, which adds the required check the CMS document type picker lacks.
> - The element picker opening the CMS element and folder picker modals itself.
> - Create Element's create and property save in one transaction.
> - Unpublish Element reporting the two invariant/variant mismatches as Validation.
> - The culture-from-type bug Update Element Property shares with Update Content and Media Property goes to one follow-up for all three.
>
> Changed: Get Element and Get Element Property no longer write a success line to the run log, matching Get Content.

## Why the change

Automations can now read, create, change, publish and unpublish Umbraco 18 Library elements, so one shared snippet (like the G2 review rating that umbraco.com pages pick) can be updated on a schedule instead of by hand.

## What it looks like

**Where to find it:**
- Automation › a workspace with a service account › an automation › Add action › the Library group
- Any of the six Library steps › Settings

**Who can see it:** the actions show up when the workspace's service account has access to the Library section. Each step also needs that account's element permission (Read, Create, Update, Publish or Unpublish), including any permissions set on a Library folder.

Step picker, filtered to "element":

![Step picker with the Library group](https://github.com/umbraco/Umbraco.Automate/blob/e317db6bdb9040b75e3b6f2c3929781e88530958/docs/plans/library-element-actions/screenshots/step-picker.png?raw=true)

Update Element Property settings, with an element picked:

![Update Element Property settings](https://github.com/umbraco/Umbraco.Automate/blob/e317db6bdb9040b75e3b6f2c3929781e88530958/docs/plans/library-element-actions/screenshots/update-element-property.png?raw=true)

Create Element settings, saved with Element Type empty:

![Create Element settings](https://github.com/umbraco/Umbraco.Automate/blob/e317db6bdb9040b75e3b6f2c3929781e88530958/docs/plans/library-element-actions/screenshots/create-element.png?raw=true)

## How to verify

1. On a v18 demo site, create a Library element type with a text property (`customSchema`), a Library folder "Reviews" and an element "G2 review rating" in it, and publish the element. Create a page whose element picker picks it, with a template that prints the property.
2. In a workspace whose service account has the Library section, add an automation: Manual trigger → **Update Element Property** (pick the element, alias `customSchema`, a new value) → **Publish Element**. The picker opens the Library tree, and the binding switch works on the Element field.
3. Run it. Both steps complete, and the page shows the new value on its next load.
4. Point Update Element Property at an element in the Library recycle bin. The step takes **Not found** and nothing is saved.
5. Add **Create Element** with Folder "Reviews", an element type with "Allow in Library", a Name and some Property Values JSON. Run it: the element is created as a draft in "Reviews" with those values. Leave Element Type empty and save the step: it shows "This field is required".
6. Access: give the service account's group a permission set on "Reviews" without Create, and run Create Element again. The step fails, naming `Umb.Element.Create` and the folder.

## Good to know

- v18 only. CMS 17 has no element publishing service, so there's no port.
- The acceptance specs were run against a demo site on SQL Server LocalDB, as CI uses. The local SQLite demo threw `database is locked` on content and element tests alike.
- Publishing an element updates pages that already rendered it on their next request, so there's no cache-refresh code. Checked on one page with no output cache.
- Follow-up issues are drafted, not filed: the culture rule in all three Update actions, Create Content's Content Type not enforcing "required", `CannotUnpublishWhenReferenced` reported as Unknown in both unpublish actions, and a shared helper for the per-action copies.
- The UmbracoDocs change for the actions page is drafted at the end of `BUILD-LOG.md`.

## Change outline

How every element step checks access and handles a missing or trashed element.

![Element step flow](https://github.com/umbraco/Umbraco.Automate/blob/e317db6bdb9040b75e3b6f2c3929781e88530958/docs/plans/library-element-actions/diagrams/element-step-flow.svg?raw=true)

New files in `Umbraco.Automate.Core`:

```diff
 Actions/BuiltIn/
+├── GetElementAction.cs / Settings / Output              # published element cache
+├── GetElementPropertyAction.cs / Settings / Output
+├── CreateElementAction.cs / Settings / Output           # CreateAsync, then Save, in one scope
+├── UpdateElementPropertyAction.cs / Settings / Output   # IElementService.Save
+├── PublishElementAction.cs / Settings / Output          # IElementPublishingService.PublishAsync
+└── UnpublishElementAction.cs / Settings / Output        # IElementPublishingService.UnpublishAsync
 Security/
 ├── IAutomationActionAuthorizer.cs        # + AuthorizeElementAsync (×2), AuthorizeElementParentAsync, fail-closed defaults
 ├── AutomationActionAuthorizer.cs         # + IElementPermissionService, IElementContainerPermissionService
 └── AutomationActionAuthorizerExtensions.cs  # + AuthorizeElementOrFailAsync
 Extensions/
 └── AutomationAuthorizationResultExtensions.cs  # + list-of-permissions overloads for elements
 Cms/
 ├── IContentValueNormaliser.cs            # + IPublishedElement overloads, default forwards content
 └── ContentValueNormaliser.cs             # one read path; content keeps its own Value overload
 Constants.cs                              # + ElementKeyPicker, DocumentTypePicker UI aliases
```

Backoffice registrations:

| Alias | Element | Change |
|---|---|---|
| `Umb.Automate.ElementKeyPicker` | `ua-element-key-picker` | Added: picks one element, or one folder with `folderOnly` |
| `Umb.Automate.DocumentTypePicker` | `ua-document-type-picker` | Added: CMS document type picker with a required check |
| `uaOutcomes_folderNotFound`, `uaOutcomes_elementTypeNotFound` | `lang/en.ts` | Added: Create Element's outcome labels |

The six actions:

| Action | Permission | Outcomes |
|---|---|---|
| Get Element | Read | Found (default), Not found |
| Get Element Property | Read | Found (default), Not found, Property not found |
| Create Element | Create (on the folder, or the Library root) | Created (default), Folder not found, Element type not found |
| Update Element Property | Update | Updated (default), Not found, Property not found |
| Publish Element | Publish | none: a missing or trashed element fails the step |
| Unpublish Element | Unpublish | none: a missing or trashed element fails the step |

<details>
<summary>Checked and fine (7)</summary>

- Create Element takes Property Values in Create Content's JSON format, setting them after the create (Rick's call, DECISION-LOG T10).
- The step picker reads Content, Core, Library, Media, sorted by name (Rick confirmed).
- The Element Type picker greys out page types instead of hiding them, as the CMS does (Rick confirmed; STORY-8 AC8 reworded).
- Folder access goes through `IElementContainerPermissionService`, because `IElementPermissionService` looks up element keys only at 18.1.0.
- Create Element checks a trashed folder itself: the CMS's create only checks that the folder exists.
- `IContentValueNormaliser` still reads content through the content `Value` overload, so content keeps the overridable fallback and per-content segment.
- `AuthorizeElementParentOrFailAsync` was removed before it shipped, because nothing called it.

</details>

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01NNrj1N3TENGjXFQaLmeS4S
