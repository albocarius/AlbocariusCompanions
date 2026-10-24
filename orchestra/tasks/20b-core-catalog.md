# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 20b-core-catalog: Companion catalog

## Context
Read `orchestra/PROJECT.md` and SPEC §2 Phase 2 / §4 in `orchestra/SPEC.md`. Core is netstandard2.0, no TaleWorlds references,
JSON via Newtonsoft.Json (already referenced). Models, issue codes and rule constants already exist — use them, do not duplicate:
- `src/AlbocariusCompanions.Core/Models/*.cs` (CompanionDefinition, DialogFile/DialogLine, CompanionPackage)
- `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `IssueCodes.cs` (issues are keyed by **folder name**: `ValidationIssue.Folder`)
- `src/AlbocariusCompanions.Core/Rules/*.cs` (DialogSlots, LineIds, CompanionRules: regexes, ranges, voice types)

## Goal
Implement `CompanionCatalog.Load(string companionsRoot)` in `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs` (keep `CatalogResult`).
- `CompanionLoader.LoadAll` → `CompanionValidator.Validate(packages)` → registry of the packages whose **FolderName** has no Error-severity issue. Warnings do not skip.
- `Issues` = loader issues followed by validation issues. Never throws for bad data.
Files allowed to edit: `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/CatalogTests.cs` and the helper `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
Read the tests first: they are the precise contract (codes, severities, ordering, messages that must contain a field/slot/key name).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(dotnet 10.0.113 on this machine sometimes aborts with "Internal CLR error" — that is infra, just rerun.)
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
