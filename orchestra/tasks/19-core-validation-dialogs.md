# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 19-core-validation-dialogs: dialogs.json validation

## Context
Read `orchestra/PROJECT.md` and SPEC §2 Phase 2 / §4 in `orchestra/SPEC.md`. Core is netstandard2.0, no TaleWorlds references,
JSON via Newtonsoft.Json (already referenced). Models, issue codes and rule constants already exist — use them, do not duplicate:
- `src/AlbocariusCompanions.Core/Models/*.cs` (CompanionDefinition, DialogFile/DialogLine, CompanionPackage)
- `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `IssueCodes.cs` (issues are keyed by **folder name**: `ValidationIssue.Folder`)
- `src/AlbocariusCompanions.Core/Rules/*.cs` (DialogSlots, LineIds, CompanionRules: regexes, ranges, voice types)

## Goal
Implement `DialogChecks.Validate(IReadOnlyList<CompanionPackage>)` in `src/AlbocariusCompanions.Core/Validation/DialogChecks.cs`. Returns issues, never throws; all are errors; `Folder` = package.FolderName; companion id for line-id checks = `CompanionPackage.Id`. `Lines` may be null (treat as empty).
- slot missing or not `DialogSlots.IsKnown` → `UnknownSlot` (message contains the slot).
- line id missing, or (slot known and) not `LineIds.IsValid(id, companionId, slot)` → `InvalidLineId` (message contains the id). Unknown slot → only `UnknownSlot`, no line-id error.
- line ids unique globally (within a companion and across all packages): every occurrence after the first → `DuplicateLineId` (message contains the id) on the folder of that occurrence.
- every `DialogSlots.Required` slot needs >= 1 line, else one `MissingRequiredSlot` per slot (message contains the slot).
- text null, without `en`, or `en` blank → `MissingEnglishText`. Each key not matching `CompanionRules.LanguageCodePattern` → `InvalidLanguageCode` (message contains the key).
- voiced line with `CompanionRules.PlayerTokenPattern` in ANY language's text → one `PlayerTokenInVoicedLine` per line. Unvoiced lines may contain tokens.
- audio (when not null): empty, rooted (`/x`, `\\x`, `C:\\x`, `C:/x`), or containing a `..` segment with either separator → `InvalidAudioPath`.
- Exactly one error per broken rule.
Files allowed to edit: `src/AlbocariusCompanions.Core/Validation/DialogChecks.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/DialogValidationTests.cs` and the helper `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
Read the tests first: they are the precise contract (codes, severities, ordering, messages that must contain a field/slot/key name).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(dotnet 10.0.113 on this machine sometimes aborts with "Internal CLR error" — that is infra, just rerun.)
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
