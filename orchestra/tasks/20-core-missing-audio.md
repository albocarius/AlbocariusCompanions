# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 20-core-missing-audio: Missing-audio report

## Context
Read `orchestra/PROJECT.md` and SPEC §2 Phase 2 / §4 in `orchestra/SPEC.md`. Core is netstandard2.0, no TaleWorlds references,
JSON via Newtonsoft.Json (already referenced). Models, issue codes and rule constants already exist — use them, do not duplicate:
- `src/AlbocariusCompanions.Core/Models/*.cs` (CompanionDefinition, DialogFile/DialogLine, CompanionPackage)
- `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `IssueCodes.cs` (issues are keyed by **folder name**: `ValidationIssue.Folder`)
- `src/AlbocariusCompanions.Core/Rules/*.cs` (DialogSlots, LineIds, CompanionRules: regexes, ranges, voice types)

## Goal
Implement `MissingAudioReport.Find(IEnumerable<CompanionPackage>)` in `src/AlbocariusCompanions.Core/Audio/MissingAudioReport.cs` (keep `MissingAudioEntry`).
- For every voiced line (in package order, then line order): relative path = `line.Audio` or `LineIds.DefaultAudioPath(line.Id)`; full path = Path.Combine(package.FolderPath, relative path with '/' converted to the OS separator). If the file does not exist → entry (CompanionId = package.Id, LineId, RelativePath with '/' separators, FullPath).
- Unvoiced lines are never reported. Lines without id are skipped. A lipsync .xml does not count as audio.
Files allowed to edit: `src/AlbocariusCompanions.Core/Audio/MissingAudioReport.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/MissingAudioTests.cs` and the helper `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
Read the tests first: they are the precise contract (codes, severities, ordering, messages that must contain a field/slot/key name).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(dotnet 10.0.113 on this machine sometimes aborts with "Internal CLR error" — that is infra, just rerun.)
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
