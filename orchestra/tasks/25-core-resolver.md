# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 25-core-resolver: resolve companion ids against the running game (SPEC rule 9)

## Context
Read `orchestra/PROJECT.md`, SPEC rule 9 in `orchestra/SPEC.md`, GAME_MAP Q5 (fixed items, civilian rosters) and Q13 (missing ids
return null). The game is reached only through `src/AlbocariusCompanions.Core/Resolution/IGameObjectLookup.cs` (already defined; the Game
layer implements it later). Output type `ResolvedCompanion` (Core/Templates) and issue codes (Core/Validation/IssueCodes.cs) exist.
Input packages have already passed validation (required fields present), but treat null skills/traits/sets as empty and never throw.

## Goal
Implement `CompanionResolver.Resolve(CompanionPackage, IGameObjectLookup)` in `src/AlbocariusCompanions.Core/Resolution/CompanionResolver.cs`
(keep `ResolutionResult` and the signature). All issues use `Folder = package.FolderName`.
- Culture: culture exists → use. Else cultureFallback exists → use it + WARNING `CultureFallbackUsed` (message names the missing id).
  Else ERROR `CultureMissing` and `Companion = null` (skip this companion only).
- Battle roster: equipment.templateId exists → use. Else fallbackTemplateId exists → use + WARNING `EquipmentFallbackUsed`.
  Else ERROR `EquipmentMissing`, `Companion = null`.
- Civilian roster: `<resolved battle roster id>_civilian` (`CivilianRosterSuffix`) if that roster exists, else null (no issue).
- Skills / traits: keep existing ids; each missing id → WARNING `SkillMissing` / `TraitMissing` (message names the id), dropped.
- Fixed sets (equipment.battle / equipment.civilian): per entry, item missing → WARNING `ItemMissing`; exists but does not fit the slot
  → WARNING `ItemDoesNotFitSlot` (messages name the item id); dropped. If a set was given but nothing remains → that set is null and
  one WARNING `FixedSetEmpty` (after the item warnings), so the roster import is used. Sets not given → null.
- Copy id, name, isFemale, voice, age, bodyProperties straight from the definition.
- Exactly the issues the tests expect (no extra info issues).
Files allowed to edit: `src/AlbocariusCompanions.Core/Resolution/CompanionResolver.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/ResolverTests.cs` and `Core/Fixtures.cs` — read them first; they are the contract.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
