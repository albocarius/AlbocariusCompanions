# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 106-gm-q43-proximity: GAME_MAP question Q43

## Goal
Answer Q43 by reading the decompiled game source and write it into the `## Q43 — Proximity trigger` section of `docs/GAME_MAP.md` (the section already exists with placeholder text; replace the placeholder). Research only, no code.
Sections Q1–Q34 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; these answers prepare event triggers (a companion reacts to something that happened) and role-aware conditions. See `orchestra/SPEC_SELECTION_AND_EVENTS.md` §2. Non-DLC sources only (TaleWorlds.*, SandBox*, StoryMode*); never NavalDLC.

The question:

How can the mod detect "the player party is approaching settlement X" (no event is expected for this)?
Answer: (1) which tick to use — hourly (`HourlyTickEvent`/`HourlyTickPartyEvent`), `QuarterHourlyTickEvent`, `TickEvent` (per frame) or the AI partial-hourly tick — what each costs and how often it fires in campaign time; (2) how to compute the map distance between `MobileParty.MainParty` and a `Settlement` (exact members: positions such as `Position2D`/`GetPosition2D`/`CampaignVec2`, `Distance`/`DistanceSquared`, or a path-distance model such as `Campaign.Current.Models.MapDistanceModel.GetDistance(...)` — exact signatures, public?, units, cost); (3) typical distances to give a sensible radius scale: a town's gate/visual radius, the distance a party travels per hour at normal speed (`MobileParty.Speed` units per hour), and typical distance between neighbouring towns (cite vanilla constants or models if any); (4) a recommended hysteresis design: enter radius R → fire once → re-arm only after leaving a larger radius R2 — and what state must be saved for it to survive save/load (Q20/Q27 save patterns); (5) how to find a settlement by string id (e.g. `Settlement.Find`) and how to tell when the player is already inside it (Q28). Worked example: a companion from Onira (a vanilla town; find its settlement string id in `~/bannerlord/moduledata_win/SandBox/settlements.xml` or similar) speaks when the party nears Onira.


## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q43` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q43 — Proximity trigger

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
