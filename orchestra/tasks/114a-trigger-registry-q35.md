# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 114a-trigger-registry-q35: trigger registry entries for GAME_MAP Q35

## Goal
Add one `TriggerDefinition` per trigger of GAME_MAP Q35 to `src/AlbocariusCompanions.Core/Triggers/TriggerRegistry.Q35.cs`.
The list of triggers is `orchestra/triggers.tsv`: every row whose column `q` is `Q35` and whose column `name` is not `-`
(25 rows). Columns: event (the CampaignEvents member = `source`), q, line (row of the event in docs/GAME_MAP.md), old, name,
category, common (1 = true), note. Use exactly the tsv `name`, `category` and `common`. Background: the mod lets companions
react to game events (orchestra/SPEC_TRIGGERS_DELIVERY.md T1). This is data only: no game code, no other files.

## Files
- Read: `orchestra/triggers.tsv`, `src/AlbocariusCompanions.Core/Triggers/*.cs` (model + helpers `F`, `S`,
  `MapEventFields` in TriggerRegistry.cs; worked examples in TriggerRegistry.Q35.cs), the `## Q35 — Campaign events catalog 1 (battle, siege, crime, prisoners)` section of
  `docs/GAME_MAP.md` (the row at the tsv `line` gives signature, raise site, filter, notes), and Q23/Q32 rows it refers to.
  `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs` for the exact signature.
- Edit: ONLY `src/AlbocariusCompanions.Core/Triggers/TriggerRegistry.Q35.cs` (keep the three existing entries unchanged).

## How to write an entry
```csharp
new TriggerDefinition("<name>", "<category>", "<one line: what happened, for authors>", "<CampaignEvents member>",
    fields: new[] { F("<field>", FieldKind.<Kind>, "<meaning>", "<from>"), ... },
    subjectFields: S("<field>", ...), defaultSubject: Subjects.<X>, common: <true|false>, inMission: <true|false>),
```
- **fields**: one per useful event argument. Kinds: `Hero`, `Settlement` (also for `Town`/`Village`/`Hideout` args: use
  `.Settlement` in from), `Clan`, `Kingdom` (an `IFaction` arg → `Kingdom` if the GAME_MAP row says it is a kingdom, else
  `Clan`), `Party` (`MobileParty`/`PartyBase`), `Item` (`ItemObject`), `Int`, `Number` (float), `Bool`, `Text` (enums,
  ids, names). A `CharacterObject` that is a hero → `Hero` with from `<arg>.HeroObject`; a troop type → `Text` from
  `<arg>.StringId`. Field names camelCase, short (`victim`, `settlement`, `winner`).
- A `MapEvent` argument → `fields: MapEventFields("<arg name>")` (you may append fields with `.Concat(new[]{...}).ToArray()`,
  add `using System.Linq;`). Rosters, lists and other complex arguments: expose a count or a simple value only if the
  GAME_MAP row names the member that gives it; otherwise leave the argument out.
- Leave out pure UI flags (`showNotification`, `shouldNotify`, `doNotNotify`, `hideInformation`).
- **from**: the argument name, or `<arg>.<Member>` using ONLY members that appear in docs/GAME_MAP.md. Never invent members.
- **subjectFields**: the entity fields (Hero/Party/Settlement/Clan/Kingdom) that say *who/where* the event is about (e.g.
  victim, the raided village, the attacker). Empty when the event has no such field (then defaultSubject must be `Subjects.Any`).
- **defaultSubject**: never `Subjects.Any` when subjectFields is not empty. Hero/party events → `Subjects.PlayerClan` or
  `Subjects.PlayerParty` (or `Subjects.Player` when the event is about the player's own party/hero); settlement events →
  `Subjects.PlayerClan`; clan/kingdom events → `Subjects.Player`. It must be valid for the subject field kinds
  (see `Subjects.KindsFor`).
- **inMission**: true only when the GAME_MAP raise site is inside a mission/scene (battle, tournament, alley fight,
  Mission*/Agent* classes); false for map/campaign code.
- **description**: one plain line for mod authors (what happened, from the player's point of view where it matters).

## Check
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` — in particular
`TriggerRegistryTests` (the theory row for Q35 compares your entries with the tsv; `Every_entry_is_well_formed` checks the rules
above). Do not edit tests or orchestra/.
In your final message list any argument you left out and why.
