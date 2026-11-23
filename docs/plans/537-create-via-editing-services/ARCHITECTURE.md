# Architecture: Create Content and Create Media follow the CMS create rules

Both actions keep their authorisation and type lookup, then build a CMS create model and hand it to `IContentEditingService.CreateAsync` or `IMediaEditingService.CreateAsync`. The CMS decides whether the create is allowed, converts the property values through each property editor and validates them, and the action maps the status it returns onto the step's outcomes. Create Media puts its downloaded file in a CMS temporary file and passes its key on `umbracoFile`, the same way a backoffice upload does. What the steps do: [SPEC](./SPEC.md).

![Create flow](./diagrams/create-flow.svg)

> [!IMPORTANT]
> **5 guesses need your OK.**
>
> 1. Top-level values for the Content Picker, Member Picker, Multinode Treepicker and Multi URL Picker that are in their stored format (UDIs, `udi` links) are rewritten to the editor format before the CMS sees them, with a run-log warning. Everything else goes through unchanged and is covered by the release notes.\
>    *If not:* release notes only. Those picker values are then silently dropped, because the editors return `null` for them (evidence below).
> 2. `IMediaFileDownloader` gets a new default interface member, `DownloadToTemporaryFileAsync`, and the old `DownloadToPropertyAsync` is marked `[Obsolete]` (removal in Umbraco Automate 19).\
>    *If not:* a new public interface for the temporary-file download, and `IMediaFileDownloader` stays as it is.
> 3. Create Media calls `IMediaEditingService.ValidateCreateAsync` before `CreateAsync`, and stops when it reports property errors. The CMS converts the file property, which copies the file into the media folder, before it decides not to save, so a refused media item would otherwise leave an orphan file.\
>    *If not:* call `CreateAsync` only, and accept the orphan file on a refused media item. That is what the backoffice does too.
> 4. New content keeps no template, as today. The editing service sets `TemplateId` to null when no template key is given, and `IContentService.Create` never set one either.\
>    *If not:* pass the content type's default template, as the backoffice does. That changes the front end of every page an automation creates.
> 5. No mockup. The only visible change is one more exit on two canvas nodes, drawn by the existing outcomes mechanism like every other built-in exit.\
>    *If not:* mock the two nodes before planning is approved.

## Decisions
- **Create through `IContentEditingService.CreateAsync` / `IMediaEditingService.CreateAsync`**, not `IContentService` with filters run by hand. Agreed on #537 (option 1). It runs every CMS create rule in one place: allowed at root, allowed children, every registered `IContentTypeFilter`, trashed parent (`ContentEditingServiceBase.cs:601-666` at `release-18.1.0`).
- **Remove #528's root and allowed-child checks** and the `GetById` trashed-parent check. The CMS returns `NotAllowed`, `ParentNotFound` and `InTrash` for the same cases.
- **Keep `IAutomationActionAuthorizer` before the create.** The editing services check CMS rules, not user permissions. The Management API checks those in its own authorisation layer, which the action doesn't pass through.
- **Keep the content or media type lookup before the create.** The action needs it to build the model: the type's culture variance, each property's variance and editor alias, and dropping unknown aliases. The CMS refuses a whole create with `PropertyTypeNotFound` when one alias is unknown (`ContentEditingServiceBase.cs:562-567`), where the action has always skipped it with a warning.
- **Parse the Properties JSON with the CMS's own `JsonObjectConverter`**, not today's `PropertyValuesJson` conversion. It's the converter the Management API reads request values with (`ConfigureUmbracoBackofficeJsonOptions.cs:54`), and the Management API passes the values to the editing service unchanged (`ContentEditingPresentationFactory.cs:14-19`). So each property editor gets exactly the CLR types it gets from the backoffice: `List<string>`, `JsonObject`, `JsonArray`, `int`, `long`, `double`, `bool`, `string` (`JsonObjectConverter.cs:40-116`). Today's parser gives `long` for numbers and raw JSON text for arrays, which the Tags, Dropdown, Checkbox list and Repeatable textstrings editors read as "no value" (they want `IEnumerable<string>`).
- **Map the editing service's status in one internal helper shared by both actions**, so the two can't drift and #551 can reuse it.
- **Create Media's file goes through `ITemporaryFileService`**, not `IMediaImportService`. `IMediaImportService` creates with `IMediaService` and skips the editing service too (`MediaImportService.cs:58-88`). The temporary file is the documented path: the backoffice uploads to the temporary-file endpoint, then sends `{ "temporaryFileId": "<key>" }` on the file property, and the Image Cropper and File Upload editors turn that into a media path (`ImageCropperPropertyValueEditor.cs:131-221`, `FileUploadPropertyValueEditor.cs:113-176`).
- **The downloader creates the temporary file**, not the action. The size cap, SSRF protection and file naming already live there, so the action only sees a key and a file name.
- **Always delete the temporary file after the create attempt.** When the CMS used it, it already enlisted its own delete (`FileUploadPropertyValueEditor.cs:149`, `ImageCropperPropertyValueEditor.cs:178`), so the second delete is a no-op. When the create was refused before the file was read, nothing else would delete it until the CMS's cleanup job.
- **Keep the old constructors as `[Obsolete]` overloads** that fetch the new services from `StaticServiceProvider`, the same as `NotifyEditorAction.cs:46-61`. The new constructors carry `[ActivatorUtilitiesConstructor]`. Both actions are in released tags with today's constructors.
- **One `validationFailed` outcome key on both actions**, with an action-specific description. Content is saved even with property errors (`ContentEditingService.cs:141-158`), media is not (`MediaEditingService.cs:114-118`), so the same key means "the values failed" on both and the description says whether an item exists.

## What changes

**Server: Core**

```text
Umbraco.Automate/src/Umbraco.Automate.Core/
├── Actions/BuiltIn/
│   ├── CreateContentAction.cs          # IContentEditingService.CreateAsync; old ctor [Obsolete]; validationFailed outcome
│   ├── CreateContentOutput.cs          # new InvalidProperties
│   ├── CreateMediaAction.cs            # IMediaEditingService.ValidateCreateAsync + CreateAsync; temporary file; old ctor [Obsolete]
│   ├── CreateMediaOutput.cs            # new InvalidProperties
│   ├── CreateContentSettings.cs        # Property Values description: editor format
│   ├── CreateMediaSettings.cs          # Property Values description: editor format
│   ├── PropertyValuesJson.cs           # parse with JsonObjectConverter; legacy picker rewrite
│   └── ContentEditingStatusMapper.cs   # new, internal: status -> outcome or failure, shared by both actions
└── Cms/
    ├── IMediaFileDownloader.cs         # new DownloadToTemporaryFileAsync (default member); DownloadToPropertyAsync [Obsolete]; result gains TemporaryFileKey
    └── MediaFileDownloader.cs          # download into ITemporaryFileService
```

**Backoffice**

```text
Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/lang/
└── en.ts   # uaOutcomes.validationFailed, plus the two descriptions
```

**Tests**

```text
Umbraco.Automate/tests/
├── Umbraco.Automate.Tests.Unit/Actions/BuiltIn/   # the pending specs, plus today's Create tests moved onto the editing services
├── Umbraco.Automate.Tests.Unit/Cms/MediaFileDownloaderTests.cs
└── Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/Outcomes/   # a Validation failed spec on the demo site
```

- **Events:** the save now goes through the editing service, which raises the same `ContentSavingNotification` / `MediaSavingNotification` as before. `EnsureUmbracoContext` stays around the call, because notification handlers resolve URLs.
- **Culture permissions:** for culture-variant content, the CMS puts back the old value for every culture the service account can't edit (`ContentEditingServiceBase.cs:811-893`). On a new item that means empty, so a service account without access to the step's culture creates the item with those values missing. The backoffice behaves the same. The release notes say so.

Not touched: persistence (no tables), Deploy (no new entity), the Management API (no endpoint), the canvas code (outcomes are drawn from the action's declaration), Update Content Property and Update Media Property (#551).

## Evidence for the two open questions on #537

**1. How Create Media's download fits the editing service** (all at `release-18.1.0`):

- `MediaCreateModel` adds nothing to `ContentCreationModelBase` (`Key`, `ContentTypeKey`, `ParentKey`) and its `Properties` / `Variants`. There's no stream or file field. A file reaches the item only as a property value.
- That value is `{ "src": "", "temporaryFileId": "<key>" }` (`TemporaryFileUploadValueBase.cs`). Both editors find the temporary file with `ITemporaryFileService.GetAsync`, check it (extension against `ContentSettings` and the data type, `IFileStreamSecurityValidator`), copy it into the media file system under the item's key, and enlist the temporary file's delete.
- `ITemporaryFileService.CreateAsync(CreateTemporaryFileModel { Key, FileName, OpenReadStream })` is what the Management API's temporary-file endpoint calls (`CreateTemporaryFileController.cs`). It refuses a blocked extension, an invalid file name or an unsafe stream, and stores the file on local disk under `umbraco/Data/TEMP` (`TemporaryFileService.cs:49-120`, `LocalFileSystemTemporaryFileRepository.cs:89-106`).
- Today's downloader writes with the `SetValue(MediaFileManager, ...)` extension, which stores the file with none of those checks (`ContentExtensions.cs:343`, `:410`). The new path adds them.
- `umbracoFile` is mandatory on every default media type with a file (`DatabaseDataCreator.cs:1683` and five more). `MediaEditingService.CreateAsync` saves nothing when any property fails validation (`MediaEditingService.cs:114-118`). So an Image with no file, or whose download failed, is now refused, where today it was created without a file.

**2. Whether saved Properties JSON needs a conversion.** Today's values are written raw with `SetValue`, the stored format. The editing service passes them to each editor's `FromEditor`, which expects the editor format (`ContentEditingServiceBase.cs:764-793`):

| Editor | Today's value | Editor value | Old values |
|---|---|---|---|
| Text, textarea, numbers, slider, date | as typed | same | work |
| True/false | `true` / `1` | `bool`, `int`, string | work |
| Content picker | `umb://document/…` | GUID | dropped |
| Member picker | `umb://member/…` | GUID | dropped |
| Multinode treepicker | UDI list | `[{type, unique}]` | dropped |
| Multi URL picker | `[{udi, url}]` | `[{type, unique, url}]` | links lost |
| Media picker | `[{key, mediaKey}]` | same | work |
| Block list / grid | block JSON | same envelope | nested values follow these rows |
| Rich text | HTML or `{markup}` | same | work, now sanitized |
| Dropdown, checkbox list, tags | `["a"]` | `["a"]` | work |
| Repeatable textstrings | newline string | `["a"]` | dropped |
| File upload | media path | path or `{src}` | work |
| Image cropper | `{src, crops}` | same | work; a bare path throws |

Sources, all `release-18.1.0`: `ContentPickerPropertyEditor.cs:114-118`, `MemberPickerPropertyEditor.cs:85-88`, `MultiNodeTreePickerPropertyEditor.cs:155-183`, `MultiUrlPickerValueEditor.cs:307-342`, `MediaPicker3PropertyEditor.cs:314-334`, `BlockEditorPropertyValueEditor.cs:98-127` with `BlockValuePropertyValueEditorBase.cs:230`, `RichTextPropertyEditor.cs:386-425` with `RichTextPropertyEditorHelper.cs:37-41`, `MultipleValueEditor.cs:43-51`, `TagsPropertyEditor.cs:209-238`, `MultipleTextStringPropertyEditor.cs:128-142`, `FileUploadValueParser.cs:33-39`, `ImageCropperPropertyValueEditor.cs:252-262`, `TrueFalsePropertyEditor.cs:73-83`.

A one-off migration of saved automations can't fix this: Properties JSON is often bound (`${...}`) and only resolves at run time, and the property's editor is only known once the content type is. Guess 1 rewrites the four picker formats at run time instead, and the release notes cover the rest.

## Version parity

CMS `release-17.6.0` has the same API and behaviour: `IContentEditingService.CreateAsync` and `IMediaEditingService.CreateAsync` have the same signatures, `TryGetAndValidateParentIdAsync` (with the filters) is identical, `MediaEditingService.CreateAsync` refuses on property errors the same way (`MediaEditingService.cs:97-123`), and `FileUploadPropertyValueEditor`, `ImageCropperPropertyValueEditor`, `RichTextPropertyEditor`, `TrueFalsePropertyEditor`, `JsonObjectConverter` and `TemporaryFileService` don't differ. The only differences: `MapCreate` creates the item with a `null` name instead of `string.Empty`, and 18 adds an allowed-type validator to the content picker. Automate's `CreateContentAction.cs`, `CreateMediaAction.cs`, `MediaFileDownloader.cs` and `PropertyValuesJson.cs` are identical on `v17/dev`. The port should be a straight copy.
