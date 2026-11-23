# Spec: Create Content and Create Media follow the CMS create rules

> [!IMPORTANT]
> **5 guesses need your OK.**
>
> 1. For Create Content, "Validation failed" counts only errors on values the step supplied, in the step's culture or on invariant properties. A mandatory property the step left out doesn't count, because Create Content saves a draft, which the backoffice also allows with mandatory fields empty. Errors in other cultures don't count either: the CMS validates every site language on create (`ContentValidationServiceBase.cs:63-67`), and the step only creates one.\
>    *If not:* every CMS error counts. On a content type with a mandatory property the step doesn't fill, every run takes Validation failed, and a saved automation wired only to Created stops after this step.
> 2. The new output field is `InvalidProperties`, a list of the failing aliases. The CMS's messages go to the run log only.\
>    *If not:* a list of `{ alias, culture, messages }` objects in the output, which can be bound in later steps but is bulkier.
> 3. When the download fails, Create Media still asks the CMS to create the item without a file, and takes File download failed either way. The item exists only when the media type's file property is optional, so `MediaKey` is empty for Image, File and the other default types.\
>    *If not:* skip the create when the download fails, so File download failed always means "nothing created".
> 4. The run log says "under parent `<key>`" instead of the parent's name, because the action no longer loads the parent.\
>    *If not:* keep a parent lookup just for the log line.
> 5. Values in another property's stored format are rewritten only for the four pickers in ARCHITECTURE guess 1, and only at the top level, not inside blocks.\
>    *If not:* release notes only, or a wider rewrite (Repeatable textstrings, nested block values).

## Endpoints
None.

## Settings
None. The Property Values field descriptions change on both actions (R20).

## Rules

**Creating**

- **R1** Create Content calls `IContentEditingService.CreateAsync` once, with the content type key, the parent key (`null` at the root), one variant and the parsed property values. The variant's name is the step's Name. Its culture is the step's Culture for a culture-variant type, otherwise `null`. Its segment is always `null`.
- **R2** Create Media calls `IMediaEditingService.CreateAsync` once, with the media type key, the parent key (`null` at the root), one invariant variant named after the step's Name, and the parsed property values plus the file (R14).
- **R3** Neither action checks "allowed at root" or the parent's allowed children itself. The CMS decides, including every registered `IContentTypeFilter`.
- **R4** Unchanged: the step fails with a validation error before any CMS call when the type key, Name or parent key is missing or malformed, and when a culture-variant content type has no Culture.
- **R5** Unchanged: `IAutomationActionAuthorizer` runs before the create. A refusal fails the step with an Authentication error, and a parent it reports as not found takes Parent not found. In both cases the editing service isn't called.
- **R6** Unchanged: a type key that doesn't resolve takes Content type not found / Media type not found without calling the editing service.
- **R7** The user passed to the editing service is the backoffice user, or else the run's service account, as today.

**What the CMS answers**

- **R8** The editing service's status, from `CreateAsync` or Create Media's `ValidateCreateAsync` (R15), maps to the step's result:
  - `Success` → Created (the default exit), with the new item's key in the output.
  - `PropertyValidationError` → Validation failed (R11).
  - `ParentNotFound`, `InTrash` → Parent not found, nothing created.
  - `ContentTypeNotFound` → Content type not found / Media type not found.
  - `NotAllowed` → the step fails with a validation error: "Content type 'blogPost' isn't allowed at the content root" or "…under 'Home'". The message adds that the type's "Allow at root", the parent type's allowed child types, or a content type filter on the site decides this. The CMS doesn't say which one refused.
  - `InvalidCulture` → the step fails with a validation error: "Culture 'xx-YY' isn't a language on this site".
  - `CancelledByNotification` → the step fails as Cancelled.
  - Any other status → the step fails as Unknown, naming the status.
- **R9** Nothing is retried. A validation, authentication or cancelled failure follows the step's error behaviour as today.

**Validation failed**

- **R10** Both actions declare a `validationFailed` outcome, labelled `#uaOutcomes_validationFailed` ("Validation failed"). Create Content's description (`#uaOutcomes_validationFailedContentDescription`): "The item was saved as a draft, but some property values failed validation." Create Media's (`#uaOutcomes_validationFailedMediaDescription`): "Some property values failed validation, so nothing was created."
- **R11** Create Content takes Validation failed when at least one counted error remains (guess 1). The item is saved: `ContentKey` is set. Create Media takes it on any property error. Nothing is saved: `MediaKey` is empty.
- **R12** The output's `InvalidProperties` lists each failing alias once, in the order the CMS reported them (guess 2). It's empty on every other result.
- **R13** The run log gets one warning per failing alias, with the culture when there is one and the CMS's messages.

**Create Media's file**

- **R14** With a Source URL, Create Media downloads the file (the same size cap, SSRF protection and file naming as today) into a CMS temporary file. It then sets `umbracoFile` to `{ "src": "", "temporaryFileId": "<key>" }`. An `umbracoFile` value in the Properties JSON is ignored when a Source URL is set.
- **R15** Before creating, Create Media calls `IMediaEditingService.ValidateCreateAsync` with the same model. When it reports property errors, the step takes Validation failed without calling `CreateAsync`, so no file is copied into the media folder.
- **R16** After the create attempt, Create Media always deletes the temporary file it created.
- **R17** When the download fails, or the CMS refuses the temporary file (blocked extension, invalid file name, unsafe content), Create Media still creates without a file and takes File download failed (guess 3). File download failed wins over Validation failed, so a mandatory file property that's now empty doesn't change the exit; `InvalidProperties` still lists it. The run log gives the reason, with the URL's query string hidden as today. A media type without `umbracoFile` takes File download failed without downloading, as today.

**Property values**

- **R18** The Properties JSON is parsed the way the Management API reads values (`JsonObjectConverter`): a string stays a string, a whole number becomes `int` (or `long`), any other number `double`, `true`/`false` `bool`, an object `JsonObject`, an array of strings `List<string>`, an array of objects `JsonArray`. `null` values and empty arrays are left out. Malformed JSON, or JSON that isn't an object, sends no values and logs a warning, as today. An alias the type doesn't have is left out with a warning, as today. A culture-variant property gets the step's culture; an invariant one gets `null`.
- **R19** A top-level value for one of these editors, in its stored format, is rewritten to the editor format with a run-log warning (guess 5):
  - Content Picker, Member Picker: `umb://document/<guid>` or `umb://member/<guid>` → `<guid>`.
  - Multinode Treepicker: `umb://document/<a>,umb://media/<b>` → `[{ "type": "document", "unique": "<a>" }, { "type": "media", "unique": "<b>" }]`.
  - Multi URL Picker: an item with `udi` → `type` and `unique` from the UDI; an item with only `url` → `type: "external"`.
- **R20** Both Property Values field descriptions say the values use "the same format as the Management API", with a picker example.

**Compatibility**

- **R21** The old constructors still compile, are marked `[Obsolete]` ("Scheduled for removal in Umbraco Automate 19."), and run the same as the new ones.
- **R22** New content keeps no template, as today (ARCHITECTURE guess 4).

## Targets
None: each step makes one create call (two for media), the same order of work as today.

## Release notes

For the v18 and v17 PR descriptions, and the UmbracoDocs follow-up:

- Create Content and Create Media now follow every CMS create rule, including content type filters a site registers. A create the backoffice refuses now fails the step with a validation error.
- Property Values use the format the Management API takes. Text, numbers, dates, media pickers, block JSON, rich text and dropdown arrays are unchanged. Content, member, tree and URL picker values in the old stored format are rewritten and logged. Repeatable textstrings need an array (`["a", "b"]`), and pickers inside block values need the editor format.
- A new Validation failed exit. Content is still saved as a draft. Media isn't created.
- Create Media no longer creates Image, File or other media with a mandatory file when there's no file or the download fails. It takes File download failed (or Validation failed when there was no Source URL), and nothing is created.
- Downloaded files now pass the CMS's upload checks: blocked extensions (`Umbraco:CMS:Content:DisallowedUploadedFileExtensions`) and file safety. A refused file takes File download failed.
- For culture-variant content, values in a culture the service account can't edit are left empty, as in the backoffice.
- Rich text values are run through the site's HTML sanitizer.
