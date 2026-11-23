# Plan: Create Content and Create Media follow the CMS create rules

**10 tasks in 5 areas.** T1, T2, T3 and T6 have no dependencies and run in parallel (group A). T4 and T5 follow (group B), then the acceptance spec and smoke (T7, T8), the release notes (T9), and the v17 port (T10) after the v18 PR merges.

The pending specs are in `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Actions/BuiltIn/EditingServiceCreate/`, 76 of them, one file per story S1 to S6. `CreateActionHarness.cs` builds each action through `ActivatorUtilities` from a container holding both the released and the new constructors' services, with a real `MediaFileDownloader` over a stubbed HTTP response. Keep the harness working as the constructors change, and remove each `Skip = "Pending"` as its task turns the spec green.

## Core

- [ ] **T1** - story: S4 (AC1-AC15). Rewrite `PropertyValuesJson` (`Umbraco.Automate/src/Umbraco.Automate.Core/Actions/BuiltIn/PropertyValuesJson.cs`) to build CMS `PropertyValueModel`s:
  - Parse with `System.Text.Json` and the CMS's `JsonObjectConverter` (`Umbraco.Cms.Infrastructure.Serialization`), into `Dictionary<string, object?>`. Malformed JSON, or JSON that isn't an object, returns `null` as today.
  - Take the type's composition property types (alias, `PropertyEditorAlias`, variance) and the step's culture. Leave out `null` values and aliases the type doesn't have, and report the skipped aliases so the caller can log them. Give culture-variant properties the culture, invariant ones `null`, segment always `null`.
  - Rewrite the stored formats in SPEC R19, top level only, and report each rewritten alias for a run-log warning. Use `UdiParser` for the UDIs.
  - Direct unit tests for the parser in `Umbraco.Automate.Tests.Unit/Actions/BuiltIn/PropertyValuesJsonTests.cs`.

  Acceptance: the parser's own tests are green. The S4 specs go green with T4 and T5.
  depends-on: none. parallel-group: A

- [ ] **T2** - story: S1 (AC9-AC17), S2 (AC8-AC12). Add internal `ContentEditingStatusMapper` in `Umbraco.Automate/src/Umbraco.Automate.Core/Actions/BuiltIn/`. It maps a `ContentEditingOperationStatus` to an outcome key or an `ActionResult.Failed` with the category and message in SPEC R8. It takes the parent's description ("the content root", "the media root", "parent `<key>`") and the type alias for the `NotAllowed` and `InvalidCulture` messages. With unit tests for every status in R8.
  Acceptance: the mapper's tests are green.
  depends-on: none. parallel-group: A

- [ ] **T3** - story: S5 (AC1, AC8). Temporary-file download in `Umbraco.Automate/src/Umbraco.Automate.Core/Cms/`:
  - `IMediaFileDownloader` gets `DownloadToTemporaryFileAsync(string url, string fallbackName, CancellationToken)` as a default interface member that throws `NotSupportedException`. `DownloadToPropertyAsync` gets `[Obsolete("Use DownloadToTemporaryFileAsync. Scheduled for removal in Umbraco Automate 19.")]` (ARCHITECTURE guess 2).
  - `MediaFileDownloadResult` gets an init-only `TemporaryFileKey` (not a positional parameter, so the record's constructor doesn't change).
  - `MediaFileDownloader` implements it: the same URL check, size cap, `HttpResponseBodyReader` and `MediaFileNameResolver` as today, then `ITemporaryFileService.CreateAsync` with a new key, the resolved file name and the buffer. A refused temporary file (`FileExtensionNotAllowed`, `InvalidFileName`, `UploadBlocked`) is a failed result whose reason names the status. Its constructor gains `ITemporaryFileService` and keeps everything `DownloadToPropertyAsync` still needs.
  - Extend `MediaFileDownloaderTests` for the new method, success and each refusal.

  Acceptance: `MediaFileDownloaderTests` are green.
  depends-on: none. parallel-group: A

- [ ] **T4** - story: S1 (AC1-AC19), S3 (AC1, AC3-AC7, AC12, AC13), S4 (AC1-AC8, AC10-AC15), S6 (AC1, AC3). Move `CreateContentAction` onto `IContentEditingService.CreateAsync`:
  - New constructor `(ActionInfrastructure, IContentEditingService, IContentTypeService, IBackOfficeSecurityAccessor, IUmbracoContextFactory, IAutomationActionAuthorizer, ILogger<CreateContentAction>)` with `[ActivatorUtilitiesConstructor]`. The released constructor stays, `[Obsolete("Use the constructor that takes an IContentEditingService. Scheduled for removal in Umbraco Automate 19.")]`, and fetches `IContentEditingService` from `StaticServiceProvider`, as `NotifyEditorAction` does.
  - Keep the settings checks, the authorizer and the content type lookup (SPEC R4 to R7). Remove the trashed-parent `GetById` check and #528's root and allowed-child checks.
  - Build `ContentCreateModel` (SPEC R1) with T1's values. Call `CreateAsync` inside `EnsureUmbracoContext`. Map the status with T2.
  - Add `OutcomeValidationFailed = "validationFailed"` to `GetOutcomes()` with label `#uaOutcomes_validationFailed` and description `#uaOutcomes_validationFailedContentDescription`. Count only the errors in SPEC R11 (SPEC guess 1). Add `InvalidProperties` (`IReadOnlyList<string>`, empty by default) to `CreateContentOutput`. Log each error (R13).
  - Run log on success: "Created '`<name>`' (`<key>`) as `<alias>` under parent `<key>`" or "at the content root" (SPEC guess 4).
  - Update the Property Values description in `CreateContentSettings` (R20).
  - Move `CreateContentActionTests` onto the editing service: drop the tests of removed checks, keep the settings, authorizer and culture tests. Add `validationFailed` to `BuiltInActionOutcomeTests.ContentActionOutcomeKeys`.

  Acceptance: the S1 specs, the content specs of S3, S4 and S6, and the full `Umbraco.Automate.slnx` test run are green.
  depends-on: T1, T2. parallel-group: B

- [ ] **T5** - story: S2 (AC1-AC14), S3 (AC2, AC8-AC11), S4 (AC9), S5 (AC1-AC11), S6 (AC2, AC4). Move `CreateMediaAction` onto `IMediaEditingService`:
  - New constructor `(ActionInfrastructure, IMediaEditingService, IMediaTypeService, IBackOfficeSecurityAccessor, IUmbracoContextFactory, IAutomationActionAuthorizer, IMediaFileDownloader, ITemporaryFileService, ILogger<CreateMediaAction>)` with `[ActivatorUtilitiesConstructor]`. The released constructor stays `[Obsolete]`, same message pattern as T4.
  - Keep the settings checks, the authorizer and the media type lookup. Remove the trashed-parent check and #528's checks.
  - With a Source URL and an `umbracoFile` property, call T3's `DownloadToTemporaryFileAsync`, and on success set `umbracoFile` to `{ "src": "", "temporaryFileId": "<key>" }` as a `JsonObject`, replacing any Properties JSON value for it (R14). On failure, create without the file (R17).
  - Call `ValidateCreateAsync`, then `CreateAsync` only when it returns `Success` (R15, ARCHITECTURE guess 3). Map both with T2. File download failed wins over Validation failed.
  - Delete the temporary file with `ITemporaryFileService.DeleteAsync` in a `finally` after the create attempt (R16).
  - Add `validationFailed` with description `#uaOutcomes_validationFailedMediaDescription`, and `InvalidProperties` on `CreateMediaOutput`.
  - Update the Property Values description in `CreateMediaSettings` (R20). Move `CreateMediaActionTests` onto the editing service, and add `validationFailed` to `BuiltInActionOutcomeTests.MediaActionOutcomeKeys`.

  Acceptance: the S2 and S5 specs, the media specs of S3, S4 and S6, and the full `Umbraco.Automate.slnx` test run are green.
  depends-on: T1, T2, T3. parallel-group: B

## Backoffice

- [ ] **T6** - story: S3 (AC3). Add to `uaOutcomes` in `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/lang/en.ts`: `validationFailed: "Validation failed"`, `validationFailedContentDescription` and `validationFailedMediaDescription` with SPEC R10's text. Build the frontend from the repo root.
  Acceptance: `npm run build` is green, and the keys match T4's and T5's.
  depends-on: none. parallel-group: A

## Acceptance and smoke

- [ ] **T7** - **wire: both actions into the demo site.** story: S3 (AC4, AC8), S1 (AC7). Add `outcomes.create-validation.spec.ts` under `Umbraco.Automate/tests/Umbraco.Automate.Tests.AcceptanceTests/tests/DefaultConfig/Outcomes/`, built like `outcomes.recycle-bin.spec.ts`. Seed a document type with a text property that has a regex, and publish an automation where Create Content sets a value that fails it, with a step on `success` and one on `validationFailed`. Then run it, plus the existing Create Content and Create Media cases in `outcomes.recycle-bin.spec.ts` and `automation.bindable-settings.spec.ts`, against the demo site.
  Acceptance: the new spec takes the `validationFailed` path and the page exists as a draft, and the existing Create specs still pass, through the real DI-built actions.
  depends-on: T4, T5, T6

- [ ] **T8** - **wire: the repro on the demo site.** story: S7 (AC1-AC3). Run the BRIEF's Repro with a scratch `IContentTypeFilter` composer in `demos/v18/` (never committed). Also run Create Media with a public image URL: the item has its file, and `umbracoWidth`, `umbracoHeight`, `umbracoBytes` and `umbracoExtension` are filled. Check the step's run log for each case.
  Acceptance: all three S7 criteria hold, and the image smoke shows the file and the auto-filled properties.
  depends-on: T7

## Docs

- [ ] **T9** - story: S1-S7. Put SPEC's "Release notes" in the v18 PR description under a "Behaviour changes" heading. Draft, for Rick to approve, the UmbracoDocs follow-up for the Create Content and Create Media pages, and a comment for #551 that points at T1's parser and T2's mapper and at `UpdateAsync` replacing every property (`ContentEditingServiceBase.cs:221`, `:747-762` at `release-18.1.0`).
  Acceptance: the PR description has the section, and both drafts are in the PR's "Left out".
  depends-on: T8

## Port

- [ ] **T10** - story: S1-S6. Once the v18 PR merges, port it to `v17/dev` with `change-backport`. The four Automate files are identical on `v17/dev`, and CMS `release-17.6.0` has the same editing-service create API and editors (ARCHITECTURE "Version parity"). Run the same specs, and T7's acceptance spec on a v17 demo if one can be set up. The v17 PR gets the same release notes.
  Acceptance: a v17 PR is open with the specs and the full suite green.
  depends-on: T9
