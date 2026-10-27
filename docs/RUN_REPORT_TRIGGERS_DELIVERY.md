# Run report — Triggers & delivery (SPEC_TRIGGERS_DELIVERY)

Version **0.4.0**. Branch `orchestra/foundation` (Track I developed on `orchestra/m11-track-i`, merged). Not pushed,
not deployed, not tested in game. Tests: Core.Tests 281/281, Acceptance 884/884; CLI `validate
module/ModuleData/Companions` → 0 errors, 0 warnings.

Companions can now react to the world: **166 triggers** (161 game events + 5 polling triggers) start conversations or
slot-free lines, delivered as a map conversation, a message-log notification or a pop-up, through a queue with limits.

## Per task

| Task | What | Notes |
|---|---|---|
| T1 registry (112–114) | `Core/Triggers`: `TriggerDefinition` (name, category, common, description, source, fields with kind + meaning + game source, subject fields, default subject, `inMission`, polling params), `TriggerRegistry` | 161 events from GAME_MAP Q35–Q42 (useful + niche + marginal) + `nearSettlement`, `daily`, `varCrossed`, `roleStarted`, `roleEnded`. Entries written by the worker in 8 batches (one per GAME_MAP question, all PASS on the first attempt), reviewed. Payload model (G2): entities hero / settlement / clan / kingdom / faction (clan or kingdom) / party / item with fixed properties, plus int, number, bool, text. |
| T1 naming pass (112) | `orchestra/triggers.tsv` | Full old → new table below (74 renamed). One event = one trigger. |
| T2 docs (115) | `docs/TRIGGERS.md`, CLI `docs triggers [outFile]` | Generated from the registry: subjects, field kinds and properties, delivery limits, common triggers, then per category every trigger with source, subjects + default, fields, params, example. `TriggerDocsTests` fails when the committed file differs. |
| T3 schema (116, 117) | `trigger` on conversations and slot-free lines; `event.<path>` and `role` conditions | Validation: unknown trigger, `on` missing, subject not valid for the trigger, `where` on unknown field / property or wrong value type, chance outside 0..1, unknown delivery, negative hours/days, params (polling only; nearSettlement radius > 0 and rearmRadius > radius; varCrossed own int variable or `mood`, direction up/down; role known), content vs delivery (triggered conversation: no entry, npc start; inquiry: one npc start, 1..N player options each ending; notification: start ends; a line: notification only, no slot), `event.` conditions only in triggered content and on the trigger's fields. SCHEMA.md "Triggers" + conditions. |
| T4 polling (119) | `TriggerPolling` + host ticks | `nearSettlement`: two-radius latch on the hourly tick (Q43), first look only arms (no fire when a save loads next to the town). `daily`: once per in-game day. `varCrossed`: after every effect that writes a variable, after `albocarius.set`, and daily. `roleStarted`/`roleEnded`: hourly diff of the companion's role (Q45/Q47), `role` condition. |
| T5 delivery (118, 123) | `TriggerEngine` (Core), `TriggerHost` (Game) | Fire → subject → where → companion in party (unless `allowAbsent`) → item cooldown → not already queued → chance → `when` → queue. Delivery: expired dropped, `when` and party presence re-checked, selection rule (priority, ties random) among the deliverable items, limits below. Conversation: `CampaignMapConversation.OpenConversation` with the companion (Q25); the conversation's start sentence is registered at the `start` token (priority 121) and passes only while that delivery is active. Notification: `Name: text` in the message log, start-node effects applied. Inquiry: `ShowInquiry` (1–2 options) / `ShowMultiSelectionInquiry` (3+), npc-node effects at show, the chosen option's effects on click. Queue, cooldowns, counters and polling state saved (`_triggerQueue`, `_triggerPolling`, List<string>). Listeners: **only for triggers used by loaded content** (G11, tested). |
| T6 morale (120) | effect `partyMorale` −20..20, not 0 | `MobileParty.MainParty.RecentEventsMorale += n` (Q22): shown under "Recent Events", decays 10 %/day. |
| T7 simulator + console (124, 125) | `serve` ops `triggers`, `fire`, `deliver`, `queue`, `clearCooldowns`; console `albocarius.fire <trigger> [field=id/value …]`, `albocarius.queue`, `albocarius.clearcooldowns` | Fire results list every candidate with each check (subject, where, present, cooldown, queued, chance, when + condition explanation). `docs/SIMULATOR_PROTOCOL.md` "Triggers". `albocarius.set` now also runs the varCrossed check. |
| T8 sample (126) | test_companion | `battle_won` (playerBattleEnded, conversation, `event.playerWon`), `idle_daily_01` (daily notification, chance 0.3), `campfire` (daily inquiry, 2 options, partyMorale +3 / −2), `near_town_01` (nearSettlement Epicrotea, 5 / 15), `mood_up_01` (varCrossed mood ≥ 10). en + tr. Zero warnings. |

## Constants (user, plan gate)

| Constant | Value |
|---|---|
| Pause per companion between interruptions (conversation or inquiry) | 3 in-game days |
| Interruptions per in-game day (all companions) | 1 |
| Notifications per in-game day (all companions, counted apart) | 3 |
| Default `expiresAfterHours` | 24 |
| Default `chance` / `cooldownDays` | 1 / 0 |
| Inquiry: standard pop-up up to | 2 options (3+ → multi-choice) |
| `partyMorale` range | −20..20 (not 0) |

All in `Core/Rules/DeliveryRules.cs`, documented in docs/TRIGGERS.md and SCHEMA.md.

## Excluded UI-raised events — how to verify each

These fire (per GAME_MAP Q35/Q40) only when the player clicks the map notification, before any choice. To verify: add a
temporary log listener on the event (one line in `TriggerAdapters`), then in game wait for the offer and **do not click**
the notification for an in-game day; then click it. If the log line appears only on the click, the event is UI-raised
and stays excluded; if it appears when the offer is made, it can be exposed.

| Event | How to get the offer |
|---|---|
| `OnRansomOfferedToPlayerEvent` | be captured with a clan member; or hold a lord prisoner until his clan offers ransom |
| `OnPeaceOfferedToPlayerEvent` | as a kingdom ruler, be at war long enough for the enemy to offer peace |
| `OnVassalOrMercenaryServiceOfferedToPlayerEvent` | clan tier 1+ without a kingdom; wait for a mercenary/vassal offer |
| `OnMarriageOfferedToPlayerEvent` | unmarried player with good relations to a lord with unmarried kin; wait for a proposal |

## UNCERTAIN

- **Conversation delivery to a companion outside the party** (`allowAbsent: true` with delivery `conversation`): Q25 only
  verified map conversations with party companions. Use `notification` or `inquiry` for absent companions until checked.
- **Start priority 121** for triggered conversations: above our greetings (119) and intro (120); no vanilla `start` line
  above 121 for a companion was found in Q18/Q20, but not proven for every quest conversation.
- **Multi-choice pop-up** (3+ options) is built with no image per row and an English "OK" button; not seen in game.
- **Adapter payloads** compile against the game DLLs and follow the verified GAME_MAP rows, but most events have never
  fired with the mod; a wrong field shows up as an empty value (adapters are guarded, a failure is logged, never thrown).
- **`inMission` flags** were set by the worker from the raise site; a wrong one only delays or speeds a delivery.
- `alleyTakenOver`, `alleyCleared` carry no fields (GAME_MAP names no member for the alley's town); prisoner-roster
  events carry no counts.

## BLOCKERS

None.

## Not done / changed from the plan

- Scaffold template: no trigger example (the template has only fixed slots; a triggered line there would make every new
  companion talk daily). SCHEMA.md and TRIGGERS.md document triggers.
- 115 (docs), 119 (polling), 122f–h (adapters Q40–Q42; the worker stalled on Q40 and was stopped) done by the orchestrator; 121 (API
  rows) folded into the adapter batches.

## In-game checklist

1. No regression: load, talk, hire, voice, conversations, transfers, noble, console (`albocarius.list/get/set/relation`).
2. Win a battle with test_companion in the party → after the battle screen, back on the map, the companion opens
   `battle_won` (not mid-battle). Losing → nothing (`event.playerWon`).
3. `daily` notification (`idle_daily_01`, 30 %) appears some days; never more than 3 notifications a day
   (`albocarius.fire daily` several times, `albocarius.queue` shows the counters).
4. Campfire pop-up (`campfire`, or `albocarius.clearcooldowns` + `albocarius.fire daily` until it is picked): two buttons;
   "Sing" → morale +3, "March" → morale −2 (party screen tooltip "Recent Events").
5. Ride to Epicrotea (town_EN1): within 5 map units the line appears once; ride beyond 15 and back → again.
6. `albocarius.set test_companion.mood 12` (from below 10) → `mood_up_01` appears.
7. `partyMorale` visible under "Recent Events" (item 4).
8. `albocarius.fire heroKilled victim=<lord id> detail=Executed` and `albocarius.queue` work; save with a queued item →
   load → `albocarius.queue` still lists it and the cooldowns.

Deploy 0.4.0: both DLLs, `SubModule.xml`, test_companion (`dialogs.json`, `lang/en.json`, `lang/tr.json`), the CLI.

## Naming table (old → new)

"Proposed name" is the name the GAME_MAP research suggested; ✱ marks a change. Full list: `orchestra/triggers.tsv`.

| CampaignEvents member | GAME_MAP | Proposed name (research) | Trigger name |
|---|---|---|---|
| `HeroWounded` | Q35 | `heroWounded` | `heroWounded` |
| `BattleStarted` | Q35 | `battleStarted` | `battleStarted` |
| `MapEventStarted` | Q35 | `mapEventStarted` | `mapEventStarted` |
| `MapEventEnded` | Q35 | `mapEventEnded` | `mapEventEnded` |
| `OnPlayerBattleEndEvent` | Q35 | `playerBattleEnded` | `playerBattleEnded` |
| `PlayerDesertedBattleEvent` | Q35 | `playerDeserted` | `playerDesertedBattle` ✱ |
| `OnHeroCombatHitEvent` | Q35 | `heroCombatHit` | `heroHitInCombat` ✱ |
| `OnSiegeEventStartedEvent` | Q35 | `siegeStarted` | `siegeStarted` |
| `OnPlayerSiegeStartedEvent` | Q35 | `playerSiegeJoined` | `playerJoinedSiege` ✱ |
| `CrimeRatingChanged` | Q35 | `crimeAdded` | `crimeRatingChanged` ✱ |
| `CharacterBecameFugitiveEvent` | Q35 | `companionBecameFugitive` | `heroBecameFugitive` ✱ |
| `BanditPartyRecruited` | Q35 | `banditsAbsorbed` | `banditPartyRecruited` ✱ |
| `TrackDetectedEvent` | Q35 | `trackSpotted` | `trackSpotted` |
| `TrackLostEvent` | Q35 | `trackLost` | `trackLost` |
| `HeroPrisonerTaken` | Q35 | `heroCaptured` | `heroCaptured` |
| `HeroPrisonerReleased` | Q35 | `heroReleased` | `heroReleased` |
| `PrisonersChangeInSettlement` | Q35 | `dungeonPopulationChanged` | `prisonersChangedInSettlement` ✱ |
| `OnRansomOfferCancelledEvent` | Q35 | `ransomOfferExpired` | `ransomOfferCancelled` ✱ |
| `VillageBeingRaided` | Q35 | `villageRaidStarted` | `villageRaidStarted` |
| `VillageLooted` | Q35 | `villageLooted` | `villageLooted` |
| `ItemsLooted` | Q35 | `itemsLooted` | `itemsLooted` |
| `TownRebelliosStateChanged` | Q35 | `townBecameRebellious` | `townRebellionStateChanged` ✱ |
| `RebellionFinished` | Q35 | `rebellionSucceeded` | `rebellionSucceeded` |
| `HeroKilledEvent` | Q35 | `heroKilled` | `heroKilled` |
| `BeforeHeroKilledEvent` | Q35 | `beforeHeroKilled` | `heroAboutToBeKilled` ✱ |
| `OnSiegeEventEndedEvent` | Q36 | `siegeEnded` | `siegeEnded` |
| `SiegeCompletedEvent` | Q36 | `—` | `siegeBattleEnded` ✱ |
| `OnSiegeAftermathAppliedEvent` | Q36 | `—` | `siegeAftermathApplied` ✱ |
| `OnBlockadeActivatedEvent` | Q36 | `portBlockaded` | `blockadeStarted` ✱ |
| `OnBlockadeDeactivatedEvent` | Q36 | `blockadeLifted` | `blockadeEnded` ✱ |
| `OnMobilePartyJoinedToSiegeEventEvent` | Q36 | `siegeJoin` | `partyJoinedSiege` ✱ |
| `OnMobilePartyLeftSiegeEventEvent` | Q36 | `siegeLeave` | `partyLeftSiege` ✱ |
| `RaidCompletedEvent` | Q36 | `—` | `villageRaidEnded` ✱ |
| `ForceVolunteersCompletedEvent` | Q36 | `volunteersForced` | `volunteersForced` |
| `ForceSuppliesCompletedEvent` | Q36 | `suppliesForced` | `suppliesForced` |
| `OnPrisonerTakenEvent` | Q36 | `prisonersTaken` | `prisonersTaken` |
| `OnPrisonerReleasedEvent` | Q36 | `prisonersReleased` | `prisonersReleased` |
| `OnMainPartyPrisonerRecruitedEvent` | Q36 | `prisonersRecruited` | `prisonersRecruited` |
| `OnPrisonerSoldEvent` | Q36 | `prisonersSold` | `prisonersSold` |
| `OnPrisonerDonatedToSettlementEvent` | Q36 | `prisonersDonated` | `prisonersDonated` |
| `OnLootDistributedToPartyEvent` | Q36 | `lootTaken` | `lootDistributed` ✱ |
| `OnCollectLootsItemsEvent` | Q36 | `lootCollected` | `lootCollected` |
| `OnHideoutSpottedEvent` | Q36 | `hideoutFound` | `hideoutSpotted` ✱ |
| `OnHideoutDeactivatedEvent` | Q36 | `hideoutCleared` | `hideoutEmptied` ✱ |
| `OnTroopsDesertedEvent` | Q36 | `desertion` | `troopsDeserted` ✱ |
| `ArmyCreated` | Q37 | `armyFormed` | `armyCreated` ✱ |
| `ArmyDispersed` | Q37 | `armyDismissed` | `armyDispersed` ✱ |
| `PartyAttachedAnotherParty` | Q37 | `playerJoinedHost` | `partyAttached` ✱ |
| `MobilePartyCreated` | Q37 | `partySpawned` | `partyCreated` ✱ |
| `MobilePartyDestroyed` | Q37 | `partyDestroyed` | `partyDestroyed` |
| `SettlementEntered` | Q37 | `settlementEntered` | `settlementEntered` |
| `OnSettlementLeftEvent` | Q37 | `settlementLeft` | `settlementLeft` |
| `VillageStateChanged` | Q37 | `villageRaided` | `villageStateChanged` ✱ |
| `OnBuildingLevelChangedEvent` | Q37 | `constructionFinished` | `buildingLevelChanged` ✱ |
| `AlleyOwnerChanged` | Q37 | `hideoutOwnershipChanged` | `alleyOwnerChanged` ✱ |
| `AlleyOccupiedByPlayer` | Q37 | `hideoutTaken` | `alleyTakenOver` ✱ |
| `AlleyClearedByPlayer` | Q37 | `hideoutCleared` | `alleyCleared` ✱ |
| `TournamentStarted` | Q37 | `tournamentAnnounced` | `tournamentStarted` ✱ |
| `TournamentFinished` | Q37 | `tournamentWon` | `tournamentFinished` ✱ |
| `TournamentCancelled` | Q37 | `tournamentCancelled` | `tournamentCancelled` |
| `PlayerEliminatedFromTournament` | Q37 | `tournamentLost` | `playerEliminatedFromTournament` ✱ |
| `AfterMissionStarted` | Q37 | `prefer` | `sceneEntered` ✱ |
| `ConversationEnded` | Q37 | `conversationEnded` | `conversationEnded` |
| `HeroOrPartyTradedGold` | Q37 | `playerGaveGold` | `goldGiven` ✱ |
| `HeroOrPartyGaveItem` | Q37 | `itemGifted` | `itemGiven` ✱ |
| `OnPartyJoinedArmyEvent` | Q38 | `partyJoinedArmy` | `partyJoinedArmy` |
| `PartyRemovedFromArmyEvent` | Q38 | `partyLeftArmy` | `partyLeftArmy` |
| `OnPartyLeaderChangedEvent` | Q38 | `partyLeaderChanged` | `partyLeaderChanged` |
| `OnPartyDisbandStartedEvent` | Q38 | `partyDisbandStarted` | `partyDisbandStarted` |
| `OnPartyDisbandedEvent` | Q38 | `partyDisbanded` | `partyDisbanded` |
| `PartyVisibilityChangedEvent` | Q38 | `partySpotted` | `partyVisibilityChanged` ✱ |
| `OnUnitRecruitedEvent` | Q38 | `troopRecruited` | `playerRecruitedTroops` ✱ |
| `OnTroopRecruitedEvent` | Q38 | `allyRecruitedTroops` | `heroRecruitedTroops` ✱ |
| `PlayerUpgradedTroopsEvent` | Q38 | `troopsUpgraded` | `troopsUpgraded` |
| `OnTroopGivenToSettlementEvent` | Q38 | `allyGarrisonedTroops` | `troopsGivenToSettlement` ✱ |
| `OnMainPartyStarvingEvent` | Q38 | `partyStarving` | `playerPartyStarving` ✱ |
| `PlayerStartTalkFromMenu` | Q38 | `playerStartTalk` | `playerStartedTalkFromMenu` ✱ |
| `OnPlayerJoinedTournamentEvent` | Q38 | `playerJoinedTournament` | `playerJoinedTournament` |
| `HeroCreated` | Q39 | `heroCreated` | `heroCreated` |
| `OnHeroActivatedEvent` | Q39 | `heroActivated` | `heroActivated` |
| `HeroOccupationChangedEvent` | Q39 | `heroOccupationChanged` | `heroOccupationChanged` |
| `HeroLevelledUp` | Q39 | `heroLeveledUp` | `heroLeveledUp` |
| `HeroGainedSkill` | Q39 | `heroSkillUp` | `heroGainedSkill` ✱ |
| `OnHeroJoinedPartyEvent` | Q39 | `heroJoinedParty` | `heroJoinedParty` |
| `NewCompanionAdded` | Q39 | `companionAdded` | `companionAdded` |
| `OnPlayerMetHeroEvent` | Q39 | `heroMet` | `playerMetHero` ✱ |
| `PlayerTraitChangedEvent` | Q39 | `traitChanged` | `playerTraitChanged` ✱ |
| `PerkOpenedEvent` | Q39 | `perkOpened` | `perkUnlocked` ✱ |
| `HeroRelationChanged` | Q39 | `relationChanged` | `relationChanged` |
| `ClanTierIncrease` | Q39 | `clanTierUp` | `clanTierIncreased` ✱ |
| `RenownGained` | Q39 | `renownGained` | `renownGained` |
| `OnClanCreatedEvent` | Q39 | `clanCreated` | `clanCreated` |
| `OnClanChangedKingdomEvent` | Q39 | `clanChangedKingdom` | `clanChangedKingdom` |
| `OnClanDefectedEvent` | Q39 | `clanDefected` | `clanDefected` |
| `KingdomDecisionAdded` | Q39 | `kingdomDecisionProposed` | `kingdomDecisionProposed` |
| `KingdomDecisionCancelled` | Q39 | `kingdomDecisionCancelled` | `kingdomDecisionCancelled` |
| `KingdomDecisionConcluded` | Q39 | `kingdomDecisionConcluded` | `kingdomDecisionConcluded` |
| `WarDeclared` | Q39 | `warDeclared` | `warDeclared` |
| `MakePeace` | Q39 | `madePeace` | `peaceMade` ✱ |
| `RulingClanChanged` | Q39 | `rulerChanged` | `rulingClanChanged` ✱ |
| `KingdomDestroyedEvent` | Q39 | `kingdomDestroyed` | `kingdomDestroyed` |
| `RomanticStateChanged` | Q39 | `romanceChanged` | `romanticStateChanged` ✱ |
| `BeforeHeroesMarried` | Q39 | `beforeMarriage` | `heroesAboutToMarry` ✱ |
| `ChildEducationCompletedEvent` | Q39 | `childEducationStage` | `childEducationCompleted` ✱ |
| `HeroComesOfAgeEvent` | Q39 | `comesOfAge` | `heroCameOfAge` ✱ |
| `HeroGrowsOutOfInfancyEvent` | Q39 | `outOfInfancy` | `heroGrewOutOfInfancy` ✱ |
| `HeroReachesTeenAgeEvent` | Q39 | `reachesTeenAge` | `heroBecameTeenager` ✱ |
| `KingdomCreatedEvent` | Q40 | `kingdomCreated` | `kingdomCreated` |
| `OnTradeAgreementSignedEvent` | Q40 | `tradeAgreementSigned` | `tradeAgreementSigned` |
| `OnAllianceStartedEvent` | Q40 | `allianceStarted` | `allianceStarted` |
| `OnAllianceEndedEvent` | Q40 | `allianceEnded` | `allianceEnded` |
| `OnCallToWarAgreementStartedEvent` | Q40 | `callToWarStarted` | `callToWarStarted` |
| `OnCallToWarAgreementEndedEvent` | Q40 | `callToWarEnded` | `callToWarEnded` |
| `OnClanEarnedGoldFromTributeEvent` | Q40 | `clanTributeEarned` | `tributeEarned` ✱ |
| `OnSettlementOwnerChangedEvent` | Q40 | `settlementOwnerChanged` | `settlementOwnerChanged` |
| `OnGovernorChangedEvent` | Q40 | `governorChanged` | `governorChanged` |
| `OnVassalOrMercenaryServiceOfferCanceledEvent` | Q40 | `kingdomOfferWithdrawn` | `serviceOfferCancelled` ✱ |
| `OnMercenaryServiceStartedEvent` | Q40 | `mercenaryHired` | `mercenaryServiceStarted` ✱ |
| `OnMercenaryServiceEndedEvent` | Q40 | `mercenaryLeft` | `mercenaryServiceEnded` ✱ |
| `OnMarriageOfferCanceledEvent` | Q40 | `marriageOfferCanceled` | `marriageOfferCancelled` ✱ |
| `OnChildConceivedEvent` | Q40 | `childConceived` | `childConceived` |
| `OnGivenBirthEvent` | Q40 | `childBorn` | `childBorn` |
| `OnHeirSelectionRequestedEvent` | Q40 | `heirSelectionRequested` | `heirSelectionRequested` |
| `OnHeirSelectionOverEvent` | Q40 | `heirSelected` | `heirSelected` |
| `OnBeforePlayerCharacterChangedEvent` | Q40 | `playerCharacterChanging` | `playerCharacterAboutToChange` ✱ |
| `OnPlayerCharacterChangedEvent` | Q40 | `playerCharacterChanged` | `playerCharacterChanged` |
| `OnClanLeaderChangedEvent` | Q40 | `clanLeaderChanged` | `clanLeaderChanged` |
| `OnClanDestroyedEvent` | Q40 | `clanDestroyed` | `clanDestroyed` |
| `OnHeroChangedClanEvent` | Q40 | `heroChangedClan` | `heroChangedClan` |
| `OnClanInfluenceChangedEvent` | Q40 | `clanInfluenceChanged` | `clanInfluenceChanged` |
| `CompanionRemoved` | Q40 | `companionRemoved` | `companionRemoved` |
| `OnHeroGetsBusyEvent` | Q40 | `heroGotBusy` | `heroGotBusy` |
| `OnHeroSharedFoodWithAnotherHeroEvent` | Q40 | `sharedFood` | `heroSharedFood` ✱ |
| `OnHeroTeleportationRequestedEvent` | Q40 | `heroTeleportRequested` | `heroTeleportRequested` |
| `PlayerInventoryExchangeEvent` | Q42 | `tradeSessionClosed` | `inventoryExchanged` ✱ |
| `OnItemsDiscardedByPlayerEvent` | Q42 | `itemsDiscarded` | `itemsDiscarded` |
| `OnPlayerTradeProfitEvent` | Q42 | `tradeProfit` | `tradeProfitMade` ✱ |
| `OnCaravanTransactionCompletedEvent` | Q42 | `caravanTradeCompleted` | `caravanTradeCompleted` |
| `OnPlayerEarnedGoldFromAssetEvent` | Q42 | `assetIncome` | `assetIncomeEarned` ✱ |
| `WorkshopOwnerChangedEvent` | Q42 | `workshopOwnerChanged` | `workshopOwnerChanged` |
| `WorkshopTypeChangedEvent` | Q42 | `workshopTypeChanged` | `workshopTypeChanged` |
| `OnItemProducedEvent` | Q42 | `fiefProduction` | `itemProduced` ✱ |
| `OnItemConsumedEvent` | Q42 | `fiefConsumption` | `itemConsumed` ✱ |
| `OnNewItemCraftedEvent` | Q42 | `itemCrafted` | `itemCrafted` |
| `CraftingPartUnlockedEvent` | Q42 | `craftingPartUnlocked` | `craftingPartUnlocked` |
| `OnEquipmentSmeltedByHeroEvent` | Q42 | `itemSmelted` | `itemSmelted` |
| `OnItemsRefinedEvent` | Q42 | `itemsRefined` | `itemsRefined` |
| `OnCraftingOrderCompletedEvent` | Q42 | `craftingOrderDone` | `craftingOrderCompleted` ✱ |
| `OnQuestStartedEvent` | Q42 | `questStarted` | `questStarted` |
| `OnQuestCompletedEvent` | Q42 | `questCompleted` | `questCompleted` |
| `OnNewIssueCreatedEvent` | Q42 | `issueCreated` | `issueCreated` |
| `OnIssueUpdatedEvent` | Q42 | `issueProgress` | `issueUpdated` ✱ |
| `OnIncidentResolvedEvent` | Q42 | `incidentResolved` | `incidentResolved` |
| `PersuasionProgressCommittedEvent` | Q42 | `persuasionChoice` | `persuasionProgressed` ✱ |
| `QuestLogAddedEvent` | Q41 | `questLogAdded` | `questLogEntryAdded` ✱ |
| `IssueLogAddedEvent` | Q41 | `issueLogAdded` | `issueLogEntryAdded` ✱ |
| `OnBarterAcceptedEvent` | Q41 | `barterAccepted` | `barterAccepted` |
| `OnBarterCanceledEvent` | Q41 | `barterRejected` | `barterCancelled` ✱ |
| `CharacterPortraitPopUpOpenedEvent` | Q41 | `companionPortraitOpened` | `portraitOpened` ✱ |
| `OnPlayerBoardGameOverEvent` | Q41 | `boardGameFinished` | `boardGameFinished` |
| `OnTradeRumorIsTakenEvent` | Q41 | `tradeRumorHeard` | `tradeRumorHeard` |

| CampaignEvents member | GAME_MAP | Reason |
|---|---|---|
| `OnRansomOfferedToPlayerEvent` | Q35 | UI-raised (notification inspect), not exposed yet |
| `AfterSiegeCompletedEvent` | Q36 | duplicate of SiegeCompletedEvent (siegeBattleEnded) |
| `BeforeSettlementEnteredEvent` | Q37 | duplicate of SettlementEntered (settlementEntered) |
| `AfterSettlementEntered` | Q37 | duplicate of SettlementEntered (settlementEntered) |
| `DailyTickTownEvent` | Q37 | tick, internal |
| `OnMissionStartedEvent` | Q37 | duplicate of AfterMissionStarted (sceneEntered) |
| `DailyTickPartyEvent` | Q37 | tick, internal |
| `OnShipCreatedEvent` | Q38 | DLC only |
| `OnMapMarkerCreatedEvent` | Q38 | DLC only |
| `OnPeaceOfferedToPlayerEvent` | Q40 | UI-raised (notification inspect), not exposed yet |
| `OnVassalOrMercenaryServiceOfferedToPlayerEvent` | Q40 | UI-raised (notification inspect), not exposed yet |
| `OnMarriageOfferedToPlayerEvent` | Q40 | UI-raised (notification inspect), not exposed yet |
| `DailyTickClanEvent` | Q40 | tick, internal |
| `HourlyTickClanEvent` | Q40 | tick, internal |
| `OnCharacterCreationIsOverEvent` | Q41 | lifecycle, internal |
