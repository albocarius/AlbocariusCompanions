# Run report — Selection rules + event research (SPEC_SELECTION_AND_EVENTS)

Version **0.3.0**. Branch `orchestra/foundation` (Track I developed on `orchestra/m9-track-i`, merged). Not pushed,
not deployed. Tests: Core.Tests 281/281, Acceptance 705/705; CLI `validate module/ModuleData/Companions` → 0 errors,
0 warnings.

## Track I — implementation

| Task | What | Notes |
|---|---|---|
| I1 selection rule (90, 91) | Core `Selection` + `SelectionMemory`, `ConversationSelection`; `priority` on conversations, slot lines, npc nodes; `when` on slot lines (gate F4) | Per the gate (F1/F2): **hub conversations are all listed**, ordered by priority (ties = file order). One winner (highest priority, ties random) for: the greeting that opens a talk, its start node, the companion reply among a `next` list, the line of a slot. A pick is kept for the whole talk, re-decided only when it no longer holds, dropped on `CampaignEvents.ConversationEnded`. Game: each npc sentence passes only if it is the chosen node (GAME_MAP Q20 addendum); hub start options are registered in priority order (same game priority, stable order). `priority` on a player node → warning (no effect). Sample: `coin_refuse` got `priority: 1`, so relation 10–20 still gives the refusal (unchanged behavior). SCHEMA.md "Selection rule". |
| I2 shadowing (92) | warning `shadowed_candidate` | Sets: greetings (one that always holds = no `when`, not `once`, an unconditioned start node), greeting start nodes, npc replies of one `next`, slot lines. Implied conditions (transfer from a non-world actor, join) count as conditions. Hub conversations are not a set. Fixture: `tests/fixtures/shadowing/shadow_companion`. |
| I3 simulator (94, 95) | Core `Simulation` (`SimState`, `SimSession`, `ConditionExplain`, `SimulatorProtocol`); CLI `serve [root]` | JSON lines on stdin/stdout, documented in `docs/SIMULATOR_PROTOCOL.md`. Each step: candidates, per-field condition results, top priority group, winner, applied effects, new state. Verdicts come from the game's condition code; the explanation is extra. Known differences from the game: relation applied raw (no vanilla scaling), no "anything else?" line before the hub, the fixed dialogs other than `hire` are not simulated. Integration test drives a real `serve` process with the sample sequence. |
| I4 `noble` (93) | companion.json `noble` (bool, default false) | Model, strict parsing (non-bool → error), template, SCHEMA.md. No behavior. |
| I5 fallback warning (96) | already in 0.2.2; key changed | Now once per session per companion and **slot** (per sentence when no slot covers it); `VanillaDialogPoints.WarningKey` in Core, tested. Message says "no line … that holds now" since slot lines can have `when`. |
| I6 / F9 decide items (97) | rescue / prison break | Verified in decomp: every line the rescued companion says (`rescue_companion_*` ×8, `prison_break_start_1/_2`, `prison_break_next_companion`) was already a required slot since M5. The other prison-break lines are only said by prisoners that are not player companions. Nothing added. Quest-created companions and the kingdom expelling the clan stay vanilla (your call at the gate). |

Remaining V3 *decide* items not covered by §0 or the gate: **none**.

## Track R — research (GAME_MAP "Triggers & roles", Q35–Q47, all VERIFIED)

- **R1 events (Q35–Q42):** all 263 `CampaignEvents` members, grouped by theme, each with arguments, raising call site,
  before/after the outcome, the player-involvement test and a trigger suggestion. Verification: every row's cited raiser
  checked against decomp (script + 31 by hand); Q39's line numbers are up to ~10 lines off, the raisers are right.
  Totals: 162 useful, 12 niche, 89 not useful. Table below.
- **Polling-only signals (Q42):** `foodLow`/`foodCritical`, `wageDebt`, `moraleLow`, `goldLow`, `companionWounded`,
  `nearSettlement`, `nightfall`/`seasonChanged` — no event exists, a daily/hourly check is needed.
- **R2 proximity (Q43):** no event. Poll on `HourlyTickEvent` (or a mod periodic event for finer steps), straight-line
  `CampaignVec2.Distance` (map units) or `DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement` (navigation-aware).
  Scale: a Speed-4 party moves ≈3.3 units per game hour; encounter radius 3; nearest-fortification spacing median ≈31
  (min 17). Suggested latch: fire on entering 20–25 units, re-arm after leaving 2× that; the latch state goes in the save.
- **R3 lord flow (Q44):** one player-facing path — the fief-grant conversation → `CompanionRolesCampaignBehavior.ClanNameSelectionIsDone`
  (`RemoveCompanionAction.ApplyByByTurningToLord`, `SetNewOccupation(Lord)`, `Clan.CreateCompanionToLordClan`, +50
  relation with the player). Vanilla changes **no** relation between the new lord and other lords. Hook for `noble`:
  `OnClanCreatedEvent` with `isCompanion == true` (single raiser). No vanilla "is now a lord" notification. The hero keeps
  its `StringId`/template (detection), and `hero_main_options` lines with a hero condition can add options to lord talk.
- **R4 roles (Q45, Q47):** governor (`Hero.GovernorOf`, `OnGovernorChangedEvent`, also on capture), party/caravan leader
  (`MobileParty.LeaderHero`, `IsCaravan`; destroyed/disbanded events), alley leader (`IAlleyCampaignBehavior`
  queries), alternative solution (`IssueBase.AlternativeSolutionHero`, `IssueManager.IssueSolvingCompanionList`). There is
  no "role ended / hero free again" event anywhere — polling needed. A governor or party leader stays in
  `PlayerClan.Companions` but leaves the main party.
- **R5 marriage (Q46):** single entry `MarriageAction.Apply(Hero, Hero, bool)`, gated by
  `MarriageModel.IsCoupleSuitableForMarriage`. **Two companions cannot marry under vanilla rules**: companions are
  `Occupation.Wanderer` and `IsSuitableForMarriage` requires `IsLord` — the action is a silent no-op. Courtship
  (`RomanceCampaignBehavior`) is the same gate. A design that wants companion marriage needs its own rules.
- **Correction to Q34 (M8 audit):** a hired companion's `Hero.Clan` is the player clan (`CompanionOf ?? _clan`), not null.
  The two Q34 rows that relied on it (tournament-prize toast, "gather my companions") stay *keep*; their companion
  replies are already slots. No guard changes.

### R1 — event → trigger → usefulness

| Q | Theme | Event | Trigger | Usefulness |
|---|---|---|---|---|
| Q35 | Battle & siege | `HeroWounded` | `heroWounded` | useful |
| Q35 | Battle & siege | `BattleStarted` | `battleStarted` | useful |
| Q35 | Battle & siege | `MapEventStarted` | `mapEventStarted` | useful |
| Q35 | Battle & siege | `MapEventEnded` | `mapEventEnded` | useful |
| Q35 | Battle & siege | `OnPlayerBattleEndEvent` | `playerBattleEnded` | useful |
| Q35 | Battle & siege | `PlayerDesertedBattleEvent` | `playerDeserted` | useful |
| Q35 | Battle & siege | `OnHeroCombatHitEvent` | `heroCombatHit` | useful |
| Q35 | Battle & siege | `OnSiegeEventStartedEvent` | `siegeStarted` | useful |
| Q35 | Battle & siege | `OnPlayerSiegeStartedEvent` | `playerSiegeJoined` | useful |
| Q35 | Crime, fugitives, bandits | `CrimeRatingChanged` | `crimeAdded` | useful |
| Q35 | Crime, fugitives, bandits | `CharacterBecameFugitiveEvent` | `companionBecameFugitive` | useful |
| Q35 | Crime, fugitives, bandits | `BanditPartyRecruited` | `banditsAbsorbed` | useful |
| Q35 | Map tracks | `TrackDetectedEvent` | `trackSpotted` | useful |
| Q35 | Map tracks | `TrackLostEvent` | `trackLost` | niche — trail-scent fading flavor |
| Q35 | Prisoners & ransom | `HeroPrisonerTaken` | `heroCaptured` | useful |
| Q35 | Prisoners & ransom | `HeroPrisonerReleased` | `heroReleased` | useful |
| Q35 | Prisoners & ransom | `PrisonersChangeInSettlement` | `dungeonPopulationChanged` | useful |
| Q35 | Prisoners & ransom | `OnRansomOfferedToPlayerEvent` | `ransomOffered` | niche — companion reaction to a ransom demand concerning them |
| Q35 | Prisoners & ransom | `OnRansomOfferCancelledEvent` | `ransomOfferExpired` | niche — the killed/released cases already fire HeroKilledEvent/HeroPrisonerReleased (Q23); only th |
| Q35 | Raids & settlements | `VillageBeingRaided` | `villageRaidStarted` | useful |
| Q35 | Raids & settlements | `VillageLooted` | `villageLooted` | useful |
| Q35 | Raids & settlements | `ItemsLooted` | `itemsLooted` | useful |
| Q35 | Raids & settlements | `TownRebelliosStateChanged` | `townBecameRebellious` | useful |
| Q35 | Raids & settlements | `RebellionFinished` | `rebellionSucceeded` | useful |
| Q35 | Heroes & characters | `HeroKilledEvent` | `heroKilled` | useful |
| Q35 | Heroes & characters | `BeforeHeroKilledEvent` | `beforeHeroKilled` | useful |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnSiegeEventEndedEvent` | `siegeEnded` | useful |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `SiegeCompletedEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `AfterSiegeCompletedEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnSiegeAftermathAppliedEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnBlockadeActivatedEvent` | `portBlockaded` | useful |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnBlockadeDeactivatedEvent` | `blockadeLifted` | useful |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnMobilePartyJoinedToSiegeEventEvent` | `siegeJoin` | useful |
| Q36 | Siege — whole siege, its battles, engines, bombardment, blockade | `OnMobilePartyLeftSiegeEventEvent` | `siegeLeave` | useful |
| Q36 | Raids and forced contributions | `RaidCompletedEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q36 | Raids and forced contributions | `ForceVolunteersCompletedEvent` | `volunteersForced` | useful |
| Q36 | Raids and forced contributions | `ForceSuppliesCompletedEvent` | `suppliesForced` | useful |
| Q36 | Prisoners | `OnPrisonerTakenEvent` | `prisonersTaken` | useful |
| Q36 | Prisoners | `OnPrisonerReleasedEvent` | `prisonersReleased` | useful |
| Q36 | Prisoners | `OnMainPartyPrisonerRecruitedEvent` | `prisonersRecruited` | useful |
| Q36 | Prisoners | `OnPrisonerSoldEvent` | `prisonersSold` | useful |
| Q36 | Prisoners | `OnPrisonerDonatedToSettlementEvent` | `prisonersDonated` | useful |
| Q36 | Loot and battle simulation | `OnLootDistributedToPartyEvent` | `lootTaken` | useful |
| Q36 | Loot and battle simulation | `OnCollectLootsItemsEvent` | `lootCollected` | useful |
| Q36 | Hideouts | `OnHideoutSpottedEvent` | `hideoutFound` | useful |
| Q36 | Hideouts | `OnHideoutDeactivatedEvent` | `hideoutCleared` | useful |
| Q36 | Party bookkeeping and internal plumbing | `OnTroopsDesertedEvent` | `desertion` | useful |
| Q37 | War & armies | `ArmyCreated` | `armyFormed` | useful |
| Q37 | War & armies | `ArmyDispersed` | `armyDismissed` | useful |
| Q37 | War & armies | `PartyAttachedAnotherParty` | `playerJoinedHost` | useful |
| Q37 | Party lifecycle & map objects | `MobilePartyCreated` | `partySpawned` | niche — mostly vanilla issue scaffolding; gate on Clan.PlayerClan if used |
| Q37 | Party lifecycle & map objects | `MobilePartyDestroyed` | `partyDestroyed` | useful |
| Q37 | Settlements, villages, towns, construction | `BeforeSettlementEnteredEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q37 | Settlements, villages, towns, construction | `SettlementEntered` | `settlementEntered` | useful |
| Q37 | Settlements, villages, towns, construction | `AfterSettlementEntered` | `—` | niche — only needed if other behaviors already listen on the first two slots |
| Q37 | Settlements, villages, towns, construction | `OnSettlementLeftEvent` | `settlementLeft` | useful |
| Q37 | Settlements, villages, towns, construction | `VillageStateChanged` | `villageRaided` | useful |
| Q37 | Settlements, villages, towns, construction | `DailyTickTownEvent` | `playerTownDailyTick` | niche — governance/governor pulse for player fiefs; not a reaction (no delta payload) |
| Q37 | Settlements, villages, towns, construction | `OnBuildingLevelChangedEvent` | `constructionFinished` | useful |
| Q37 | Crime & hideouts (town alleys) | `AlleyOwnerChanged` | `hideoutOwnershipChanged` | useful |
| Q37 | Crime & hideouts (town alleys) | `AlleyOccupiedByPlayer` | `hideoutTaken` | useful |
| Q37 | Crime & hideouts (town alleys) | `AlleyClearedByPlayer` | `hideoutCleared` | useful |
| Q37 | Tournaments | `TournamentStarted` | `tournamentAnnounced` | useful |
| Q37 | Tournaments | `TournamentFinished` | `tournamentWon` | useful |
| Q37 | Tournaments | `TournamentCancelled` | `tournamentCancelled` | useful |
| Q37 | Tournaments | `PlayerEliminatedFromTournament` | `tournamentLost` | useful |
| Q37 | Missions & conversation | `OnMissionStartedEvent` | `sceneEntered` | useful |
| Q37 | Missions & conversation | `AfterMissionStarted` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q37 | Missions & conversation | `ConversationEnded` | `conversationEnded` | useful |
| Q37 | Money & items | `HeroOrPartyTradedGold` | `playerGaveGold` | niche — otherwise a firehose |
| Q37 | Money & items | `HeroOrPartyGaveItem` | `itemGifted` | niche — gift flavor; vanilla call sites are few (quest/item trades) and payload slots may be null  |
| Q37 | Periodic ticks | `DailyTickPartyEvent` | — | duplicate of another trigger (see the GAME_MAP row) |
| Q38 | Party lifecycle & command | `OnPartyJoinedArmyEvent` | `partyJoinedArmy` | useful |
| Q38 | Party lifecycle & command | `PartyRemovedFromArmyEvent` | `partyLeftArmy` | useful |
| Q38 | Party lifecycle & command | `OnPartyLeaderChangedEvent` | `partyLeaderChanged` | useful |
| Q38 | Party lifecycle & command | `OnPartyDisbandStartedEvent` | `partyDisbandStarted` | useful |
| Q38 | Party lifecycle & command | `OnPartyDisbandedEvent` | `partyDisbanded` | useful |
| Q38 | Party lifecycle & command | `PartyVisibilityChangedEvent` | `partySpotted` | niche — ambush/detection flavor line when MainParty's visibility flips; fires also for every AI pa |
| Q38 | Troops, recruitment & supplies | `OnUnitRecruitedEvent` | `troopRecruited` | useful |
| Q38 | Troops, recruitment & supplies | `OnTroopRecruitedEvent` | `allyRecruitedTroops` | useful |
| Q38 | Troops, recruitment & supplies | `PlayerUpgradedTroopsEvent` | `troopsUpgraded` | useful |
| Q38 | Troops, recruitment & supplies | `OnTroopGivenToSettlementEvent` | `allyGarrisonedTroops` | niche — companion garrison activity flavor |
| Q38 | Troops, recruitment & supplies | `OnMainPartyStarvingEvent` | `partyStarving` | useful |
| Q38 | Conversation, tournament & map markers | `PlayerStartTalkFromMenu` | `playerStartTalk` | niche — one-line companion pre-banter about hero before the dialog opens; gate to avoid chatter sp |
| Q38 | Conversation, tournament & map markers | `OnPlayerJoinedTournamentEvent` | `playerJoinedTournament` | niche — signup moment + spectator distinction; the tournament *lifecycle* (start/match/end) is alr |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `HeroCreated` | `heroCreated` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `OnHeroActivatedEvent` | `heroActivated` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `HeroOccupationChangedEvent` | `heroOccupationChanged` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `HeroLevelledUp` | `heroLeveledUp` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `HeroGainedSkill` | `heroSkillUp` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `OnHeroJoinedPartyEvent` | `heroJoinedParty` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `NewCompanionAdded` | `companionAdded` | useful |
| Q39 | Hero lifecycle (creation, state, leveling, parties) | `OnPlayerMetHeroEvent` | `heroMet` | useful |
| Q39 | Player character (body, traits, perks) | `PlayerTraitChangedEvent` | `traitChanged` | useful |
| Q39 | Player character (body, traits, perks) | `PerkOpenedEvent` | `perkOpened` | useful |
| Q39 | Clan, renown, relations | `HeroRelationChanged` | `relationChanged` | useful |
| Q39 | Clan, renown, relations | `ClanTierIncrease` | `clanTierUp` | useful |
| Q39 | Clan, renown, relations | `RenownGained` | `renownGained` | useful |
| Q39 | Clan, renown, relations | `OnClanCreatedEvent` | `clanCreated` | useful |
| Q39 | Clan, renown, relations | `OnClanChangedKingdomEvent` | `clanChangedKingdom` | useful |
| Q39 | Clan, renown, relations | `OnClanDefectedEvent` | `clanDefected` | useful |
| Q39 | Kingdom decisions, war & peace | `KingdomDecisionAdded` | `kingdomDecisionProposed` | useful |
| Q39 | Kingdom decisions, war & peace | `KingdomDecisionCancelled` | `kingdomDecisionCancelled` | useful |
| Q39 | Kingdom decisions, war & peace | `KingdomDecisionConcluded` | `kingdomDecisionConcluded` | useful |
| Q39 | Kingdom decisions, war & peace | `WarDeclared` | `warDeclared` | useful |
| Q39 | Kingdom decisions, war & peace | `MakePeace` | `madePeace` | useful |
| Q39 | Kingdom decisions, war & peace | `RulingClanChanged` | `rulerChanged` | useful |
| Q39 | Kingdom decisions, war & peace | `KingdomDestroyedEvent` | `kingdomDestroyed` | useful |
| Q39 | Romance, marriage, children & aging | `RomanticStateChanged` | `romanceChanged` | useful |
| Q39 | Romance, marriage, children & aging | `BeforeHeroesMarried` | `beforeMarriage` | useful |
| Q39 | Romance, marriage, children & aging | `ChildEducationCompletedEvent` | `childEducationStage` | useful |
| Q39 | Romance, marriage, children & aging | `HeroComesOfAgeEvent` | `comesOfAge` | useful |
| Q39 | Romance, marriage, children & aging | `HeroGrowsOutOfInfancyEvent` | `outOfInfancy` | useful |
| Q39 | Romance, marriage, children & aging | `HeroReachesTeenAgeEvent` | `reachesTeenAge` | useful |
| Q40 | Kingdom, war and peace | `KingdomCreatedEvent` | `kingdomCreated` | useful |
| Q40 | Kingdom, war and peace | `OnPeaceOfferedToPlayerEvent` | `peaceOfferReceived` | useful |
| Q40 | Kingdom, war and peace | `OnTradeAgreementSignedEvent` | `tradeAgreementSigned` | useful |
| Q40 | Kingdom, war and peace | `OnAllianceStartedEvent` | `allianceStarted` | useful |
| Q40 | Kingdom, war and peace | `OnAllianceEndedEvent` | `allianceEnded` | useful |
| Q40 | Kingdom, war and peace | `OnCallToWarAgreementStartedEvent` | `callToWarStarted` | useful |
| Q40 | Kingdom, war and peace | `OnCallToWarAgreementEndedEvent` | `callToWarEnded` | useful |
| Q40 | Kingdom, war and peace | `OnClanEarnedGoldFromTributeEvent` | `clanTributeEarned` | useful |
| Q40 | Settlements and fiefs | `OnSettlementOwnerChangedEvent` | `settlementOwnerChanged` | useful |
| Q40 | Settlements and fiefs | `OnGovernorChangedEvent` | `governorChanged` | useful |
| Q40 | Vassal & mercenary offers and service | `OnVassalOrMercenaryServiceOfferedToPlayerEvent` | `kingdomOfferReceived` | useful |
| Q40 | Vassal & mercenary offers and service | `OnVassalOrMercenaryServiceOfferCanceledEvent` | `kingdomOfferWithdrawn` | useful |
| Q40 | Vassal & mercenary offers and service | `OnMercenaryServiceStartedEvent` | `mercenaryHired` | useful |
| Q40 | Vassal & mercenary offers and service | `OnMercenaryServiceEndedEvent` | `mercenaryLeft` | useful |
| Q40 | Marriage, children and succession | `OnMarriageOfferedToPlayerEvent` | `marriageOfferReceived` | useful |
| Q40 | Marriage, children and succession | `OnMarriageOfferCanceledEvent` | `marriageOfferCanceled` | useful |
| Q40 | Marriage, children and succession | `OnChildConceivedEvent` | `childConceived` | useful |
| Q40 | Marriage, children and succession | `OnGivenBirthEvent` | `childBorn` | useful |
| Q40 | Marriage, children and succession | `OnHeirSelectionRequestedEvent` | `heirSelectionRequested` | useful |
| Q40 | Marriage, children and succession | `OnHeirSelectionOverEvent` | `heirSelected` | useful |
| Q40 | Marriage, children and succession | `OnBeforePlayerCharacterChangedEvent` | `playerCharacterChanging` | useful |
| Q40 | Marriage, children and succession | `OnPlayerCharacterChangedEvent` | `playerCharacterChanged` | useful |
| Q40 | Marriage, children and succession | `OnClanLeaderChangedEvent` | `clanLeaderChanged` | useful |
| Q40 | Clan lifecycle and state | `DailyTickClanEvent` | `dailyClanTick` | useful |
| Q40 | Clan lifecycle and state | `HourlyTickClanEvent` | `hourlyClanTick` | useful |
| Q40 | Clan lifecycle and state | `OnClanDestroyedEvent` | `clanDestroyed` | useful |
| Q40 | Clan lifecycle and state | `OnHeroChangedClanEvent` | `heroChangedClan` | useful |
| Q40 | Clan lifecycle and state | `OnClanInfluenceChangedEvent` | `clanInfluenceChanged` | useful |
| Q40 | Clan lifecycle and state | `CompanionRemoved` | `companionRemoved` | useful |
| Q40 | Hero roles, movement and bookkeeping | `OnHeroGetsBusyEvent` | `heroGotBusy` | useful |
| Q40 | Hero roles, movement and bookkeeping | `OnHeroSharedFoodWithAnotherHeroEvent` | `sharedFood` | useful |
| Q40 | Hero roles, movement and bookkeeping | `OnHeroTeleportationRequestedEvent` | `heroTeleportRequested` | useful |
| Q41 | Game and campaign lifecycle (see Q8 for the full ordering) | `OnCharacterCreationIsOverEvent` | `playerCharacterCreated` | useful |
| Q41 | Quests, issues and the journal | `QuestLogAddedEvent` | `questLogAdded` | useful |
| Q41 | Quests, issues and the journal | `IssueLogAddedEvent` | `issueLogAdded` | useful |
| Q41 | Barter offers (lords trading fiefs, prisoners, gold, items) | `OnBarterAcceptedEvent` | `barterAccepted` | useful |
| Q41 | Barter offers (lords trading fiefs, prisoners, gold, items) | `OnBarterCanceledEvent` | `barterRejected` | useful |
| Q41 | Game menus and the character portrait pop-up | `CharacterPortraitPopUpOpenedEvent` | `companionPortraitOpened` | useful |
| Q41 | Tutorials, board games and trade rumors | `OnPlayerBoardGameOverEvent` | `boardGameFinished` | useful |
| Q41 | Tutorials, board games and trade rumors | `OnTradeRumorIsTakenEvent` | `tradeRumorHeard` | useful |
| Q42 | Trade, inventory and caravan transactions | `PlayerInventoryExchangeEvent` | `tradeSessionClosed` | useful |
| Q42 | Trade, inventory and caravan transactions | `OnItemsDiscardedByPlayerEvent` | `itemsDiscarded` | useful |
| Q42 | Trade, inventory and caravan transactions | `OnPlayerTradeProfitEvent` | `tradeProfit` | useful |
| Q42 | Trade, inventory and caravan transactions | `OnCaravanTransactionCompletedEvent` | `caravanTradeCompleted` | useful |
| Q42 | Trade, inventory and caravan transactions | `OnPlayerEarnedGoldFromAssetEvent` | `assetIncome` | useful |
| Q42 | Workshops, production and fief economy | `WorkshopOwnerChangedEvent` | `workshopOwnerChanged` | useful |
| Q42 | Workshops, production and fief economy | `WorkshopTypeChangedEvent` | `workshopTypeChanged` | useful |
| Q42 | Workshops, production and fief economy | `OnItemProducedEvent` | `fiefProduction` | useful |
| Q42 | Workshops, production and fief economy | `OnItemConsumedEvent` | `fiefConsumption` | useful |
| Q42 | Smithing and crafting | `OnNewItemCraftedEvent` | `itemCrafted` | useful |
| Q42 | Smithing and crafting | `CraftingPartUnlockedEvent` | `craftingPartUnlocked` | useful |
| Q42 | Smithing and crafting | `OnEquipmentSmeltedByHeroEvent` | `itemSmelted` | useful |
| Q42 | Smithing and crafting | `OnItemsRefinedEvent` | `itemsRefined` | useful |
| Q42 | Smithing and crafting | `OnCraftingOrderCompletedEvent` | `craftingOrderDone` | useful |
| Q42 | Quests, issues and incidents | `OnQuestStartedEvent` | `questStarted` | useful |
| Q42 | Quests, issues and incidents | `OnQuestCompletedEvent` | `questCompleted` | useful |
| Q42 | Quests, issues and incidents | `OnNewIssueCreatedEvent` | `issueCreated` | useful |
| Q42 | Quests, issues and incidents | `OnIssueUpdatedEvent` | `issueProgress` | useful |
| Q42 | Quests, issues and incidents | `OnIncidentResolvedEvent` | `incidentResolved` | useful |
| Q42 | Conversation persuasion | `PersuasionProgressCommittedEvent` | `persuasionChoice` | useful |

Not useful (89, UI/AI/internal/DLC-only; reasons in GAME_MAP): `NearbyPartyAddedToPlayerMapEvent`, `OnHomeHideoutChangedEvent`, `RebelliousClanDisbandedAtSettlement`, `CharacterDefeated`, `SiegeEngineBuiltEvent`, `OnSiegeEngineDestroyedEvent`, `OnSiegeBombardmentHitEvent`, `OnSiegeBombardmentWallHitEvent`, `OnPlayerPartyKnockedOrKilledTroopEvent`, `OnPartyAddedToMapEventEvent`, `OnMapEventContinuityNeedsUpdateEvent`, `OnBeforeMainCharacterDiedEvent`, `ArmyGathered`, `MapInteractableCreated`, `MapInteractableDestroyed`, `OnPartyRemovedEvent`, `OnPartySizeChangedEvent`, `MobilePartyQuestStatusChanged`, `VillageBecomeNormal`, `DailyTickSettlementEvent`, `PlayerStartedTournamentMatch`, `BeforeMissionOpenedEvent`, `OnAgentJoinedConversationEvent`, `HourlyTickPartyEvent`, `OnPartyLeftArmyEvent`, `OnPlayerArmyLeaderChangedBehaviorEvent`, `ArmyOverlaySetDirtyEvent`, `OnPartyLeaderChangeOfferCanceledEvent`, `OnPartyDisbandCanceledEvent`, `HourlyTickSettlementEvent`, `OnQuarterDailyPartyTick`, `PlayerStartRecruitmentEvent`, `OnPartyConsumedFoodEvent`, `OnMissionEndedEvent`, `MissionTickEvent`, `PlayerAgentSpawned`, `LocationCharactersAreReadyToSpawnEvent`, `LocationCharactersSimulatedEvent`, `OnMobilePartyRaftStateChangedEvent`, `OnMobilePartyNavigationStateChangedEvent`, `OnShipDestroyedEvent`, `OnShipOwnerChangedEvent`, `OnShipRepairedEvent`, `OnShipCreatedEvent`, `OnFigureheadUnlockedEvent`, `OnMapMarkerCreatedEvent`, `OnMapMarkerRemovedEvent`, `OnPlayerLearnsAboutHeroEvent`, `OnPlayerBodyPropertiesChangedEvent`, `PerkResetEvent`, `MercenaryTroopChangedInTown`, `MercenaryNumberChangedInTown`, `OnPeaceOfferResolvedEvent`, `OnHeroUnregisteredEvent`, `QuarterHourlyTickEvent`, `HourlyTickEvent`, `DailyTickEvent`, `WeeklyTickEvent`, `TickEvent`, `DailyTickHeroEvent`, `AiHourlyTickEvent`, `TickPartialHourlyAiEvent`, `OnSessionLaunchedEvent`, `OnAfterSessionLaunchedEvent`, `OnNewGameCreatedEvent`, `OnNewGameCreatedPartialFollowUpEvent`, `OnNewGameCreatedPartialFollowUpEndEvent`, `OnGameEarlyLoadedEvent`, `OnGameLoadedEvent`, `OnGameLoadFinishedEvent`, `BarterablesRequested`, `BeforeGameMenuOpenedEvent`, `GameMenuOpened`, `AfterGameMenuInitializedEvent`, `GameMenuOptionSelectedEvent`, `CharacterPortraitPopUpClosedEvent`, `CollectAvailableTutorialsEvent`, `OnTutorialCompletedEvent`, `OnItemSoldEvent`, `WorkshopInitializedEvent`, `OnCheckForIssueEvent`, `OnIssueOwnerChangedEvent`, `OnSaveStartedEvent`, `OnBeforeSaveEvent`, `OnSaveOverEvent`, `CollectMetadataEntriesEvent`, `OnConfigChangedEvent`, `OnGameOverEvent`, `OnCharacterCreationInitializedEvent`

## UNCERTAIN (details in each GAME_MAP section)
- 70 open items across Q35–Q47; none blocks this milestone. Notable: whether some UI-raised events (ransom, peace and
  marriage offers) fire only when the player inspects the notification (Q40); listener order between a mod and vanilla
  behaviors on the same tick (Q41); physical meaning of map units (Q43); no role-ended event (Q45); a companion couple
  across save/load (Q46, untested).
- Selection in game: the greeting/reply choice relies on the game evaluating our npc conditions when it reaches the
  token; untested in game (checklist 2).

## BLOCKERS
None.

## Worker
12 research tasks planned; 9 PASS on the first attempt, 2 salvaged (104: wrong paths; 109: timeout after writing the
section), 1 timeout with nothing written (108) → split into 108a/108b, both PASS. Track I was done by the orchestrator:
the worker queue was busy with Track R the whole time (92, 93, 95 were planned for the worker).

## Test checklist (human)
Deploy 0.3.0: both DLLs, `SubModule.xml`, test_companion's `dialogs.json` (coin_refuse priority), and the CLI.
1. [ ] No regression: load, talk, hire, voice + lipsync, conversations (`the_scar`, `spare_coin`, `after_scar`),
   transfers, console.
2. [ ] Selection in game. (a) Hub: give test_companion a second hub conversation with `priority: 5` → both are listed,
   the priority-5 one above. (b) Greetings: two greeting conversations, one `priority: 1` → it always opens the talk;
   with equal priority → each opens in some talks over several talks (one per talk). (c) Slot lines: two `backstory`
   lines, one with `"when": { "minRelation": 10 }, "priority": 1` → with relation ≥ 10 always that one, else the other.
3. [ ] `serve`: `dotnet AlbocariusCompanions.Cli.dll serve`, then paste the example session from
   `docs/SIMULATOR_PROTOCOL.md` (load → start → hire → hub → `the_scar` `p_scar_ask` → `p_scar_sorry` → `spare_coin`
   `p_coin_offer`): last step `kind: "end"`, player gold 400, companion gold 600, `got_coin: true`.
4. [ ] `validate tests/fixtures/shadowing/shadow_companion` → `WARNING shadowed_candidate … line 'backstory_02' … line
   'backstory_01' … is never chosen`, `0 error(s), 1 warning(s).`
