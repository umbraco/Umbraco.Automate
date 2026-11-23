# Stories: Create Content and Create Media follow the CMS create rules

**7 stories, 79 criteria.** Story ids are used by `PLAN.md` and the spec files, so never renumber them. S1 is the largest (19 criteria), but every criterion is one status or one field of the same call, so it isn't split.

**Core**

- [S1](#s1-create-content-follows-the-cms-create-rules) Create Content follows the CMS create rules, 19 criteria
- [S2](#s2-create-media-follows-the-cms-create-rules) Create Media follows the CMS create rules, 14 criteria
- [S3](#s3-branch-when-property-values-fail-validation) Branch when property values fail validation, 13 criteria
- [S4](#s4-property-values-reach-the-cms-in-the-editor-format) Property values reach the CMS in the editor format, 15 criteria
- [S5](#s5-create-medias-file-goes-through-a-cms-temporary-file) Create Media's file goes through a CMS temporary file, 11 criteria
- [S6](#s6-code-that-builds-the-actions-keeps-working) Code that builds the actions keeps working, 4 criteria

**Shipping**

- [S7](#s7-a-sites-content-type-filter-holds-against-automations) A site's content type filter holds against automations, 3 criteria (manual smoke, not a spec)

<details>
<summary>Ready and Done</summary>

The same agreement as the repo's earlier plans (`docs/plans/action-outcomes/STORIES.md`).

- **Ready:** role, capability and value stated. Given/When/Then covers the happy path. Out of scope stated. Passes INVEST, Small and Testable especially.
- **Done:** every criterion passes as an executable spec, sad paths included, except S7, which passes as the manual smoke check. `dotnet build` and the Umbraco.Automate tests are green. The change is reviewed. The v17 port is tracked in `PLAN.md`.

</details>

---

## S1: Create Content follows the CMS create rules

As an **automation author**,
I want Create Content to create items the way the backoffice does,
so that an automation can't create content the site's own rules forbid.

Covers SPEC R1, R3 to R9. Out of scope: property values (S4) and validation errors (S3).

**Happy path**

- **AC1: Creates through the editing service**
  Given an invariant content type that the parent allows
  When the Create Content step runs
  Then `IContentEditingService.CreateAsync` is called once
- **AC2: Passes the content type**
  Given an invariant content type that the parent allows
  When the step runs
  Then the create model's `ContentTypeKey` is the step's content type key
- **AC3: Passes the parent**
  Given a parent key on the step
  When the step runs
  Then the create model's `ParentKey` is that key
- **AC4: Creates at the root**
  Given no parent key on the step
  When the step runs
  Then the create model's `ParentKey` is `null`
- **AC5: Names an invariant item**
  Given an invariant content type and the Name "New Page"
  When the step runs
  Then the create model has one variant: culture `null`, segment `null`, name "New Page"
- **AC6: Names a culture-variant item in the step's culture**
  Given a culture-variant content type, the Name "New Page" and the Culture "en-US"
  When the step runs
  Then the create model has one variant: culture "en-US", segment `null`, name "New Page"
- **AC7: Succeeds when the CMS creates the item**
  Given the editing service returns `Success`
  When the step runs
  Then the step succeeds
- **AC8: Outputs the new item's key**
  Given the editing service returns `Success` with a new item
  When the step runs
  Then the output's `ContentKey` is the new item's key

**Sad path**

- **AC9: A refused create fails**
  Given the editing service returns `NotAllowed`
  When the step runs
  Then the step fails
- **AC10: A refused create is a validation error**
  Given the editing service returns `NotAllowed`
  When the step runs
  Then the error category is Validation
- **AC11: The message says where it was refused**
  Given no parent key, and the editing service returns `NotAllowed`
  When the step runs
  Then the error message says the type isn't allowed at the content root
- **AC12: A missing parent takes Parent not found**
  Given the editing service returns `ParentNotFound`
  When the step runs
  Then the step takes the `parentNotFound` outcome
- **AC13: A trashed parent takes Parent not found**
  Given the editing service returns `InTrash`
  When the step runs
  Then the step takes the `parentNotFound` outcome
- **AC14: A type the CMS can't find takes Content type not found**
  Given the editing service returns `ContentTypeNotFound`
  When the step runs
  Then the step takes the `contentTypeNotFound` outcome
- **AC15: A cancelled save is Cancelled**
  Given the editing service returns `CancelledByNotification`
  When the step runs
  Then the error category is Cancelled
- **AC16: An unknown culture is a validation error**
  Given the editing service returns `InvalidCulture`
  When the step runs
  Then the error category is Validation
- **AC17: Any other status is Unknown**
  Given the editing service returns `DuplicateKey`
  When the step runs
  Then the error category is Unknown
- **AC18: A refused service account creates nothing**
  Given the authorizer refuses the parent
  When the step runs
  Then `IContentEditingService.CreateAsync` isn't called
- **AC19: A type key that doesn't resolve takes Content type not found**
  Given the content type key doesn't resolve
  When the step runs
  Then the step takes the `contentTypeNotFound` outcome

## S2: Create Media follows the CMS create rules

As an **automation author**,
I want Create Media to create items the way the backoffice does,
so that an automation can't create media the site's own rules forbid.

Covers SPEC R2 to R9, R15. Out of scope: the file (S5), property values (S4) and validation errors (S3).

**Happy path**

- **AC1: Creates through the editing service**
  Given a media type that the parent folder allows
  When the Create Media step runs
  Then `IMediaEditingService.CreateAsync` is called once
- **AC2: Passes the media type**
  Given a media type that the parent folder allows
  When the step runs
  Then the create model's `ContentTypeKey` is the step's media type key
- **AC3: Passes the parent**
  Given a parent key on the step
  When the step runs
  Then the create model's `ParentKey` is that key
- **AC4: Creates at the root**
  Given no parent key on the step
  When the step runs
  Then the create model's `ParentKey` is `null`
- **AC5: Names the item**
  Given the Name "Sunset"
  When the step runs
  Then the create model has one variant: culture `null`, segment `null`, name "Sunset"
- **AC6: Succeeds when the CMS creates the item**
  Given the editing service returns `Success`
  When the step runs
  Then the step succeeds
- **AC7: Outputs the new item's key**
  Given the editing service returns `Success` with a new item
  When the step runs
  Then the output's `MediaKey` is the new item's key

**Sad path**

- **AC8: A refused create is a validation error**
  Given the editing service returns `NotAllowed`
  When the step runs
  Then the error category is Validation
- **AC9: A missing parent takes Parent not found**
  Given the editing service returns `ParentNotFound`
  When the step runs
  Then the step takes the `parentNotFound` outcome
- **AC10: A trashed parent takes Parent not found**
  Given the editing service returns `InTrash`
  When the step runs
  Then the step takes the `parentNotFound` outcome
- **AC11: A create refused at validation isn't attempted**
  Given `ValidateCreateAsync` returns `NotAllowed`
  When the step runs
  Then `IMediaEditingService.CreateAsync` isn't called
- **AC12: A cancelled save is Cancelled**
  Given the editing service returns `CancelledByNotification`
  When the step runs
  Then the error category is Cancelled
- **AC13: A type key that doesn't resolve takes Media type not found**
  Given the media type key doesn't resolve
  When the step runs
  Then the step takes the `mediaTypeNotFound` outcome
- **AC14: A refused service account creates nothing**
  Given the authorizer refuses the parent folder
  When the step runs
  Then `IMediaEditingService.CreateAsync` isn't called

## S3: Branch when property values fail validation

As an **automation author**,
I want a Validation failed exit that names the failing properties,
so that I can handle bad values without the step pretending all went well.

Covers SPEC R10 to R13, R15. Relies on SPEC guesses 1 and 2.

**Happy path**

- **AC1: Create Content declares the exit**
  Given the Create Content action
  When its outcomes are listed
  Then they include `validationFailed`
- **AC2: Create Media declares the exit**
  Given the Create Media action
  When its outcomes are listed
  Then they include `validationFailed`
- **AC3: The exit has a localised label**
  Given the Create Content action
  When its outcomes are listed
  Then the `validationFailed` label is `#uaOutcomes_validationFailed`
- **AC4: Content with a failing value takes Validation failed**
  Given the editing service saves the item but reports a `PropertyValidationError` on "title", a value the step supplied
  When the Create Content step runs
  Then the step takes the `validationFailed` outcome
- **AC5: The saved content's key is output**
  Given the same as AC4
  When the step runs
  Then the output's `ContentKey` is the saved item's key
- **AC6: The failing aliases are output**
  Given the same as AC4
  When the step runs
  Then the output's `InvalidProperties` is `["title"]`
- **AC7: A successful create outputs no failing aliases**
  Given the editing service returns `Success`
  When the step runs
  Then the output's `InvalidProperties` is empty
- **AC8: Media with a failing value takes Validation failed**
  Given `ValidateCreateAsync` reports a `PropertyValidationError` on "altText"
  When the Create Media step runs
  Then the step takes the `validationFailed` outcome
- **AC9: No media is created**
  Given the same as AC8
  When the step runs
  Then `IMediaEditingService.CreateAsync` isn't called
- **AC10: The media key is empty**
  Given the same as AC8
  When the step runs
  Then the output's `MediaKey` is `Guid.Empty`
- **AC11: The failing media aliases are output**
  Given the same as AC8
  When the step runs
  Then the output's `InvalidProperties` is `["altText"]`

**Sad path (errors that don't count)**

- **AC12: An error in another culture doesn't count**
  Given a culture-variant type, the step's Culture "en-US", and a `PropertyValidationError` only for "da-DK"
  When the Create Content step runs
  Then the step doesn't take the `validationFailed` outcome
- **AC13: A mandatory property the step left out doesn't count**
  Given a `PropertyValidationError` on "summary", which the step's Properties JSON doesn't set
  When the Create Content step runs
  Then the step doesn't take the `validationFailed` outcome

## S4: Property values reach the CMS in the editor format

As an **automation author**,
I want Property Values read the same way the Management API reads them,
so that every property editor gets values it understands, and validates and converts them as it does for the backoffice.

Covers SPEC R18, R19. Out of scope: rewriting values inside blocks, and Repeatable textstrings strings (release notes).

**Happy path**

- **AC1: A string stays a string**
  Given Properties JSON `{"title": "Hello"}`
  When the Create Content step runs
  Then the value sent for "title" is the string "Hello"
- **AC2: A whole number becomes an int**
  Given `{"count": 42}`
  When the step runs
  Then the value sent for "count" is the `int` 42
- **AC3: A boolean stays a boolean**
  Given `{"featured": true}`
  When the step runs
  Then the value sent for "featured" is the `bool` true
- **AC4: An array of strings becomes a list of strings**
  Given `{"tags": ["a", "b"]}`
  When the step runs
  Then the value sent for "tags" is a sequence of strings "a", "b"
- **AC5: An object becomes a JsonObject**
  Given `{"blocks": {"layout": {}}}`
  When the step runs
  Then the value sent for "blocks" is a `JsonObject`
- **AC6: An array of objects becomes a JsonArray**
  Given `{"links": [{"name": "Home"}]}`
  When the step runs
  Then the value sent for "links" is a `JsonArray`
- **AC7: A culture-variant property carries the step's culture**
  Given a culture-variant type with a culture-variant "title", and the step's Culture "en-US"
  When the step runs
  Then the value sent for "title" has culture "en-US"
- **AC8: An invariant property carries no culture**
  Given a culture-variant type with an invariant "code", and the step's Culture "en-US"
  When the step runs
  Then the value sent for "code" has culture `null`
- **AC9: Create Media reads values the same way**
  Given Properties JSON `{"altText": "Sunset"}`
  When the Create Media step runs
  Then the value sent for "altText" is the string "Sunset"
- **AC10: A stored content picker value is rewritten**
  Given a Content Picker property "link" and `{"link": "umb://document/<guid>"}`
  When the Create Content step runs
  Then the value sent for "link" is the string `<guid>`
- **AC11: A stored tree picker value is rewritten**
  Given a Multinode Treepicker property "related" and `{"related": "umb://document/<guid>"}`
  When the step runs
  Then the value sent for "related" serializes to `[{"type":"document","unique":"<guid>"}]`
- **AC12: A stored URL picker link is rewritten**
  Given a Multi URL Picker property "cta" and `{"cta": [{"name": "Home", "udi": "umb://document/<guid>"}]}`
  When the step runs
  Then the first link sent for "cta" has `unique` `<guid>`

**Sad path**

- **AC13: An unknown alias isn't sent**
  Given `{"nope": "x"}` on a type without "nope"
  When the step runs
  Then no value is sent for "nope"
- **AC14: A null value isn't sent**
  Given `{"title": null}`
  When the step runs
  Then no value is sent for "title"
- **AC15: Malformed JSON sends no values**
  Given Properties JSON `{not json`
  When the step runs
  Then the create model has no property values

## S5: Create Media's file goes through a CMS temporary file

As an **automation author**,
I want Create Media's downloaded file to reach the item the way a backoffice upload does,
so that the CMS checks and stores it, and the item is created in one go.

Covers SPEC R14, R16, R17. Relies on SPEC guess 3.

**Happy path**

- **AC1: The file goes into a temporary file**
  Given a Source URL that returns an image with the file name "sunset.jpg"
  When the Create Media step runs
  Then `ITemporaryFileService.CreateAsync` is called with the file name "sunset.jpg"
- **AC2: The file property points at the temporary file**
  Given the same as AC1
  When the step runs
  Then the value sent for "umbracoFile" has `temporaryFileId` set to the temporary file's key
- **AC3: The file name is output**
  Given the same as AC1, and the editing service returns `Success`
  When the step runs
  Then the output's `FileName` is "sunset.jpg"
- **AC4: The temporary file is cleaned up**
  Given the same as AC1, and the editing service returns `Success`
  When the step runs
  Then `ITemporaryFileService.DeleteAsync` is called with the temporary file's key

**Sad path**

- **AC5: A failed download takes File download failed**
  Given a Source URL that returns 404
  When the step runs
  Then the step takes the `fileDownloadFailed` outcome
- **AC6: A failed download still tries to create the item**
  Given the same as AC5
  When the step runs
  Then `IMediaEditingService.CreateAsync` is called once
- **AC7: A failed download sends no file**
  Given the same as AC5
  When the step runs
  Then no value is sent for "umbracoFile"
- **AC8: A file the CMS refuses takes File download failed**
  Given the temporary file service returns `FileExtensionNotAllowed`
  When the step runs
  Then the step takes the `fileDownloadFailed` outcome
- **AC9: A media type without a file property downloads nothing**
  Given a media type with no "umbracoFile" property and a Source URL
  When the step runs
  Then `ITemporaryFileService.CreateAsync` isn't called
- **AC10: A refused create still cleans up**
  Given a downloaded file, and the editing service returns `NotAllowed`
  When the step runs
  Then `ITemporaryFileService.DeleteAsync` is called with the temporary file's key
- **AC11: File download failed wins over Validation failed**
  Given a Source URL that returns 404, and `ValidateCreateAsync` reports the mandatory "umbracoFile" as empty
  When the step runs
  Then the step takes the `fileDownloadFailed` outcome

## S6: Code that builds the actions keeps working

As a **developer who constructs Create Content or Create Media in their own code**,
I want the released constructors to keep working,
so that upgrading Automate within a major doesn't break my build.

Covers SPEC R21.

**Happy path**

- **AC1: The old Create Content constructor is obsolete**
  Given the constructor shipped in 2026.10.1
  When its attributes are read
  Then it has `[Obsolete]`
- **AC2: The old Create Media constructor is obsolete**
  Given the constructor shipped in 2026.10.1
  When its attributes are read
  Then it has `[Obsolete]`
- **AC3: DI picks Create Content's new constructor**
  Given the constructor that takes `IContentEditingService`
  When its attributes are read
  Then it has `[ActivatorUtilitiesConstructor]`
- **AC4: DI picks Create Media's new constructor**
  Given the constructor that takes `IMediaEditingService`
  When its attributes are read
  Then it has `[ActivatorUtilitiesConstructor]`

## S7: A site's content type filter holds against automations

As a **site developer who registers an `IContentTypeFilter`**,
I want automations to be refused where my filter refuses the backoffice,
so that a rule I set on purpose can't be bypassed.

Covers BRIEF's Repro. Manual smoke on the demo site, because registering a filter needs a C# composer.

- **AC1: Refused at the root**
  Given a filter that removes the page type at the root
  When a Create Content step creates that type at the root
  Then the step fails with a validation error
- **AC2: Refused under a parent**
  Given a filter that removes the page type under the home page
  When a Create Content step creates that type under the home page
  Then the step fails with a validation error
- **AC3: Nothing is created**
  Given AC1 and AC2 have run
  When the content tree is checked
  Then neither page exists
