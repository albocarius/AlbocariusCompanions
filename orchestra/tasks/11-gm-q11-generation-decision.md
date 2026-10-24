# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 11-gm-q11-generation-decision: GAME_MAP question Q11

## Goal
Answer SPEC Phase 1 question Q11 by reading the decompiled game source, and write the answer into
the `## Q11 — ...` section of `docs/GAME_MAP.md`. This is research only: no code changes.

The question (from orchestra/SPEC.md):

11. **Generation decision (from Q5):** state explicitly whether companions can be created at runtime directly from JSON data, or whether a native XML template must be generated at build time (rule 8). Recommend one, with reasons.

Earlier sections Q1–Q5 of docs/GAME_MAP.md are VERIFIED — read them first and do not repeat them; reference them instead. Game XSD schemas are in `xmlschemas/` (cite as `xmlschemas/<Name>.xsd`). Use `xmlschemas/NPCCharacters.xsd` for the template format if XML generation is recommended.

Where to start: Combine Q4 and Q5. State clearly: can a hero be created at runtime purely from JSON data (no XML template), or must a native XML character template be generated at build time? Give the concrete API path for the recommended option with evidence.

## Files
- Read: `decomp/**` (read-only), `xmlschemas/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q11` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q11 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q11 addressed>

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
(test `GameMapTests` for q=11 must pass).
