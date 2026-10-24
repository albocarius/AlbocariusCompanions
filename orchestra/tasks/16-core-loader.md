# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 16-core-loader: Companion folder loader

## Context
Read `orchestra/PROJECT.md` and SPEC §2 Phase 2 / §4 in `orchestra/SPEC.md`. Core is netstandard2.0, no TaleWorlds references,
JSON via Newtonsoft.Json (already referenced). Models, issue codes and rule constants already exist — use them, do not duplicate:
- `src/AlbocariusCompanions.Core/Models/*.cs` (CompanionDefinition, DialogFile/DialogLine, CompanionPackage)
- `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `IssueCodes.cs` (issues are keyed by **folder name**: `ValidationIssue.Folder`)
- `src/AlbocariusCompanions.Core/Rules/*.cs` (DialogSlots, LineIds, CompanionRules: regexes, ranges, voice types)

## Goal
Implement `CompanionLoader.LoadAll(string companionsRoot)` in `src/AlbocariusCompanions.Core/Loading/CompanionLoader.cs` (keep the public signature and the constants).
- Root missing → no packages, one `RootNotFound` error (Folder = ""). Never throw for bad data.
- Every direct subdirectory is one companion folder; files in the root are ignored. Order packages by folder name (ordinal).
- Read `companion.json` and `dialogs.json` (UTF-8). Missing file → `MissingCompanionFile` / `MissingDialogsFile` error, skip folder.
- Parse strictly with Newtonsoft: `MissingMemberHandling.Error` (unknown properties are errors — catches typos), type mismatches are errors,
  a literal `null` document is an error. Any of these → one `InvalidJson` error whose message contains the file name; skip folder.
- `dialogs.json` without `lines` → `Lines` = empty list (not null).
- The loader does NOT validate rules (no id/voice/range checks) — that is the validator's job.
Files allowed to edit: `src/AlbocariusCompanions.Core/Loading/**`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/LoaderTests.cs` and the helper `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
Read the tests first: they are the precise contract (codes, severities, ordering, messages that must contain a field/slot/key name).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(dotnet 10.0.113 on this machine sometimes aborts with "Internal CLR error" — that is infra, just rerun.)
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
