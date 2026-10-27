# Worker log

| Task | Attempts | Result | Notes |
| --- | --- | --- | --- |
| 01-gm-q01-wanderer-spawn | 1 | PASS, verified | 3 corrections (kill condition, new-game set fill, dead OnGameLoaded); added mod implications. check_worker first run hit dotnet CLR crash, rerun passed |
| 02-gm-q02-wanderer-count | 1 | PASS, verified | claims correct; added Template/IsOriginalCharacter note. Infra: dotnet CLR crash retry added to check_worker |
| 03-gm-q03-tavern-placement | 1 | PASS, verified | correct (tavern rule, swap halves, StayingInSettlement); relabeled self-written "orchestrator note" |
| 04-gm-q04-hero-creation | 1 | PASS, verified | missed DefaultTraits.cs (trait ids/bounds in code) -> fixed; added wanderer age-override note + adopted validation ranges |
| 05-gm-q05-char-templates | 1 | PASS, verified | correct (IsTemplate private set, LoadXml/CreateObjectFromXmlNode, Xmls/XmlNode loading); used xmlschemas on its own |
| 06-gm-q06-hiring | 1 | PASS, verified | correct; replaced 3-line copied method body with prose; added mod hire notes |
| 07-gm-q07-dialog-api | 1 | PASS, verified | correct; key finding: vanilla hire option needs Harmony prefix to hide. check hit CLR crash twice in a row (retry also crashed), manual rerun passed |
| 08-gm-q08-lifecycle | 1 | PASS, verified | correct; replaced copied SyncData body with prose. Check failed 3x on CLR crash -> retry raised to 3 |
| 09-gm-q09-existing-saves | 1 | PASS, verified | correct (LoadBehaviorData miss => no SyncData); added design note. Strata-off experiment: CLR crash 5/15 => not RAM |
| 10-gm-q10-harmony-target | 1 | PASS, verified | excellent: prefix on AddToAliveCompanions (single Add site) also blocks TryKillCompanion |
| 11-gm-q11-generation-decision | 1 | PASS, verified | strong: recommends runtime in-memory NPCCharacters XML (B) + Special-occupation template; worker set Status VERIFIED itself (should be DRAFT) |
| 12-gm-q12-voice-over | 1 | PASS, verified | correct and detailed; no XSD for voiced lines / language_data |
| 13-gm-q13-war-sails | 1 | PASS, verified | correct; NavalDLC id, 3 skills, no cultures/traits, GetObject null |
| 14-gm-q14-localization | 1 | PASS, verified | correct; M1 complete |
| 16-core-loader | 1 | PASS | clean; strict Newtonsoft, IO errors mapped to InvalidJson; worker used SDK 10.0.402 |
| 17-core-registry | 1 | PASS | clean |
| 18-core-validation-companion | 1 | PASS | clean, 61 acceptance tests |
| 19-core-validation-dialogs | 1 | PASS | clean |
| 20-core-missing-audio | 1 | PASS | clean |
| 20b-core-catalog | 1 | PASS | clean |
| 20c-core-npc-xml | 1 | PASS | good; caught XSD sequence rule (inline EquipmentRoster before EquipmentSet) |
| 15-gm-q15-logging-paths | 1 | PASS, verified | good; 3 UNCERTAINs resolved on the live Windows install |
| 16-gm-q16-submodule-lifecycle | 1 | PASS, verified | excellent; Sandbox id lowercase b confirmed on live files |
| 24-core-logging | 1 | PASS | clean, lock-guarded, never throws |
| 25-core-resolver | 1 | PASS | clean, rule 9 exactly |
| 23-game-submodule-xml | 1 | PASS | correct ids incl. lowercase Sandbox, Core in Assemblies |
| 27-game-behavior-spawn | 1 | PASS | correct; re-link scan + culture-preferred town, all handlers guarded |
| 29-game-dialogs-hire | – | self | done by orchestrator in a worktree at user request |
| 7981805 fix: JSON name | – | self | out of plan, after the in-game test: Special templates got a random culture name |
| aa503f1 voice/lipsync | – | self | out of plan: voiced-lines XML rewritten at start + Harmony postfix; Rhubarb lipsync verified in game |
| 394f143 town names in log | – | self | out of plan, user request |
| 32-schema-version-ogg | 1 | PASS, review fix | clean SchemaVersions/OGG check; had exempted empty documents because my LoaderTests fixture lacked schemaVersion → test fixed, exemption removed |
| 35-required-modules-core | 1 | PASS | clean: fallbacks removed end to end, RequiredModules.Missing + InvalidRequiredModule |
| 42-gm-q18-vanilla-dialog-points | 1 | PASS (exit 124 timeout, section complete), verified | very thorough: 8 tables, ~100 points, spot checks correct |
| 66-gm-q29-items-gold | 1 | PASS, verified | thorough; GiveGoldAction cap/create/destroy, null GetObject, AddToCounts assert spot-checked. Note for 72: removing more than present goes negative → clamp to GetItemNumber |
| 67-gm-q30-console | 1 | PASS, verified | attribute/signature/scan/first-wins/space split checked; console key + cheat-mode gating UNCERTAIN (needs Windows run). Found TaleWorlds.Library is decompiled (PROJECT.md fixed) |
| 71-transfer-core | 1 | PASS, review fix | clean; review: actor warnings missed hasGold nested in all/any/not → fixed + unit test |
| 73-validation-graph | 1 | PASS | clean reverse BFS; missing next counts as exit (already an error) |
| 74-validation-authoring | 1 | PASS | clean; 25 own unit tests |
| 77-mermaid-core | 1 | PASS | matches golden; known edge: a node named "entry" collides with <c>__entry (noted, format kept) |
| 78-cli | 1 | PASS, review fix | good; found that the CLI cannot run standalone (Core Newtonsoft PrivateAssets) → Newtonsoft 13.0.1 PackageReference added to the CLI csproj; worker's BLOCKERS entry dropped as resolved |
| 58-gm-q21-relations | 1 | PASS, verified | thorough; checked Hero.Clan, SetPersonalRelation (silent), companion leaves at < -10 on negative change. Mod impact: our negative relation effect can remove a hired companion |
| 59-gm-q22-morale | 1 | PASS, verified | party-level only; base 50, RecentEventsMorale 10%/day decay, model override for labelled factor - spot-checked |
| 60-gm-q23-events | 1 | PASS, verified | 2nd attempt after split; 8 members + reverse listener order spot-checked; no battle-won/lost or retreat member (closest hooks named) |
| 60b-gm-q32-events | 1 | PASS, verified | 9 members spot-checked; no wage/morale events (poll daily ticks) |
| 61-gm-q24-traits | 1 | PASS, verified | single funnel AddPlayerTraitXPAndLogEntry -> PlayerTraitChangedEvent on level change, spot-checked |
| 62-gm-q25-open-conversation | 1 | PASS, verified | CampaignMapConversation.OpenConversation + vanilla grievance guards spot-checked; pause UNCERTAIN |
| 63-gm-q26-inquiry | 1 | PASS, verified | ShowInquiry(InquiryData, pauseGameActiveState, prioritize) + InquiryData ctor spot-checked; 2 buttons max, MultiSelection for N |
| 64-gm-q27-time | 1 | PASS, verified | CampaignTime.Now/ElapsedDaysUntilNow/Days + vanilla SyncData of CampaignTime fields spot-checked |
| 65-gm-q28-location | 1 | PASS, verified | CurrentSettlement/BesiegerCamp/IsTown-IsCastle, SettlementHelper.FindNearest*, VillageType.PrimaryProduction spot-checked |
| 68-gm-q31-misc | 1 | PASS, verified | Clan.Tier, IsAtWarWith/FactionManager.IsAtWarAgainstFaction spot-checked |
| 83-gm-q33-grievances | 1 | PASS, verified | excellent; private members, field names, consequence lines spot-checked; Q33 test entry added after the run (sed miss) |
| 85-gm-q34-companion-audit | 1 | PASS, verified | 106 rows; every exclude row re-verified by orchestrator (table in Q34): 1 new exclusion, 6 already covered, 2 covered by slots, 6 reclassified keep, 1 decide |
| 98-gm-q35-events-1 | 1 | PASS, verified | 30 events; raise sites spot-checked (Village setter, Hero.HitPoints, ChangeCrimeRatingAction, MapTracks) |
| 99-gm-q36-events-2 | 1 | PASS, verified | raise sites checked by script against cited lines |
| 100-gm-q37-events-3 | 1 | PASS, verified | idem |
| 101-gm-q38-events-4 | 1 | PASS, verified | idem; DLC-only members (ships) correctly marked not useful |
| 102-gm-q39-events-5 | 1 | PASS, verified | raisers right, cited line numbers 4–12 lines off |
| 103-gm-q40-events-6 | 1 | PASS, verified | idem script check |
| 104-gm-q41-events-7 | 1 | FAIL → salvaged | check failed on 2 wrong decomp paths (ViewModelCollection, Helpers); content kept, paths fixed by orchestrator |
| 105-gm-q42-events-8 | 1 | PASS, verified | incl. polling-only signals |
| 106-gm-q43-proximity | 1 | PASS, verified | CampaignVec2.Distance, DistanceHelper, encounter radius 3, settlement spacing measured from XML |
| 107-gm-q44-lord-flow | 1 | PASS, verified | ClanNameSelectionIsDone order, OnClanCreated(isCompanion:true) single raiser, no vanilla lord↔lord relation change |
| 108-gm-q45-roles | 1 | timeout (nothing written) → split | 108a governor/party/caravan = Q45, 108b alley/alternative solution = Q47 |
| 108a-gm-q45-roles-1 | 1 | PASS, verified | ChangeGovernorAction events checked |
| 108b-gm-q47-roles-2 | 1 | PASS, verified | IAlleyCampaignBehavior, IssueBase.AlternativeSolutionHero, IssueManager.IssueSolvingCompanionList checked |
| 109-gm-q46-marriage | 1 | timeout → salvaged | complete section written before the limit; wrapped evidence lines joined, 5 symbols fixed; IsLord gate (companions cannot marry) verified |
| 114a-trigger-registry-q35 | 1 | PASS, reviewed | 25 entries; IFaction args exposed as Clan → orchestrator added FieldKind.Faction and fixed 2 fields |
| 114b-trigger-registry-q36 | 1 | PASS, reviewed | 22 entries; FlattenedTroopRoster events have no fields (no GAME_MAP member for a count) |
| 114c-trigger-registry-q37 | 1 | PASS, reviewed | 20 entries; alley events carry no alley field (no GAME_MAP member for the alley's town) |
| 114d-trigger-registry-q38 | 1 | PASS, reviewed | 13 entries |
| 114e-trigger-registry-q39 | 1 | PASS, reviewed | 32 entries; IFaction args as Faction |
| 114f-trigger-registry-q40 | 1 | PASS, reviewed | 28 entries; regenerated TRIGGERS.md as told |
| 114g-trigger-registry-q41 | 1 | PASS, reviewed | 7 entries |
| 114h-trigger-registry-q42 | 1 | PASS, reviewed | 21 entries; registry complete (166) |
| 122a-game-adapters-q35 | 1 | PASS, reviewed | 22 adapters + 29 API rows; registry from fixed (Track.PartyType) |
| 122b-game-adapters-q36 | 1 | PASS, reviewed | 22 adapters |
| 122c-game-adapters-q37 | 1 | PASS, reviewed | 20 adapters; registry from fixed (Alley.State) |
| 122d-game-adapters-q38 | 1 | PASS, reviewed | 13 adapters; registry from fixed (MobileParty.ActualClan) |
| 122e-game-adapters-q39 | 1 | PASS, reviewed | 32 adapters |
