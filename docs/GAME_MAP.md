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

Status: VERIFIED

### Answer

**Short answer.** A hero is a wanderer iff its `Occupation` is `Occupation.Wanderer` — that is the whole test (`Hero.IsWanderer => Occupation == Occupation.Wanderer`). There is no wanderer flag, no tag list and no "hired" flag involved in the definition. But the *count* from Q1 (`_aliveCompanionTemplates.Count`, compared with `Town.AllTowns.Count * 0.6f`) is **not a hero count**: it is a `HashSet<CharacterObject>` of hero **templates**, so several living heroes that were made from the same character template occupy exactly **one** slot, and one hero dying (or changing occupation) frees that slot even if other heroes from the same template are still alive.

**Occupation (the only criterion).** A hero's occupation is copied from its character template at creation (`Hero.SetInitialValuesFromCharacter` assigns `Occupation = CharacterObject.GetDefaultOccupation()`), and the template's occupation comes straight from the `occupation` attribute of the character XML (`CharacterObject.Deserialize`). After creation the only way to become or stop being a wanderer is `Hero.SetNewOccupation`, which raises `HeroOccupationChangedEvent`; `CompanionsCampaignBehavior.OnHeroOccupationChanged` removes the hero from the count when the old occupation was `Wanderer` and adds it when the new one is. So the set follows occupation, always, in both directions.

**Flags.** The only other flag the counting code ever looks at is `CharacterObject.IsTemplate`, and only to build the "known wanderer template" catalog (`InitializeCompanionTemplateList` = every registered `CharacterObject` with `IsTemplate && Occupation == Occupation.Wanderer`, from `MBObjectManager.GetObjectTypeList<CharacterObject>()`). Nothing else qualifies: `Hero.CompanionOf` is hire state, not identity; `Hero.IsSpecial` is a different occupation; `CharacterObject.HiddenInEncyclopedia` is display-only. The game defines "wanderer template" the same way in a second, independent place (`CharacterHelper.GetRandomCompanionTemplateWithPredicate` uses the identical predicate), and the conversation layer classifies by the same hero property (`WandererTag.IsApplicableTo => HeroObject.IsWanderer`) — so there is no second notion of wanderer anywhere.

**Who is added / removed.**
- New game / save load: `OnGameLoadFinished` walks `Hero.AllAliveHeroes` and adds `hero.Template` for every `IsWanderer` hero, then walks `Hero.DeadOrDisabledHeroes` and adds those with `IsAlive && IsWanderer`. Both go through `AddToAliveCompanions`, whose `IsTemplateKnown` gate is skipped only on the creation path.
- Runtime: `OnHeroCreated` adds the hero when `hero.IsAlive && hero.IsWanderer` (gate bypassed via `isTemplateControlled: true`); `OnHeroOccupationChanged` moves it in/out; `OnHeroKilled` calls `RemoveFromAliveCompanions(victim)` **for every killed hero, with no occupation guard** — so a non-wanderer hero that happens to share a wanderer template also frees the slot.
- The set is never saved (`SyncData` is empty), it is rebuilt from the hero rosters on every load. Anything a mod does to wanderer identity must therefore be visible to those same filters at load time, not only at runtime.

**Hired vs unhired: irrelevant to the count.** Hiring is `AddCompanionAction.ApplyInternal` → `companion.CompanionOf = clan` (plus `AddHeroToPartyAction` in the dialog consequence) and never touches `Occupation`, so a hired companion keeps `IsWanderer` and its template keeps holding a slot. Firing (`RemoveCompanionAction.ApplyInternal`) makes the hero fugitive and resets equipment but keeps `Occupation.Wanderer`, so ex-companions keep counting too. Hire state appears only in the *downstream* filters, never in the count itself: the daily cull requires the picked hero to have `CompanionOf == null` and a settlement different from the player's (if the first alive hero of that template is hired, the cull just does not happen that day); `SwapCompanions` moves only `IsWanderer && CompanionOf == null` heroes taken from `Settlement.HeroesWithoutParty`; and the spawn predicate treats a town as "already has a wanderer" only for unhired ones (`!y.IsWanderer || y.CompanionOf != null`). Separately, the player's own companion cap (`too_many_companions()` → `Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit`) is an unrelated number and is not part of this count.

**Alive.** "Alive" here means membership in `Hero.AllAliveHeroes` (= `CampaignObjectManager`'s alive roster). A hero is in that roster unless its `HeroState` is `Dead` or `Disabled` (`CampaignObjectManager.OnHeroAdded` / `HeroStateChanged` shuffle heroes between the two lists), so `NotSpawned`, `Active`, `Fugitive`, `Prisoner`, `Released` and `Traveling` wanderers are all counted. Consequences: a wanderer is counted from the moment it is created (`CreateSpecialHero` fires `OnHeroCreated` while the hero is still `NotSpawned`); a fired, fugitive companion is still counted; a wanderer removed by the cull leaves both lists (`KillCharacterAction.MakeDead` sets `CharacterStates.Dead` before `HeroKilledEvent` fires) and can never come back, because dead wanderers are unregistered from the campaign entirely (`OnHeroKilled` for never-met ones, `WeeklyTick` after 40 days, via `CampaignObjectManager.UnregisterDeadHero`). Note `Hero.IsAlive` is literally `!IsDead`, which is why the load pass also scans `DeadOrDisabledHeroes` for `IsAlive && IsWanderer` — a `Disabled` hero would be counted there; in the decompiled code the only callers of `DisableHeroAction.Apply` are StoryMode scripted-hero cleanups, so in sandbox the practical ways out of the count are dying and changing occupation.

**Not checked anywhere:** age/child, clan (the player's clan, an AI lord's hired companions travelling in a party — all counted), culture, gender, `HasMet` (only affects how fast a dead wanderer is unregistered), settlement or party membership, and whether the hero is reachable/visible in the encyclopedia.

**Consequence for the mod.** Any alive hero whose `Occupation` is `Wanderer` is indistinguishable from a vanilla named companion to this logic — even one created for a quest. The "notable wants his daughter found" quest creates the daughter with `HeroCreator.CreateSpecialHero` from a template fetched by `GetRandomCompanionTemplateWithPredicate`, and hero initialization keeps the template's occupation unless the caller overrides it (`HeroCreator.InitializeHeroFromSettings`), so she is `IsWanderer`: she consumes a wanderer slot and is eligible for the daily cull because `CompanionOf` is null. A mod companion with `Occupation.Wanderer` behaves the same way unless it is excluded (Q10).

**Orchestrator note (verified).** `Hero.Template` is `CharacterObject.OriginalCharacter`, i.e. the private `_originCharacter`; it is null when the hero's `CharacterObject` was not derived from another character (`IsOriginalCharacter`). Heroes made by `HeroCreator.CreateSpecialHero` from a template are derived, so `Template` is that template object — this is the key the wanderer set uses and the natural key for excluding mod companions (Q10).

### Evidence

- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.IsWanderer` — the single hero-level wanderer test: `Occupation == Occupation.Wanderer`; no dedicated flag exists.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Occupation.cs` — `Occupation.Wanderer` — the enum member that defines wanderer-hood (no other wanderer-like occupation value).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.Occupation` — saveable property with a private setter; only written by `Hero.SetNewOccupation`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.SetNewOccupation` — the only runtime way to gain/lose wanderer-hood; dispatches the occupation-changed event the counter listens to.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.SetInitialValuesFromCharacter` — a new hero takes `Occupation = CharacterObject.GetDefaultOccupation()`, i.e. wanderer-hood is inherited from the character template.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs` — `CharacterObject.Deserialize` — reads the `occupation` attribute (enum parse) and the `is_template` attribute from the character XML.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs` — `CharacterObject.IsTemplate` — the only extra flag the counting logic consults; marks a character row as a spawn template rather than a hero instance.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs` — `CharacterObject.OriginalCharacter` — the root template of a character; this is what the count stores.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs` — `CharacterObject.CreateFrom` — every hero made from one template keeps the same origin character, so several heroes collapse into one counted slot.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.Template` — returns the origin `CharacterObject`; the value added to and removed from the counted set.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs` — `CharacterObject.IsOriginalCharacter` — true when `_originCharacter` is null, in which case `Hero.Template` is null.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior._aliveCompanionTemplates` — the counted collection: a `HashSet<CharacterObject>` of templates, not heroes.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.InitializeCompanionTemplateList` — catalog = every `CharacterObject` with `IsTemplate && Occupation == Occupation.Wanderer`; nothing else is consulted.
- `decomp/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs` — `MBObjectManager.GetObjectTypeList` — the catalog source: all `CharacterObject`s registered by the loaded modules (mod XML included).
- `decomp/TaleWorlds.CampaignSystem/Helpers/CharacterHelper.cs` — `CharacterHelper.GetRandomCompanionTemplateWithPredicate` — independent second definition of "wanderer template" with the identical predicate, confirming occupation+is_template is the game-wide rule.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnGameLoadFinished` — rebuild on load from `Hero.AllAliveHeroes` filtered only by `IsWanderer`, plus `Hero.DeadOrDisabledHeroes` filtered by `IsAlive && IsWanderer`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.AddToAliveCompanions` — stores `companion.Template`; the `IsTemplateKnown` gate is applied except when `isTemplateControlled` is set (creation path).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroCreated` — counts a hero at creation on `IsAlive && IsWanderer`; no hire, age, clan or location condition.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/HeroCreator.cs` — `HeroCreator.InitializeHeroFromSettings` — occupation is only overridden if the caller passes a different one (the args default to the hero's template occupation), so a wanderer-template NPC stays a wanderer.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroOccupationChanged` — removes on leaving `Wanderer`, adds on becoming `Wanderer`; occupation is the only non-lethal switch in/out of the count.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.OnHeroKilled` — `RemoveFromAliveCompanions(victim)` runs for every killed hero, with no `IsWanderer` guard.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.AllAliveHeroes` — the roster the load-time rebuild iterates.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignObjectManager.cs` — `CampaignObjectManager.OnHeroAdded` — a hero is in the alive list unless its state is `Dead` or `Disabled`, so `NotSpawned`/`Fugitive`/`Prisoner`/`Traveling` wanderers all count.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignObjectManager.cs` — `CampaignObjectManager.HeroStateChanged` — moves heroes between the alive and dead-or-disabled lists when a state changes.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.CharacterStates` — the state enum (`NotSpawned`, `Active`, `Fugitive`, `Prisoner`, `Released`, `Dead`, `Disabled`, `Traveling`) the alive/dead split is based on.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.IsAlive` — literally `!IsDead`, which is why the load pass separately scans `Hero.DeadOrDisabledHeroes` for `IsAlive` wanderers.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/KillCharacterAction.cs` — `KillCharacterAction.MakeDead` — sets `CharacterStates.Dead` (and `DeathDay`) before the killed event fires, so a dead wanderer leaves both the roster and the set.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignObjectManager.cs` — `CampaignObjectManager.UnregisterDeadHero` — final removal of a dead wanderer from the campaign (never-met on kill, `WeeklyTick` after 40 days), so it can never re-enter the count.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.SyncData` — empty: the counted set is not persisted and is rebuilt from hero rosters on every load.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/AddCompanionAction.cs` — `AddCompanionAction.ApplyInternal` — hiring only sets `companion.CompanionOf = clan`; occupation is untouched, so hired wanderers keep counting.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs` — `LordConversationsCampaignBehavior.conversation_companion_hire_on_consequence` — the player hire path (`AddCompanionAction` + `AddHeroToPartyAction`); no occupation change anywhere.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/RemoveCompanionAction.cs` — `RemoveCompanionAction.ApplyInternal` — a fired companion is made fugitive and equipped as a civilian but stays `IsWanderer`, so ex-companions still occupy a counted slot.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.TryKillCompanion` — hire state is only a downstream filter: first alive hero with that `Template` and `IsWanderer`, then `CompanionOf == null` and a settlement other than the player's; a hired hero means no cull that day.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.SwapCompanions` — daily movement only considers `IsWanderer && CompanionOf == null` heroes.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs` — `Settlement.HeroesWithoutParty` — the roster those movement/spawn filters walk; heroes travelling with a party are not in it.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.TrySpawnNewCompanion` — town predicate `!y.IsWanderer || y.CompanionOf != null`: only unhired wanderers block a new spawn in that town.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/LordConversationsCampaignBehavior.cs` — `LordConversationsCampaignBehavior.too_many_companions` — the player cap (`Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit`) is a different count entirely.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Clan.cs` — `Clan.CompanionLimit` — the clan-based companion cap, unrelated to the wanderer population count.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Conversation.Tags/WandererTag.cs` — `WandererTag.IsApplicableTo` — the dialog layer also classifies wanderers with the same `IsWanderer` property; no separate "hireable" flag exists there either.
- `decomp/SandBox/SandBox.Issues/NotableWantsDaughterFoundIssueBehavior.cs` — `HeroCreator.CreateSpecialHero` — a quest NPC built from a wanderer template with no occupation override: she is `IsWanderer` and takes a counted slot exactly like a real companion.
- `decomp/StoryMode/StoryMode.GameComponents.CampaignBehaviors/TutorialPhaseCampaignBehavior.cs` — `DisableHeroAction.Apply` — the only callers of the `Disabled` state in the decompiled sources are StoryMode scripted-hero cleanups, so in sandbox death and occupation change are the practical exits from the count.
- `~/bannerlord/moduledata/SandBoxCore/spnpccharactertemplates.xml` — `is_template` — vanilla character XML really does carry `is_template="true"` and `occupation="…"` per character element (none of the locally present ones say `Wanderer`).

### Uncertain

- **UNCERTAIN**: which module/XML defines the vanilla named-companion (wanderer) templates with `occupation="Wanderer"` and `is_template="true"` — the local `~/bannerlord/moduledata` extract (SandBoxCore, SandBox) contains no `occupation="Wanderer"` anywhere and has no companion-template file (no `spspecialcharacters.xml`, only its localization file), and `~/bannerlord/dlls/**` contains no ModuleData XML; resolve by reading a full install's `Modules/SandBoxCore/ModuleData/spspecialcharacters.xml` or by dumping `MBObjectManager.GetObjectTypeList<CharacterObject>()` at runtime.
- **UNCERTAIN**: whether a mod-generated character XML with `is_template="true" occupation="Wanderer"` really lands in the catalog built by `InitializeCompanionTemplateList` at load time (mod module load order vs. that call) — nothing here proves the ordering; resolve by logging `_aliveCompanionTemplates.Count` with a mod companion present, or by the Q10 patch test in Phase 4.
- **UNCERTAIN**: how many other vanilla paths create heroes from wanderer templates and thereby consume counted slots — only the "notable wants his daughter found" quest was traced here; resolve by auditing every `HeroCreator.CreateSpecialHero` call site that receives a template from `GetRandomCompanionTemplateWithPredicate` (e.g. the tutor and family-feud issues) and checking whether any of them calls `SetNewOccupation`.

## Q3 — Tavern placement and movement

Status: VERIFIED

### Answer
**The placement state itself.** A hero's settlement presence without a party is one property: `Hero.StayingInSettlement`. Its setter is the whole bookkeeping — it removes the hero from the old settlement's hero-without-party cache (`Settlement.RemoveHeroWithoutParty`) and adds it to the new one (`Settlement.AddHeroWithoutParty`), which is what `Settlement.HeroesWithoutParty` exposes. `Hero.CurrentSettlement` falls back to it only when the hero belongs to no party and no prisoner party. Exactly one write in the decompiled sources sets it non-null: `EnterSettlementAction.ApplyForCharacterOnly(hero, settlement)`; it is cleared by `LeaveSettlementAction.ApplyForCharacterOnly(hero)`, by joining a party (`AddHeroToPartyAction` sets it to null, as does the internal `Hero.OnAddedToParty`), and by capture/disable paths (`TakePrisonerAction`, `KillCharacterAction`, `DisableHeroAction`, `EndCaptivityAction`). So "which settlement a wanderer is in" is not a coordinate or a party — it is this one reference, maintained by two action APIs.

**Initial placement.** Every wanderer the game spawns ends up in a town through `CompanionsCampaignBehavior`: at new game one per (shuffled) town, daily at most one more (`CreateCompanionAndAddToSettlement`). That method creates the hero with `HeroCreator.CreateSpecialHero(template, cultureMatchedRandomTown, null, null, age)` — the culture-matched town is only the hero's `BornSettlement`, creation does not place the hero anywhere — then `hero.ChangeState(Hero.CharacterStates.Active)` and finally `EnterSettlementAction.ApplyForCharacterOnly(hero, settlement)` puts the hero into the caller-chosen town (daily spawns pick a random town that is not the player's current settlement, has no `SiegeEvent`, and has no still-unhired wanderer in `HeroesWithoutParty`).

**Movement between settlements over time.** One mechanic, one cadence: `CompanionsCampaignBehavior.SwapCompanions`, called from `DailyTick` after `TryKillCompanion` and before `TrySpawnNewCompanion`. It splits `Town.AllTowns` into two halves by index; it takes one random hireable wanderer (`IsWanderer && CompanionOf == null`) from `HeroesWithoutParty` of a random town in the first half, then picks a random town in the second half: if that town also has a hireable wanderer, the two wanderers are removed with `LeaveSettlementAction.ApplyForCharacterOnly` and re-entered into each other's town with `EnterSettlementAction.ApplyForCharacterOnly` — a swap; if the second town has none, the first wanderer simply moves there. Net effect: at most one wanderer leaves a town per day, counts never change, hired wanderers never move, and (unlike the spawn predicate) no town is excluded — siege towns can receive or lose wanderers. There is no travel phase: the leave+enter pair is instantaneous, and `Hero.CharacterStates.Traveling` is used only by `TeleportHeroAction` (a debug/teleport helper), never by wanderer movement.

**"Standing in the tavern" is a separate, view-time layer.** The hero never physically lives in a tavern on the map; the tavern agent exists only while the player is inside the settlement's location complex. `HeroAgentSpawnCampaignBehavior` listens on `SettlementEntered`, `OnSettlementLeft`, `OnMissionEnded`, `OnGameLoadFinished`, governor changes and prisoner changes, and each refresh only runs when `LocationComplex.Current` (the complex of the settlement the player is currently in, via `PlayerEncounter.LocationEncounter`) equals the settlement's complex. It then takes the settlement's `HeroesWithoutParty` (plus lords, prisoners, party leaders) and asks the `HeroAgentLocationModel` game model where each hero belongs: `DefaultHeroAgentLocationModel.GetLocationForHero` returns `HeroLocationDetail.Wanderer` and `settlement.LocationComplex.GetLocationWithId("tavern")` for a wanderer (or hired `IsPlayerCompanion`) in a **town**, `"center"` in a castle, `"village_center"` in a village. New characters are added with `location.AddCharacter` or moved with `LocationComplex.ChangeLocation`; wanderers never list in the settlement overlay unless their `CurrentSettlement` equals the player party's current/last-visited settlement (`WillBeListedInOverlay`).

**The tavern agent itself.** `HeroAgentSpawnCampaignBehavior.CreateLocationCharacterForHero` builds the wanderer's `LocationCharacter`: spawn tag `"npc_common"`, `FixedLocation = true`, civilian equipment, no horse, faction colors deliberately *not* applied (wanderers keep neutral look), and the idle animation set `_warrior_in_tavern` (or `_warrior_in_aserai_tavern` in Aserai/Khuzait culture towns). When the player walks into the tavern location, `MissionAgentHandler.SpawnWanderingAgent` instantiates that character's agent on a usable spawn point found by tag — `SpecialTargetTag` first, falling back to `npc_common_limited`, then `npc_common`, i.e. the generic NPC spawn points inside the town/tavern scene.

**Where the tavern location comes from.** Each `Settlement` owns a `LocationComplex` (`Settlement.LocationComplex`) built in `Settlement.Deserialize` from the settlement XML's `<Locations complex_template="…"/>` reference. The templates are an MBObject type loaded at campaign init by `SandBoxManager.InitializeSandboxXMLs` via `MBObjectManager.LoadXML("LocationComplexTemplates")`; `LocationComplex.GetLocationWithId` is the lookup the placement model calls. In the default model the player's access to the tavern is never restricted (`DefaultSettlementAccessModel` returns true unconditionally from `CanMainHeroGoToTavern`). Note the tavern's staff are not heroes at all: `TavernEmployeesCampaignBehavior` spawns the tavernkeeper, wench, musician and ransom broker as culture `CharacterObject`s on dedicated spawn points (`spawnpoint_tavernkeeper`, `sp_tavern_wench`, `musician`) whenever a location mission opens the `"tavern"` location.

**Implications for the mod.** A mod companion only appears in a tavern if (a) it is in `Settlement.HeroesWithoutParty` (i.e. placed via `EnterSettlementAction.ApplyForCharacterOnly` and not in a party) and (b) a hero-agent-location model maps it to a location — vanilla maps `IsWanderer || IsPlayerCompanion`, so a hired mod companion sitting partyless in a town still gets a tavern agent. Movement is entirely `SwapCompanions` (unhired wanderers only, one per day) — a mod that wants "my companion follows me between towns" or wants to stop the daily shuffle must hook these two action calls or replace `HeroAgentLocationModel`, not touch scenes.

### Evidence
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.StayingInSettlement` — the placement property: its setter calls `RemoveHeroWithoutParty` on the old settlement and `AddHeroWithoutParty` on the new one.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.CurrentSettlement` — resolves party, then prisoner party, then `StayingInSettlement`; the last branch is how a partyless wanderer "is in" a settlement.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs` — `Settlement.HeroesWithoutParty` — `MBReadOnlyList` backed by `_heroesWithoutPartyCache`, the list every wanderer placement/movement filter walks.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs` — `Settlement.AddHeroWithoutParty` — internal list maintenance invoked by the `StayingInSettlement` setter.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/EnterSettlementAction.cs` — `EnterSettlementAction.ApplyForCharacterOnly` — the only non-null write of `StayingInSettlement` (`hero.StayingInSettlement = settlement`) plus entry-event dispatch.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/LeaveSettlementAction.cs` — `LeaveSettlementAction.ApplyForCharacterOnly` — clears `StayingInSettlement` and removes the hero's `LocationCharacter` via `RemoveCharacterIfExists` from the settlement complex.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/AddHeroToPartyAction.cs` — `AddHeroToPartyAction` — first line `hero.StayingInSettlement = null`: a hired wanderer immediately stops being settlement-placed.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs` — `Hero.OnAddedToParty` — internal hook that also nulls `StayingInSettlement` whenever a hero joins any party.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/HeroCreator.cs` — `HeroCreator.CreateSpecialHero` — sets `BornSettlement` (the culture-matched town) but never places the hero in any settlement; placement is a separate step.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.CreateCompanionAndAddToSettlement` — creation order: `CreateSpecialHero` into a culture-matched town as birthplace, `ChangeState(Active)`, then `EnterSettlementAction.ApplyForCharacterOnly(hero, settlement)` into the actual host town.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.TrySpawnNewCompanion` — daily spawn-target town predicate: `SiegeEvent == null`, not the player's settlement, and every `HeroesWithoutParty` hero is hired or not a wanderer.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.SwapCompanions` — splits `Town.AllTowns` in halves, picks one `IsWanderer && CompanionOf == null` hero from each side, leaves and re-enters them in each other's settlement (swap, or simple move when the second town has none).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/CompanionsCampaignBehavior.cs` — `CompanionsCampaignBehavior.DailyTick` — cadence: `TryKillCompanion`, `SwapCompanions`, `TrySpawnNewCompanion`, in that order, every day.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Actions/TeleportHeroAction.cs` — `TeleportHeroAction` — the only decompiled use of `Hero.CharacterStates.Traveling`; wanderer swaps never use a travel state, they are instantaneous leave+enter.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/HeroAgentSpawnCampaignBehavior.cs` — `HeroAgentSpawnCampaignBehavior.RegisterEvents` — subscribes `SettlementEntered`, `OnSettlementLeft`, `OnMissionEnded`, `OnGameLoadFinishedEvent`, `OnGovernorChangedEvent`, prisoner events — the triggers that re-place hero agents inside locations.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/HeroAgentSpawnCampaignBehavior.cs` — `HeroAgentSpawnCampaignBehavior.RefreshLocationOfHeroesForPlayersCurrentSettlement` — only runs when `LocationComplex.Current == Settlement.CurrentSettlement.LocationComplex`; the roster it refreshes starts from `currentSettlement.HeroesWithoutParty`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/HeroAgentSpawnCampaignBehavior.cs` — `HeroAgentSpawnCampaignBehavior.RefreshLocationOfHeroForSettlement` — asks the location model for the hero's location; adds a `LocationCharacter` (`locationForHero.AddCharacter`) or `LocationComplex.ChangeLocation` when it changed.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultHeroAgentLocationModel.cs` — `DefaultHeroAgentLocationModel.GetLocationForHero` — the tavern rule: `HeroesWithoutParty.Contains(hero) && (hero.IsWanderer || hero.IsPlayerCompanion)` → `HeroLocationDetail.Wanderer` at `GetLocationWithId("tavern")` for a town, `"center"` for a castle, `"village_center"` otherwise.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultHeroAgentLocationModel.cs` — `WillBeListedInOverlay` — a hero shows in the settlement overlay list only when `CurrentSettlement` equals the main party's current (or last-visited) settlement.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.ComponentInterfaces/HeroAgentLocationModel.cs` — `HeroLocationDetail` — the swappable game model (`MBGameModel`) and its detail enum (`Wanderer`, `PartylessHeroInsideVillage`, …) that decide where each hero stands.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CampaignBehaviors/HeroAgentSpawnCampaignBehavior.cs` — `HeroAgentSpawnCampaignBehavior.CreateLocationCharacterForHero` — wanderer branch: spawn tag `"npc_common"`, `fixedLocation` true, `_warrior_in_tavern` action set (`_warrior_in_aserai_tavern` for Aserai/Khuzait cultures), civilian equipment; `SetAgentDataProperties` skips faction clothing colors exactly for the Wanderer detail.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements.Locations/LocationCharacter.cs` — `SpecialTargetTag` — the constructor's `spawnTag` parameter, stored per location character and used to pick spawn points.
- `decomp/SandBox/SandBox.Missions.MissionLogics/MissionAgentHandler.cs` — `SpawnWanderingAgent` — instantiates a location character's agent, taking the spawn frame from `GetInitialFrameForSpawnTag(SpecialTargetTag)` with fallbacks `"npc_common_limited"` then `"npc_common"`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs` — `Settlement.LocationComplex` — each settlement's instance; `Settlement.Deserialize` builds it from the XML `complex_template` reference (`new LocationComplex(complexTemplate)`).
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements.Locations/LocationComplex.cs` — `LocationComplex.GetLocationWithId` — the id lookup used for `"tavern"`, `"center"`, etc.; `Current` is derived from `PlayerEncounter.LocationEncounter.Settlement.LocationComplex`.
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/SandBoxManager.cs` — `SandBoxManager.InitializeSandboxXMLs` — loads the template objects: `MBObjectManager.Instance.LoadXML("LocationComplexTemplates")`.
- `~/bannerlord/moduledata/SandBox/Languages/std_location_complex_templates_xml.xml` — `Tavern` — localization for `location_complex_templates.xml` confirms the town complex really contains a named "Tavern" location (also "Center", "Village Center", "Castle Courtyard").
- `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultSettlementAccessModel.cs` — `CanMainHeroGoToTavern` — the tavern location is always accessible to the main hero in the default model (`return true` unconditionally).
- `decomp/SandBox/SandBox.CampaignBehaviors/TavernEmployeesCampaignBehavior.cs` — `LocationCharactersAreReadyToSpawn` — tavern *staff* (tavernkeeper/wench/musician) are spawned as culture characters, not heroes, on `spawnpoint_tavernkeeper`/`sp_tavern_wench`/`musician` spawn points when the current location `StringId == "tavern"`.

### Uncertain
- **UNCERTAIN**: the raw `location_complex_templates.xml` (the town complex's full `Location` list, passage graph, per-location scene names, and the "tavern" scene itself) is not in the local `~/bannerlord/moduledata` extract — only its localization stub proves a "Tavern" location name exists; resolve by reading a full game install's `Modules/SandBoxCore/ModuleData/location_complex_templates.xml`.
- **UNCERTAIN**: where the `npc_common` / `npc_common_limited` usable spawn points are placed inside the tavern scene and how many wanderers can stand at once — scenes are binary `.scene` assets, not present locally; resolve by inspecting the town/tavern scenes in the Bannerlord editor or by logging `unusedUsablePointCount` at runtime.
- **UNCERTAIN**: whether sieges, sackings or ownership changes additionally relocate or suppress partyless wanderers beyond what is documented here — `ChangeOwnerOfSettlementAction`/`SackSettlementAction` contain no `Wanderer`/`HeroesWithoutParty` handling in the decompiled sources, but the hero-agent refresh handlers explicitly skip while `Settlement.CurrentSettlement.IsUnderSiege`, so the in-world behaviour during sieges is unverified; resolve by auditing every write site of `StayingInSettlement = null` (prisoner/disable/capture paths) and by runtime observation during a siege.

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
