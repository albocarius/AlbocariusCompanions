# GAME_MAP — Bannerlord APIs used by AlbocariusCompanions

Every TaleWorlds class/method/field used in mod code must appear here with its source file (SPEC rule 1).
Summaries and signatures only — no copied decompiled method bodies (SPEC rule 2).

Section format (checked by `tests/AlbocariusCompanions.Acceptance/GameMapTests.cs`):

- `Status:` TODO → DRAFT (worker) → VERIFIED (orchestrator)
- `### Answer` plain-language answer to the SPEC question
- `### Evidence` one bullet per claim: ``- `decomp/<path>.cs` — `Class.Member` — what it shows`` (or a `~/bannerlord/...` path for XML/DLL facts)
- `### Uncertain` items marked **UNCERTAIN** with what would resolve them, or `None.`

## Q1 — Wanderer spawning

Status: VERIFIED

### Answer
**Behavior.** Wanderers are spawned by the TaleWorlds behavior `CompanionsCampaignBehavior` (namespace `TaleWorlds.CampaignSystem.CampaignBehaviors`), which the sandbox campaign registers via `SandBoxManager.Initialize`. It is the only behavior in all decompiled sources that mass-spawns wanderer heroes; SandBox issue behaviors only borrow wanderer templates to create quest NPCs (e.g. the "notable wants his daughter found" issue creates one daughter hero from a random wanderer template).

**When it runs.** `RegisterEvents` subscribes to `OnNewGameCreatedEvent`, `OnGameLoadFinishedEvent`, `DailyTickEvent`, `WeeklyTickEvent`, `HeroCreated`, `HeroKilledEvent` and `HeroOccupationChangedEvent`:

- New game (`OnNewGameCreated`): builds the wanderer-template catalog, shuffles `Town.AllTowns` and creates one wanderer per town in a loop whose index stays below the target — i.e. about 0.6 wanderers per town exist at game start.
- Daily (`DailyTick`), in order: `TryKillCompanion` (10 % chance per day: picks a random template from `_aliveCompanionTemplates`, finds the first alive wanderer hero with that `Template`, and removes it via `KillCharacterAction.ApplyByRemove` if it is not hired (`CompanionOf == null`) and not in the player's current settlement), then `SwapCompanions` (moves unrecruited wanderers between random towns — placement only, count unchanged), then `TrySpawnNewCompanion` (at most one new wanderer per day).
- Weekly (`WeeklyTick`): cleanup only — dead/disabled wanderers whose `DeathDay` is ≥ 40 days old (`RemoveWandererAfterDays`) are unregistered via `CampaignObjectManager.UnregisterDeadHero`. No spawning.
- Game load (`OnGameLoadFinished`): rebuilds the alive-wanderer bookkeeping only, never spawns. (A second method `OnGameLoaded(CampaignGameStarter)` with the same body exists but is not subscribed in `RegisterEvents` — dead code.)

**How the number is decided.** The target is derived from towns, not a fixed number: `_desiredTotalCompanionCount` = `Town.AllTowns.Count` × 0.6 (`DesiredCompanionPerTown` = 0.6). A daily spawn happens only when the current count is below that target, and creates at most one hero per day. The destination town is chosen by predicate: not the player's current settlement, no `SiegeEvent`, and no already-unrecruited wanderer in `Settlement.HeroesWithoutParty` (so at most one hireable wanderer per town). `GetCompanionTemplateTypeToSpawn` picks the skill category (Combat, Scouting, Tactics, … each with a fixed weight such as 5/38) whose share of the target is furthest below its weight; `GetCompanionTemplateToSpawn` then shuffles that category and returns the first template that has no living hero. The hero is created with `HeroCreator.CreateSpecialHero(template, townOfTemplateCulture, null, null, age)` (age = coming-of-age + 5 + random 0–11), equipment is re-rolled with the `companion_armor`/`companion_weapon`/`companion_horse` item modifiers (`AdjustEquipments`), and `EnterSettlementAction.ApplyForCharacterOnly` moves the hero into the destination town. The catalog itself is every loaded `CharacterObject` with `IsTemplate` and `Occupation == Occupation.Wanderer` (`InitializeCompanionTemplateList`).

**Exactly where and how existing wanderers are counted.** The spawn decision never counts heroes ad hoc: the behavior maintains a `HashSet` field `_aliveCompanionTemplates` of wanderer *templates* (unique `CharacterObject`s) that currently have a living hero. The exact check is in `TrySpawnNewCompanion`: `_aliveCompanionTemplates.Count < _desiredTotalCompanionCount`. Per-category counting in `GetCompanionTemplateTypeToSpawn` loops the category's templates and tests `_aliveCompanionTemplates.Contains(item)`. On a new game the set is filled only through `OnHeroCreated` as each wanderer is created; on load it is rebuilt by iterating `Hero.AllAliveHeroes` (plus still-alive entries of `Hero.DeadOrDisabledHeroes`) filtered on `IsWanderer` (`OnGameLoadFinished`); it is then maintained by `OnHeroCreated` (adds living wanderers, bypassing the known-template check), `OnHeroKilled` (removes the template; if the dead wanderer was never `HasMet` the hero is immediately unregistered with `CampaignObjectManager.UnregisterDeadHero`) and `OnHeroOccupationChanged` (add/remove when a hero becomes or stops being a wanderer). Except on creation, only templates present in the wanderer catalog are counted (`IsTemplateKnown`). Because counting is per template, vanilla keeps roughly one hero per wanderer template and the population oscillates around the 0.6-per-town target via the daily kill/spawn churn. A "wanderer" here is any hero with `Occupation == Occupation.Wanderer` (`Hero.IsWanderer`); the full classification is Q2.

**Implications for the mod (orchestrator note).** `_aliveCompanionTemplates` drives three things: the spawn target check, template choice, and the daily kill (`TryKillCompanion`). A mod companion created alive with `Occupation.Wanderer` is added by `OnHeroCreated` (template gate bypassed), so it would (a) count toward the target and (b) be eligible for random removal while unhired. Keeping mod companions' templates out of this set (see Q10) solves both. `SwapCompanions` selects by `IsWanderer && CompanionOf == null` directly from settlements, not from the set, so mod companions still get vanilla tavern movement.

### Evidence
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior` — the behavior class itself, holding all spawn/kill/count logic.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/SandBoxManager.cs` — `SandBoxManager.Initialize` — registers the behavior (`gameStarter.AddBehavior(new CompanionsCampaignBehavior())`).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.RegisterEvents` — lists every event it subscribes to: OnNewGameCreatedEvent, OnGameLoadFinishedEvent, HeroKilledEvent, HeroOccupationChangedEvent, HeroCreated, DailyTickEvent, WeeklyTickEvent.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs` — `CampaignEvents.DailyTickEvent` — the campaign tick / lifecycle events (DailyTickEvent, WeeklyTickEvent, OnNewGameCreatedEvent, OnGameLoadFinishedEvent) the behavior listens on.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnNewGameCreated` — on new game shuffles Town.AllTowns and creates one companion per town while index < desired count.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.DailyTick` — daily order: TryKillCompanion, SwapCompanions, TrySpawnNewCompanion.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.TryKillCompanion` — 10 % daily removal of the hero of a random counted template if unhired and not in the player's settlement.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.AddToAliveCompanions` — adds `hero.Template`; `IsTemplateKnown` gate skipped when `isTemplateControlled` is true.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/KillCharacterAction.cs` — `KillCharacterAction.ApplyByRemove` — the kill action used by the daily cull.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.SwapCompanions` — moves unrecruited wanderers (`IsWanderer && CompanionOf == null`) between random towns daily.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.WeeklyTick` — weekly purge of dead wanderers with `DeathDay` older than `RemoveWandererAfterDays` (40).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnGameLoadFinished` — rebuild of the alive set from `Hero.AllAliveHeroes` / `Hero.DeadOrDisabledHeroes` filtered by `IsWanderer`; no spawning on load.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior._desiredTotalCompanionCount` — target companion total = `Town.AllTowns.Count * 0.6f` (constant `DesiredCompanionPerTown`).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Town.cs` — `Town.AllTowns` — the town list the target is derived from.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.TrySpawnNewCompanion` — the exact count check `_aliveCompanionTemplates.Count < _desiredTotalCompanionCount` and the town predicate (not player's settlement, no SiegeEvent, no unrecruited wanderer present); spawns at most one per day.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs` — `Settlement.HeroesWithoutParty` — the hero list the spawn predicate scans for already-present wanderers.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior._aliveCompanionTemplates` — the HashSet of wanderer templates with a living hero; this is what the spawn decision counts.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.AllAliveHeroes` — the hero lists scanned when rebuilding the count at load/new game.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.IsWanderer` — wanderer = `Occupation == Occupation.Wanderer`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroCreated` — adds living wanderers to the set (`isTemplateControlled`, skipping the known-template gate).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroKilled` — removes the template; unregisters never-met dead wanderers immediately.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignObjectManager.cs` — `CampaignObjectManager.UnregisterDeadHero` — full removal of dead wanderer heroes (internal method).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroOccupationChanged` — add/remove when occupation changes to/from `Occupation.Wanderer`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.IsTemplateKnown` — only templates in the wanderer catalog are counted on those maintenance paths.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.InitializeCompanionTemplateList` — catalog = every `CharacterObject` with `IsTemplate` and `Occupation == Occupation.Wanderer`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.GetCompanionTemplateTypeToSpawn` — chooses the skill category whose share (`num2 / _desiredTotalCompanionCount`) is furthest below its weight from `GetTemplateTypeScore`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.GetCompanionTemplateToSpawn` — first shuffled template in that category with no living hero (`!_aliveCompanionTemplates.Contains`).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.CreateCompanionAndAddToSettlement` — creates the hero in a town of the template's culture with age `HeroComesOfAge + 5 + MBRandom.RandomInt(12)`, then enters it into the destination settlement.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/HeroCreator.cs` — `HeroCreator.CreateSpecialHero` — hero-from-template factory used by the spawn.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.AdjustEquipments` — re-rolls gear with the `companion_armor`/`companion_weapon`/`companion_horse` item modifiers.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/EnterSettlementAction.cs` — `EnterSettlementAction.ApplyForCharacterOnly` — moves the created wanderer into its town.
- `decomp/SandBox/SandBox.Issues/NotableWantsDaughterFoundIssueBehavior.cs` — `CharacterHelper.GetRandomCompanionTemplateWithPredicate` — example of an issue behavior borrowing a wanderer template for a quest NPC (not general spawning).
- `~/bannerlord/moduledata/SandBox/companion_strings.xml` — `Petrys` — comments ("companion #0, Petrys") show vanilla named wanderers are module data, i.e. templates like the ones the behavior scans for.

### Uncertain
- **UNCERTAIN**: which vanilla XML file defines the wanderer `CharacterObject` templates (the named companions) — the local `~/bannerlord/moduledata` extract contains no `occupation="Wanderer"` entries (`spnpccharacters.xml` and friends do not list them and there is no `spspecialcharacters.xml`, only its localization file); resolve by grepping a full game install's `ModuleData` for the companion template XML or by dumping `MBObjectManager`'s `CharacterObject` list at runtime.

## Q2 — Which heroes count as wanderers

Status: TODO

### Answer

### Evidence

### Uncertain

## Q3 — Tavern placement and movement

Status: TODO

### Answer

### Evidence

### Uncertain

## Q4 — Hero creation

Status: TODO

### Answer

### Evidence

### Uncertain

## Q5 — Character templates

Status: TODO

### Answer

### Evidence

### Uncertain

## Q6 — Hiring

Status: TODO

### Answer

### Evidence

### Uncertain

## Q7 — Dialog API

Status: TODO

### Answer

### Evidence

### Uncertain

## Q8 — Campaign lifecycle hooks

Status: TODO

### Answer

### Evidence

### Uncertain

## Q9 — Existing saves

Status: TODO

### Answer

### Evidence

### Uncertain

## Q10 — Harmony target for wanderer count

Status: TODO

### Answer

### Evidence

### Uncertain

## Q11 — Generation decision

Status: TODO

### Answer

### Evidence

### Uncertain

## Q12 — Voice-over resolution

Status: TODO

### Answer

### Evidence

### Uncertain

## Q13 — War Sails DLC

Status: TODO

### Answer

### Evidence

### Uncertain

## Q14 — Localization

Status: TODO

### Answer

### Evidence

### Uncertain
