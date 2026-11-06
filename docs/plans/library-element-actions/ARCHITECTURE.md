# Architecture: Library element actions

Six new built-in actions, each a copy of its content counterpart pointed at the CMS 18 element services. They check access through a new element path in `IAutomationActionAuthorizer`, and treat a missing or trashed element as not found.

![Element step flow](./diagrams/element-step-flow.svg)

## Decisions
- **Built-in actions in Core, found by their `[Action]` attribute**, like the content actions. No new extension point, no registration code.
- **One action per content action, same settings, outputs and outcomes**, not one "manage element" action with a mode switch. An author who knows Update Content Property already knows Update Element Property.
- **A new "Library" group in the step picker**, not inside "Content". It matches the CMS section name and keeps Content from growing to 15 actions.
- **New members on `IAutomationActionAuthorizer` with default implementations that refuse access**, not a separate element authorizer. Actions already take this one interface, and a decorator a site registered keeps compiling. The defaults follow the `TODO (V19)` pattern already on the interface.
- **The authorizer wraps `IElementPermissionService` for elements and `IElementContainerPermissionService` for the Create Element folder.** Both have the same statuses and start-node rules as the content service, so access matches what the backoffice allows, folder permissions included. The folder can't go through `IElementPermissionService`: it looks keys up as elements only (`ElementPermissionService.cs:44`, `GetAllPaths([Element], …)`), so a folder key comes back `NotFound`. The container service looks up `ElementContainer` paths (`ElementContainerPermissionService.cs:40`), the same at 18.1.0 and the current 18.x.
- **Trashed elements are missing.** Every action that loads an `IElement` checks `Trashed` after loading it, as #514 made the rule (the two Get actions read the published cache, which drops trashed elements) (`docs/identity-ownership-permissions.md`). The authorizer also gets the same "hidden in the recycle bin" check for scoped accounts, using the element bin path `-1,-22,`.
- **Update Element Property saves through `IElementService.Save`**, like Update Content Property, not `IElementEditingService.UpdateAsync`. `UpdateAsync` takes the whole set of property values and names, so writing one value through it means rebuilding everything else from the loaded element.
- **Create Element goes through `IElementEditingService.CreateAsync`**, unlike Create Content. `IElementService` has no create method, and the editing service already refuses an element type that isn't "Allow in Library" or that the folder doesn't allow (`ElementEditingService.cs:111`, `:120`, `:238`). That's the #528 check without copying it. Both come back as the same `NotAllowed` status, so the action looks up the type and folder names for the error message. The editing service doesn't refuse a trashed folder on create (it only checks the folder exists, `:254`; the `InTrash` check at `:357` is for move and restore), so the action loads the folder through `IElementContainerService` and checks `Trashed` itself. Property Values don't go through `CreateAsync`, whose editors expect the backoffice format. The action creates the element with its name only, then sets the values and saves through `IElementService.Save`, so values use Create Content's format (Rick's call, T10). Both writes sit in one `ICoreScopeProvider` scope, so a failed second save rolls back the create.
- **Publish and Unpublish go through `IElementPublishingService`**, like the content actions use `IContentPublishingService`.
- **Get Element and Get Element Property read the published element cache** (`IPublishedElementCache`), like the content Get actions read the published content cache. The cache drops trashed elements (`ElementCacheService.cs:331`).
- **`IContentValueNormaliser` gets overloads that take `IPublishedElement`**, so element properties come out in the same shape as content properties. Default implementations hand an `IPublishedContent` to the existing method and throw for anything else.
- **A new `ua-element-key-picker` property editor UI** stores a single key as a string, the same job `ua-media-key-picker` does for media. The CMS's own element picker stores an array, which the settings form can't bind. It opens the CMS's element and folder picker modals itself rather than wrapping `umb-input-element`, because that input opens its picker through a modal route the step settings panel has no router for (found in T12). It shows a key that no longer resolves as "Not found" with Remove. The existing bindable editor wraps it, so the binding switch works with no extra code.
- **Element Type uses a new `ua-document-type-picker` (`Umb.Automate.DocumentTypePicker`)**, which wraps the CMS's `umb-input-document-type` with `onlyPickElementTypes` and adds the required check the CMS picker lacks (found in T12). It can't filter to "Allow in Library" types; the CMS refuses the rest when the step runs. Page types show greyed out, as in the CMS.

## What changes

**Server: `Umbraco.Automate/src/Umbraco.Automate.Core/`**

```text
Actions/BuiltIn/
├── GetElementAction.cs                 # published cache read
├── GetElementSettings.cs
├── GetElementOutput.cs
├── GetElementPropertyAction.cs
├── GetElementPropertySettings.cs
├── GetElementPropertyOutput.cs
├── CreateElementAction.cs              # IElementEditingService.CreateAsync, then IElementService.Save, in one scope
├── CreateElementSettings.cs
├── CreateElementOutput.cs
├── UpdateElementPropertyAction.cs      # IElementService.Save
├── UpdateElementPropertySettings.cs
├── UpdateElementPropertyOutput.cs
├── PublishElementAction.cs             # IElementPublishingService.PublishAsync
├── PublishElementSettings.cs
├── PublishElementOutput.cs
├── UnpublishElementAction.cs           # IElementPublishingService.UnpublishAsync
├── UnpublishElementSettings.cs
└── UnpublishElementOutput.cs
Security/
├── IAutomationActionAuthorizer.cs      # + AuthorizeElementAsync (×2), AuthorizeElementParentAsync
├── AutomationActionAuthorizer.cs       # wraps IElementPermissionService + IElementContainerPermissionService
└── AutomationActionAuthorizerExtensions.cs  # + AuthorizeElementOrFailAsync
Extensions/
└── AutomationAuthorizationResultExtensions.cs  # + AuthorizeElementAsync / AuthorizeElementParentAsync taking IReadOnlyList<string>
Cms/
├── IContentValueNormaliser.cs          # + IPublishedElement overloads
└── ContentValueNormaliser.cs
Constants.cs                            # + ElementKeyPicker and DocumentTypePicker UI aliases
```

**Backoffice: `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/`**

```text
core/components/element-key-picker/
├── element-key-picker.element.ts       # opens the CMS element/folder picker modals, stores one key as a string
└── manifests.ts                        # Umb.Automate.ElementKeyPicker
core/components/document-type-picker/
├── document-type-picker.element.ts     # wraps umb-input-document-type, adds the required check
└── manifests.ts                        # Umb.Automate.DocumentTypePicker
core/manifests.ts                       # registers both
lang/en.ts                              # + uaOutcomes_folderNotFound, uaOutcomes_elementTypeNotFound
```

**Tests**

```text
Umbraco.Automate/tests/
├── Umbraco.Automate.Tests.Unit/Actions/BuiltIn/   # one *ActionTests.cs per action
├── Umbraco.Automate.Tests.Unit/Security/AutomationActionAuthorizerElementTests.cs  # element and folder access
├── Umbraco.Automate.Tests.Unit/Cms/ContentValueNormaliserTests.cs   # element values in content shape
└── Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/
    ├── Outcomes/outcomes.built-in.spec.ts         # element outcomes
    ├── Outcomes/outcomes.recycle-bin.spec.ts      # trashed element, root and scoped accounts
    └── Automations/automation.library-elements.spec.ts  # picker, folder permissions, publish reaches the page
```

**Docs**

```text
docs/
├── identity-ownership-permissions.md   # element methods on the authorizer
└── functional-overview.md              # Library row in the provider table
```

- **Audit log:** the four write actions are `ICmsAction`, so the existing middleware writes an audit entry, as for content.
- **Run log:** the four write actions (Create, Update, Publish, Unpublish) write one line naming the element, using `ActionLogFormat.Item`, as the content write actions do. The two Get actions write no success line, matching Get Content and Get Content Property (Rick's call on #574).

- **Section checks and the permission drift health check:** pick up `RequiredSections = [Library]` with no change.

Not touched: Deploy (the Automate connector doesn't track picked documents or media either), database and migrations (reads and writes Umbraco's own data), Management API (the catalogue lists new actions on its own), search index (no Find Element), v17 and v19 lines (see BRIEF).
