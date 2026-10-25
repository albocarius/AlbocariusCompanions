# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 61-gm-q24-traits: GAME_MAP question Q24

## Goal
Answer Q24 by reading the decompiled game source and write it into the `## Q24 — Player traits` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q20 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; the answers prepare a behavior system (variables, conditions, effects, later event triggers). See `orchestra/SPEC_BEHAVIOR_FOUNDATION.md` §1.

The question:

**Where trait XP/level changes happen (is there a single hook point / event?), how to read current trait levels of the player (API), which player actions change which trait (table: action → trait → where in decomp).**

Where to start: grep `TraitLevelingHelper\|GetTraitLevel\|SetTraitLevel\|OnPlayerTraitChanged\|DefaultTraits` in decomp; start with `TraitLevelingHelper` (grep its file).

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q24` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q24 — Player traits

Status: DRAFT

### Answer
<plain-language answer, every sub-question addressed; tables welcome>

### Evidence
- `decomp/<path to .cs file>` — `Class.Member` — <what this shows, one line>

### Uncertain
- **UNCERTAIN**: <claim> — <what would resolve it>   (or exactly: None.)
```
Rules for the section:
- Every fact in the Answer must be backed by at least one Evidence bullet. Exact bullet format:
  dash, space, backticked path, space, em dash (—), space, backticked symbol, space, em dash, space, text.
- The path must be a real file (relative to repo root starting with `decomp/`, or `~/bannerlord/...` for XML/DLL facts).
- The LAST identifier in the backticked symbol must literally appear in that file. The test checks this.
- Give exact signatures (parameter types) for every API you recommend. Say whether it is public.
- Never guess. If you cannot find something, write it under Uncertain as **UNCERTAIN** with what would resolve it.
- Signatures and short quotes are fine; do not copy method bodies. Code blocks max 6 lines.
- Set `Status: DRAFT` (the orchestrator sets VERIFIED later).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(test `GameMapTests` for q=24 must pass).
