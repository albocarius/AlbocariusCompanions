# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 13-gm-q13-war-sails: GAME_MAP question Q13

## Goal
Answer SPEC Phase 1 question Q13 by reading the decompiled game source, and write the answer into
the `## Q13 — ...` section of `docs/GAME_MAP.md`. This is research only: no code changes.

The question (from orchestra/SPEC.md):

13. **War Sails DLC:** the DLC's module id/folder name; how to detect at runtime whether it is loaded **without referencing its assemblies**; the string ids of the skills, cultures and traits it adds; whether War Sails runs as a separate campaign/game mode from the base sandbox and how to detect which one is active; what the runtime string-id lookup returns for a missing object (null vs exception). If the DLC module was not copied, mark the whole question UNCERTAIN and continue.

Earlier sections Q1–Q5 of docs/GAME_MAP.md are VERIFIED — read them first and do not repeat them; reference them instead. Game XSD schemas are in `xmlschemas/` (cite as `xmlschemas/<Name>.xsd`). `xmlschemas/` also contains DLC-related schemas (e.g. `ShipHulls.xsd`, `MissionShips.xsd`); mention them only if relevant.

Where to start: DLC sources: `decomp/NavalDLC*`, DLLs in `~/bannerlord/dlls/Modules/NavalDLC/`. SubModule.xml is NOT available; derive the module id from the code (e.g. ModuleHelper / module name strings) and mark UNCERTAIN where needed. Runtime detection without referencing assemblies: look at `decomp/TaleWorlds.ModuleManager` (ModuleHelper.GetModules / GetActiveModules or similar) and TaleWorlds.MountAndBlade module info. Skill/culture/trait ids: search decomp/NavalDLC for `DefaultSkills`-like registrations / `Register<SkillObject>`/`new TraitObject(` etc. Missing-object lookup: `decomp/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs` GetObject<T>(string).

## Files
- Read: `decomp/**` (read-only), `xmlschemas/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q13` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q13 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q13 addressed>

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
(test `GameMapTests` for q=13 must pass).
