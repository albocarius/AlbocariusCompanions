# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 35-required-modules-core: requiredModules replaces the fallback fields

## Context
Read `orchestra/PROJECT.md`, `orchestra/SPEC_CORE_GAPS.md` (§1 rule 1 and T2.3) and `docs/SCHEMA.md`.
Core is netstandard2.0, no TaleWorlds references. Existing code to change, not duplicate:
- `src/AlbocariusCompanions.Core/Models/CompanionDefinition.cs` (`CultureFallback`, `EquipmentDefinition.FallbackTemplateId`)
- `src/AlbocariusCompanions.Core/Validation/CompanionChecks.cs`, `IssueCodes.cs`
- `src/AlbocariusCompanions.Core/Resolution/CompanionResolver.cs` (`ResolveCulture`, equipment roster resolution)
The game side (checking which modules are active in the running game) is NOT part of this task; Core only gets a predicate.

## Goal
1. **Remove the fallbacks** everywhere in Core: `cultureFallback` and `equipment.fallbackTemplateId` from the models (so the
   strict loader rejects them as unknown members → `InvalidJson`), their validation (`SameAsFallback`), and the resolver
   fallback branches. Delete the issue codes `CultureFallbackUsed`, `EquipmentFallbackUsed`, `SameAsFallback`. The resolver
   keeps its current behavior otherwise: a missing culture → `CultureMissing` error and the companion is skipped; a missing
   equipment roster → `EquipmentMissing` error and skipped. Update the messages (no mention of a fallback any more) and the
   doc comments that talk about fallbacks for cultures/rosters.
2. **Add `requiredModules`**: `[JsonProperty("requiredModules")] public List<string>? RequiredModules` on `CompanionDefinition`
   (optional; absent or `[]` is fine). Validation in `CompanionChecks`: a blank entry (empty/whitespace) or a duplicate entry
   (ordinal) → error with the new code `InvalidRequiredModule = "invalid_required_module"`; message contains `requiredModules`
   and, for duplicates, the module id. One error per bad entry.
3. New file `src/AlbocariusCompanions.Core/Validation/RequiredModules.cs`, namespace `AlbocariusCompanions.Core.Validation`:
   `public static class RequiredModules { public static IReadOnlyList<string> Missing(CompanionPackage package, Func<string, bool> isModuleActive) }`
   → the declared module ids for which the predicate is false, in declared order (empty when none are required). Never throws
   for a null list.
4. Data and docs: remove `cultureFallback` and `fallbackTemplateId` from `module/ModuleData/Companions/test_companion/companion.json`
   (do not add `requiredModules` there: the placeholder needs no module). `docs/SCHEMA.md`: remove both rows, add the
   `requiredModules` row (string array, optional, module ids as in the module's SubModule.xml `<Id>`; companion is skipped
   when a listed module is not loaded; example `["NavalDLC"]`).
5. Update the unit tests in `tests/AlbocariusCompanions.Core.Tests/**` that use the removed fields/codes (RulesTests,
   CompanionResolverTests, CompanionChecksTests): delete fallback tests, keep the rest green.

## Files allowed to edit
`src/AlbocariusCompanions.Core/Models/CompanionDefinition.cs`, `src/AlbocariusCompanions.Core/Validation/**`,
`src/AlbocariusCompanions.Core/Resolution/**`, `src/AlbocariusCompanions.Core/Templates/ResolvedCompanion.cs` (doc comment only),
`module/ModuleData/Companions/test_companion/companion.json`, `docs/SCHEMA.md`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/RequiredModulesTests.cs`, plus the updated `ResolverTests.cs`,
`CompanionValidationTests.cs` and `Fixtures.cs` (no fallback fields any more). All other acceptance tests must stay green.
Read the tests first: they are the precise contract.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
