# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 85-gm-q34-companion-audit: GAME_MAP question Q34

## Goal
Answer Q34 by reading the decompiled game source and write it into the `## Q34 — Vanilla behaviors acting on companions (audit)` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q32 are VERIFIED — reuse them (reference "see Q7" etc.) instead of repeating. Game version: Bannerlord 1.4.8.
Background: the mod (AlbocariusCompanions) adds data-driven companions with authored conversations; the answers prepare a behavior system (variables, conditions, effects, later event triggers). See `orchestra/SPEC_VANILLA_GUARDS.md` §0–§1.

The question:

**List every vanilla campaign behavior, action and game model (non-DLC sources only: TaleWorlds.*, SandBox*, StoryMode*) that targets player companions or wanderers — code that checks `IsPlayerCompanion`, `CompanionOf`, `Occupation.Wanderer`, `IsWanderer`, `Clan.Companions`/`PlayerClan.Companions`, or otherwise acts on heroes because they are companions/wanderers. For each give one table row: class.member | what it does | trigger | consequence for the hero | classification. Classification rules: **keep** = mechanical and neutral (movement, combat, party roles, equipment, skills, placement, hiring UI the mod already handles); **exclude** = it removes, kills, disables or makes the companion leave on its own logic, OR makes the companion speak vanilla text (comments, requests, notifications in its name); **decide** = unclear or a gameplay choice (marriage, becoming a lord/governor/party leader, fiefs, leaving at low morale, caravans, etc.). For every exclude row also give the narrowest member a Harmony prefix could target (exact signature). Cite evidence for each row. Q18 (dialog points), Q19 (dismissal), Q21 (relation removal), Q33 (grievances) are already mapped: reference them as rows instead of re-researching.**

Where to start: grep -rln `IsPlayerCompanion\\|CompanionOf\\|Occupation.Wanderer\\|IsWanderer\\|\\.Companions` in decomp/TaleWorlds.CampaignSystem, decomp/SandBox*, decomp/StoryMode* (exclude NavalDLC), then read each hit in context. Work incrementally: write the table with the first rows early and run the check, then extend — a partial table that passes beats nothing at the time limit.

## Files
- Read: `decomp/**` (read-only), `~/bannerlord/moduledata_win/**` (read-only vanilla XML), `docs/GAME_MAP.md`.
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q34` section (between its header and the next `## ` header). Do not touch other sections.
- Search with grep/rg on the decomp folder. Read files in parts; do not read whole huge files.

## Required section format (an acceptance test checks it)
```
## Q34 — Vanilla behaviors acting on companions (audit)

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
(test `GameMapTests` for q=34 must pass).
