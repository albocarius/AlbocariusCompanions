# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 100-gm-q37-events-3: GAME_MAP question Q37

## Goal
Answer Q37 by reading the decompiled game source and write it into the `## Q37 — Campaign events catalog 3 (party, army, settlement, tournaments)` section of `docs/GAME_MAP.md` (the section already exists with placeholder text; replace the placeholder). Research only, no code.
Sections Q1–Q34 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; these answers prepare event triggers (a companion reacts to something that happened) and role-aware conditions. See `orchestra/SPEC_SELECTION_AND_EVENTS.md` §2. Non-DLC sources only (TaleWorlds.*, SandBox*, StoryMode*); never NavalDLC.

The question:

For **each** member below give one table row (group rows under `####` theme sub-headings: battle, crime, prisoners, party, settlement, kingdom/diplomacy, clan/heroes, economy, quests/issues, family, time — whichever apply):
`Member | Arguments (types, in order) | When it fires: the raising call site (Class.Method that calls CampaignEventDispatcher.Instance.OnX / the event's Invoke) and whether it fires BEFORE or AFTER the outcome is applied | How to tell the player's party / clan is involved (which argument to compare with MobileParty.MainParty / Clan.PlayerClan / Hero.MainHero) | Usefulness for companion reactions: a suggested trigger name in camelCase (e.g. "battleWon", "settlementEntered") with one line why, OR "not useful" + why (UI/AI/internal/DLC-only)`.

Members (all are `public static` members of `TaleWorlds.CampaignSystem.CampaignEvents`, file `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignEvents.cs`): HeroOrPartyTradedGold, HeroOrPartyGaveItem, PartyAttachedAnotherParty, ArmyCreated, ArmyDispersed, ArmyGathered, VillageStateChanged, SettlementEntered, AfterSettlementEntered, BeforeSettlementEnteredEvent, AlleyOwnerChanged, AlleyOccupiedByPlayer, AlleyClearedByPlayer, PlayerEliminatedFromTournament, PlayerStartedTournamentMatch, TournamentStarted, TournamentFinished, TournamentCancelled, MobilePartyDestroyed, MobilePartyCreated, MapInteractableCreated, MapInteractableDestroyed, MobilePartyQuestStatusChanged, AfterMissionStarted, VillageBecomeNormal, OnAgentJoinedConversationEvent, ConversationEnded, OnMissionStartedEvent, BeforeMissionOpenedEvent, OnPartyRemovedEvent, OnPartySizeChangedEvent, OnSettlementLeftEvent, DailyTickPartyEvent, DailyTickTownEvent, DailyTickSettlementEvent, OnBuildingLevelChangedEvent, HourlyTickPartyEvent

How: the argument types are in the `IMbEvent<...>` declaration in CampaignEvents.cs. The raising method is `CampaignEvents.OnX(...)`-style (same file, called by `CampaignEventDispatcher`); find who calls the dispatcher method with grep -rn "\.OnX(" decomp/TaleWorlds.CampaignSystem decomp/SandBox* decomp/StoryMode* (never NavalDLC; if the only raiser is NavalDLC say "DLC only, not useful"). Q23 and Q32 already cover some members (battle, war, settlement, kingdom, death, daily ticks): reuse them with "see Q23" instead of re-researching. One Evidence bullet per raising call site you cite is enough; for the declaration itself one bullet for CampaignEvents.cs.
Work incrementally: write the first ten rows early and run the check, then extend — a partial table that passes beats nothing at the time limit. If you run out of time, list the members you did not reach under Uncertain.


## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q37` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q37 — Campaign events catalog 3 (party, army, settlement, tournaments)

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
