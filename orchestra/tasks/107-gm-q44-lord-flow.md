# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 107-gm-q44-lord-flow: GAME_MAP question Q44

## Goal
Answer Q44 by reading the decompiled game source and write it into the `## Q44 — Companion becomes a lord` section of `docs/GAME_MAP.md` (the section already exists with placeholder text; replace the placeholder). Research only, no code.
Sections Q1–Q34 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; these answers prepare event triggers (a companion reacts to something that happened) and role-aware conditions. See `orchestra/SPEC_SELECTION_AND_EVENTS.md` §2. Non-DLC sources only (TaleWorlds.*, SandBox*, StoryMode*); never NavalDLC.

The question:

How does the player turn a companion into a lord/vassal, and what does the mod need to hook?
Answer: (1) EVERY path in vanilla by which a player companion becomes a clan leader / lord (the "turn companion to lord" conversation with `turn_companion_to_lord_on_condition`, `ClanNameSelectionIsDone`, granting a fief, kingdom/king conversations, anything else) with the exact actions called (e.g. `ChangeClanLeaderAction`, `Clan.CreateCompanionToLordClan`-style methods — exact names/signatures, public?); (2) which CampaignEvents fire along the way (OnClanCreatedEvent, OnHeroChangedClanEvent, CompanionRemoved, OnSettlementOwnerChangedEvent, ...) in order; (3) whether vanilla already changes relations with other lords on this, and by how much (cite the code); (4) the exact point (event or method) where the mod should apply its own `noble`-based relation change (non-noble companion made a lord → relation penalty with other lords) — and which API to use to change relation between two lords (see Q21); (5) after the change: how to detect that a hero is a former mod companion now leading a clan (what survives: `Hero.StringId`, `Hero.Template`/`CharacterObject.OriginalCharacter`, `Occupation`, `Clan.Leader`); (6) how to ADD player options to the vanilla lord conversation for such a hero without replacing vanilla lines — which tokens/entry points vanilla lord talk uses (`lord_start`, `lord_pretalk`, `hero_main_options`, ...; see Q7, Q18, Q20), and whether our existing `hero_main_options` options (registered with a condition on the hero) would still show for a lord. Mention also what the "is now a lord" notification is.


## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q44` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q44 — Companion becomes a lord

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
