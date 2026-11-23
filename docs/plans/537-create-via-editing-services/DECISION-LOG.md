# Decision log

## 06-11-2026: Brief

- **Option 1, agreed by Rick on #537** ("Notes for the fix"): create through `IContentEditingService` / `IMediaEditingService`, remove #528's checks, add a "validation failed" outcome, run values through the property editors, keep `[Obsolete]` constructors, port to v17. Option 2 (running the filters by hand) is ruled out. Not re-opened here.
- **#551 stays a follow-up (guess, BRIEF 1).** `UpdateAsync` removes every property the model doesn't carry (`ContentEditingServiceBase.cs:221`, `:747-762` at `release-18.1.0`), so the update actions need a read-every-value design that this plan doesn't. They reuse this plan's parser and status mapper. CMS 18 also has a document patch engine in the Management API that 17.6 doesn't, which may change that design on v18 only.

## 06-11-2026: Design

- **Open question 1 settled: the file goes through `ITemporaryFileService`.** `MediaCreateModel` has no file field, and both upload editors read `temporaryFileId` from the property value. `IMediaImportService` was rejected: it creates with `IMediaService` and skips the editing service too.
- **Open question 2 settled: no stored conversion.** Properties JSON is often bound and only resolves at run time, and the editor is only known once the type is, so a migration can't rewrite it. Text, numbers, dates, media pickers, block envelopes, rich text, dropdown and tag arrays and file paths keep working. Content, member, tree and URL picker values in the stored format are rewritten at run time (guess); everything else is in the release notes.
- **Parse values with the CMS's `JsonObjectConverter`**, so editors get the same CLR shapes as from the Management API. Today's parser gives `long` and raw JSON text, which several editors read as no value.
- **Keep the authorizer and the type lookup** in front of the create. The editing services don't check user permissions, and the CMS refuses a whole create for one unknown alias, where the action has always skipped it.
- **Media validates before it creates (guess).** The CMS copies the file into the media folder while converting values, before it decides not to save media with property errors, so validating first avoids an orphan file.
- **Specs live in the feature worktree**, not on trunk. Other sessions work in sibling worktrees at the same time.

## 06-11-2026: Plan

- **T1, T2, T3 and T6 have no dependencies** (parser, status mapper, downloader, localisation keys), so they can be built and reviewed in parallel before the two actions.
- **T4 and T5 can run in parallel**: they share T1 and T2 but touch different action, settings, output and test files. Both edit `BuiltInActionOutcomeTests.cs`, in different `TheoryData` members, so expect a trivial merge.
- **The specs build the actions through `ActivatorUtilities`** from a container holding both constructors' services, so they compile against today's code and run against whichever constructor DI picks after the build.
- **The S7 repro stays a manual smoke.** Registering an `IContentTypeFilter` needs a C# composer, which the Playwright suite can't add to the demo site.
