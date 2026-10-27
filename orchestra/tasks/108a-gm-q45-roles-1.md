# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 108a-gm-q45-roles-1: GAME_MAP question Q45

## Goal
Answer Q45 by reading the decompiled game source and write it into the `## Q45 — Companion roles 1 (governor, party leader, caravan leader)` section of `docs/GAME_MAP.md` (the section already exists with placeholder text; replace the placeholder). Research only, no code.
Sections Q1–Q34 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; these answers prepare event triggers (a companion reacts to something that happened) and role-aware conditions. See `orchestra/SPEC_SELECTION_AND_EVENTS.md` §2. Non-DLC sources only (TaleWorlds.*, SandBox*, StoryMode*); never NavalDLC.

The question:

For each role — **governor** of a town/castle, **party leader** (leads its own clan party) and **caravan leader** — answer: (1) how to check whether a hero currently has the role (exact public members, e.g. `Hero.GovernorOf`, `Hero.PartyBelongedTo.LeaderHero == hero`, `MobileParty.IsCaravan`, `Alley.Owner`/`Hero.OwnedAlleys`, `IssueBase.AlternativeSolutionHero` / `Hero.IsActive`/`DisableHeroAction` state — whatever exists); (2) the events (CampaignEvents members, or polling if none) fired on assignment, on removal, and when the role ends on its own (caravan destroyed or disbanded, alley lost/cleared, governor removed by siege/capture, alternative solution succeeded / failed / cancelled — and where the companion returns); (3) whether the hero is still in `Clan.PlayerClan.Companions` and in `MobileParty.MainParty` while in the role. Reuse Q34 rows (roles were listed as decide items there) and Q23/Q32 events.


Keep it compact: one sub-heading per role, a short table of members/events, cite only what you checked. Write the section early and extend it; the time limit is 60 minutes.

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q45` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q45 — Companion roles 1 (governor, party leader, caravan leader)

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
