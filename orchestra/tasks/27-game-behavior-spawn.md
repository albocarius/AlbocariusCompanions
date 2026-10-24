# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 27-game-behavior-spawn: spawn, persist and re-link mod companions

## Shared rules for this task
- Game layer: `src/AlbocariusCompanions.Game` (net472, compiled against the real game DLLs). There is no way to run the game here:
  the check is the build plus the acceptance tests (incl. `ApiIndexTests`). The orchestrator reviews every API call.
- **Every TaleWorlds type/member you use must have a row in the `## API index` table at the end of `docs/GAME_MAP.md`**
  (format: `| `Type.Member` | `decomp/<path>.cs` | Q<n> | used for |`, the Q must be a VERIFIED section). The APIs listed below are
  already verified in the decomp; add the missing rows yourself. Do not use any API that is not listed here or already in the index.
- Every delegate/handler the game calls must be wrapped with `Safe.Run(ModRuntime.Log, "<context>", ...)` (GAME_MAP Q15: the game
  catches nothing). Conditions use the `Safe.Run<bool>(..., fallback: false)` overload.
- Log meaningful events with `ModRuntime.Log.Info/Warning/Error`. Read `ModRuntime.cs`, `SubModule.cs`, `CompanionHeroFactory.cs` first.
- Keep the existing style (file-scoped namespaces, `internal`, XML doc summaries on types).

## Goal
Fill `src/AlbocariusCompanions.Game/AlbocariusCompanionsCampaignBehavior.cs` (keep class name, BehaviorId and the base(BehaviorId) ctor).
- Field `private Dictionary<string, Hero> _companionHeroes = new Dictionary<string, Hero>();` (companion id → hero).
- `SyncData`: `dataStore.SyncData("_companionHeroes", ref _companionHeroes);` then replace a null dictionary with an empty one
  (GAME_MAP Q8: `Dictionary<string, Hero>` is a registered saveable container; Q9: on an old save SyncData is never called).
- `RegisterEvents`: `CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated)` (Action<CampaignGameStarter>),
  `CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished)` (Action),
  `CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)` (Action<CampaignGameStarter>) which calls
  `CompanionDialogs.Register(starter)`. Each handler body inside Safe.Run.
- `OnNewGameCreated` and `OnGameLoadFinished` both call `SpawnMissing(string reason)`:
  for each `(id, template)` in `ModRuntime.Templates` ordered by id (ordinal), each in its own Safe.Run so one failure never stops the others:
  1. map has a non-null hero for id → log "already present" (alive or dead — never respawn, never duplicate), continue.
  2. else search `Hero.AllAliveHeroes` then `Hero.DeadOrDisabledHeroes` for a hero whose `Template?.StringId == id` → store it in the map,
     log "re-linked", continue (save lost the map / mod data removed and re-added).
  3. else `ModRuntime.Registry.TryGet(id, out package)`; pick a town (below); `var hero = CompanionHeroFactory.Create(template, package.Companion, town.Settlement)`;
     `EnterSettlementAction.ApplyForCharacterOnly(hero, town.Settlement)` (GAME_MAP Q3); store in the map; log id, hero StringId and settlement StringId.
- Town choice like vanilla (GAME_MAP Q1/Q3): candidates = `Town.AllTowns` whose `Settlement != Hero.MainHero?.CurrentSettlement`;
  prefer towns with `town.Culture == template.Culture`; otherwise any candidate; pick with `MBRandom.RandomInt(count)`. No town → log an error, skip.
- At the end log a one-line summary (spawned / present / re-linked counts and the reason).

## Verified APIs you may use (add missing API-index rows)
`CampaignEvents.OnNewGameCreatedEvent`, `CampaignEvents.OnGameLoadFinishedEvent`, `CampaignEvents.OnSessionLaunchedEvent` (Q8; decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs),
`IMbEvent.AddNonSerializedListener` (Q8; .../TaleWorlds.CampaignSystem/IMbEvent.cs), `IDataStore.SyncData` (Q8),
`Hero.AllAliveHeroes`, `Hero.DeadOrDisabledHeroes`, `Hero.Template`, `Hero.MainHero`, `Hero.CurrentSettlement`, `Hero.StringId` via MBObjectBase (Q1/Q2/Q3),
`Town.AllTowns` (Q1; .../TaleWorlds.CampaignSystem.Settlements/Town.cs), `Town.Culture` (Q1), `SettlementComponent.Settlement` (Q3; .../TaleWorlds.CampaignSystem.Settlements/SettlementComponent.cs),
`CharacterObject.Culture` (Q5), `EnterSettlementAction.ApplyForCharacterOnly` (Q3; .../TaleWorlds.CampaignSystem.Actions/EnterSettlementAction.cs),
`MBRandom.RandomInt` (Q1; decomp/TaleWorlds.Core/TaleWorlds.Core/MBRandom.cs).
Files allowed to edit: `src/AlbocariusCompanions.Game/AlbocariusCompanionsCampaignBehavior.cs`, `docs/GAME_MAP.md` (API index rows only).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
