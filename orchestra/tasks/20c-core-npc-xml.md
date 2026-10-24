# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 20c-core-npc-xml: NPCCharacters template XML generator

## Context
Read `orchestra/PROJECT.md`, SPEC rule 8 and its amendment in `orchestra/SPEC.md`, and GAME_MAP Q5 + Q11 in `docs/GAME_MAP.md`.
Mod companion templates are generated at runtime from JSON as an `NPCCharacters` XML document and loaded by the game.
This task builds only the pure generator in Core (netstandard2.0, no TaleWorlds references). Input type already exists:
`src/AlbocariusCompanions.Core/Templates/ResolvedCompanion.cs` (do not change it).

## Goal
Implement `NpcCharacterXml.Build(IEnumerable<ResolvedCompanion>)` in `src/AlbocariusCompanions.Core/Templates/NpcCharacterXml.cs` (keep the signature):
- Root `<NPCCharacters>`, one `<NPCCharacter>` per companion in input order. Duplicate ids → `ArgumentException`.
- Attributes: `id`, `name="{=<id>_name}<name>"`, `is_template="true"`, `occupation="Special"`, `culture="Culture.<cultureId>"`,
  `voice`, `age`, `is_female` (`true`/`false`). Numbers with invariant culture.
- `<face><BodyProperties version="4" key="..."/></face>` only when BodyPropertiesKey is not null.
- `<skills><skill id value/></skills>` and `<Traits><Trait id value/></Traits>`, sorted by id (ordinal); omit the element when empty.
- `<Equipments>`: `<EquipmentSet id="<battle roster>"/>` (no equipmentType attribute), then, if CivilianEquipmentRosterId is not null,
  `<EquipmentSet id="<civilian roster>" equipmentType="Civilian"/>`.
- Fixed items: if BattleItems is not null, write `<EquipmentRoster>` with one `<equipment slot="<slot>" id="Item.<item id>"/>` per entry
  (slots ordered as in `CompanionRules.EquipmentSlots`) INSTEAD of the battle `<EquipmentSet>`. If CivilianItems is not null, write
  `<EquipmentRoster civilian="true">` the same way INSTEAD of the civilian `<EquipmentSet>`. Inside `<Equipments>`: battle first, then civilian.
- Element order inside NPCCharacter: face, skills, Traits, Equipments. The document must validate against `xmlschemas/NPCCharacters.xsd`.
Files allowed to edit: `src/AlbocariusCompanions.Core/Templates/NpcCharacterXml.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/NpcCharacterXmlTests.cs` — read it first; it is the contract.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
