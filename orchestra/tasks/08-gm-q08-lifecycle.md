# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 08-gm-q08-lifecycle: GAME_MAP question Q8

## Goal
Answer SPEC Phase 1 question Q8 by reading the decompiled game source, and write the answer into
the `## Q8 — ...` section of `docs/GAME_MAP.md`. This is research only: no code changes.

The question (from orchestra/SPEC.md):

8. **Campaign lifecycle hooks:** which events to use for "new game created", "game loaded", "daily tick", and how campaign behaviors persist data (`SyncData` or equivalent).

Earlier sections Q1–Q5 of docs/GAME_MAP.md are VERIFIED — read them first and do not repeat them; reference them instead. Game XSD schemas are in `xmlschemas/` (cite as `xmlschemas/<Name>.xsd`).

Where to start: Start at `decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignBehaviorBase.cs` (RegisterEvents, SyncData) and `CampaignEvents` (OnNewGameCreatedEvent, OnGameLoadedEvent, DailyTickEvent, OnSessionLaunchedEvent ...). Also IDataStore.SyncData and what types can be saved (e.g. Dictionary<string, Hero>?).

## Files
- Read: `decomp/**` (read-only), `xmlschemas/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q8` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q8 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q8 addressed>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
- **UNCERTAIN**: <claim> — <what would resolve it>   (or exactly: None.)
```
Rules for the section:
- Every fact in the Answer must be backed by at least one Evidence bullet. Use the exact bullet format:
  dash, space, backticked path, space, em dash (—), space, backticked symbol, space, em dash, space, text.
- The path must be a real file (relative to repo root, starting with `decomp/` or `xmlschemas/`; or `~/bannerlord/...` for XML/DLL facts).
- The LAST identifier in the backticked symbol (e.g. `CreateSpecialHero` in `HeroCreator.CreateSpecialHero(...)`) must literally appear in that file. The test checks this.
- Never guess. If you cannot find something, write it under Uncertain as **UNCERTAIN** with what would resolve it.
- Signatures are fine; do not copy method bodies. Code blocks max 6 lines.
- Set `Status: DRAFT` (the orchestrator sets VERIFIED later).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(test `GameMapTests` for q=8 must pass).
