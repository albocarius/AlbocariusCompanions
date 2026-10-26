# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 109-gm-q46-marriage: GAME_MAP question Q46

## Goal
Answer Q46 by reading the decompiled game source and write it into the `## Q46 — Marriage between heroes` section of `docs/GAME_MAP.md` (the section already exists with placeholder text; replace the placeholder). Research only, no code.
Sections Q1–Q34 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; these answers prepare event triggers (a companion reacts to something that happened) and role-aware conditions. See `orchestra/SPEC_SELECTION_AND_EVENTS.md` §2. Non-DLC sources only (TaleWorlds.*, SandBox*, StoryMode*); never NavalDLC.

The question:

Answer: (1) the vanilla marriage action(s) (`MarriageAction.Apply...` — exact signatures, public?) and the conditions the game checks (`Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage` etc.: age, existing spouse, clan, kingdom, gender, relatives); (2) whether two companions in the player's clan can marry each other under vanilla rules (companions' clan is the player clan? Q34 says mod companions may have `Hero.Clan == null` — check what `IsCoupleSuitableForMarriage` and the action do with that); (3) consequences of the action: clan membership of the spouse, `Hero.Spouse`, events fired (`BeforeHeroesMarried`, ...), children/pregnancy (`PregnancyCampaignBehavior`, conditions), inheritance; (4) the vanilla courtship flow (`RomanceCampaignBehavior`, `Romance.RomanticState`, `RomanticStateChanged`, persuasion) versus calling the action directly — what the mod can skip safely; (5) side effects on the player (relation changes, notifications, the player's own marriage options, `OnMarriageOfferedToPlayerEvent`).


## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q46` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q46 — Marriage between heroes

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
