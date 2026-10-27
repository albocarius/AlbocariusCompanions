# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 122e-game-adapters-q39: game event adapters for the GAME_MAP Q39 triggers

## Goal
For every trigger of `src/AlbocariusCompanions.Core/Triggers/TriggerRegistry.Q39.cs` (29 triggers), add one adapter entry to
`src/AlbocariusCompanions.Game/TriggerAdapters.Q39.cs`. An adapter listens on the trigger's `CampaignEvents` member
(`Source` in the registry entry) and builds the payload with exactly the registry entry's field names, using the field's
`from` text as the guide for which argument/member fills it. Background: orchestra/SPEC_TRIGGERS_DELIVERY.md T1/T5.

## Files
- Read: `src/AlbocariusCompanions.Core/Triggers/TriggerRegistry.Q39.cs` (the entries), `src/AlbocariusCompanions.Core/Triggers/TriggerRegistry.cs`
  (`MapEventFields`), `src/AlbocariusCompanions.Game/TriggerAdapters.cs`, `TriggerAdapters.Q35.cs` (worked examples),
  `src/AlbocariusCompanions.Game/TriggerPayloads.cs` (builders), `docs/GAME_MAP.md` (the Q39 section and the `## API index`),
  `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs` (exact `IMbEvent<...>` signature of each member)
  and the decomp class of any member a `from` expression uses.
- Edit: ONLY `src/AlbocariusCompanions.Game/TriggerAdapters.Q39.cs`, and `docs/GAME_MAP.md` ONLY by appending rows at the very
  end of the `## API index` table (the last section of the file).

## How to write an adapter
```csharp
d["<name>"] = (owner, fire) => CampaignEvents.<Source>.AddNonSerializedListener(owner,
    (<one parameter per IMbEvent type argument; _ / _1 for unused>) => Emit(fire, "<name>", () => Of(("<field>", <Builder>(<value>)), ...)));
```
- One entry per trigger, written exactly in that shape (a test parses `d["name"] = (owner, fire) => CampaignEvents.X.AddNonSerializedListener(owner,`).
- The lambda must take as many parameters as the event's `IMbEvent<...>` has type arguments (`IMbEvent` without arguments → `() =>`).
- Builders (`TriggerPayloads`, already imported with `using static`): Hero field → `Hero(x)` (Hero or CharacterObject), Settlement →
  `Settlement(x)` (Settlement, Town or Village), Clan → `Clan(x)`, Kingdom → `Kingdom(x)`, Faction → `Faction(x)` (IFaction),
  Party → `Party(x)` (MobileParty or PartyBase), Item → `Item(x)` (ItemObject), Int → `Int(x)`, Number → `Number(x)`,
  Bool → `Bool(x)`, Text → `Text(x)` (enums and ids; use `x?.StringId` for an object id).
- An entry whose fields are `MapEventFields("<arg>")` → `Of(MapEvent(<the MapEvent>))`; with extra fields:
  `Of(MapEvent(m).Concat(new (string, PayloadValue?)[] { ("winnerSide", Text(winnerSide)) }).ToArray())` (add
  `using AlbocariusCompanions.Core.Triggers;` for PayloadValue).
- A trigger with no fields → `Of()`.
- Use only members listed in the GAME_MAP API index. For each member you use that is not listed yet (every
  `CampaignEvents.<Source>` you subscribe to, and members such as `Army.LeaderParty`), append one row:
  `| \`Type.Member\` | \`decomp/<path of the file that declares it>\` | Q39 | <trigger name> trigger (M11) |` — the member name must
  appear in that file (ApiIndexTests checks it). Never invent members; if a `from` member does not exist, leave that
  field out and say so in your final message.
- Add `using` lines for the game namespaces you need (e.g. `TaleWorlds.CampaignSystem.Settlements`). No other code.

## Check
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` — in particular
`TriggerAdapterBatchTests` (row Q39: every trigger of the batch has an adapter), `TriggerGameWiringTests` (entry shape,
source = registry, field names = registry fields) and `ApiIndexTests`. The Game project builds against the real game DLLs,
so a wrong lambda shape or member is a compile error. Do not edit tests or orchestra/.
