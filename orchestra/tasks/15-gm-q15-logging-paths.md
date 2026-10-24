# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 15-gm-q15-logging-paths: GAME_MAP question Q15

## Goal
Answer Q15 by reading the decompiled game source and write it into the `## Q15 — ...` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q14 are VERIFIED — read the relevant ones and reference them instead of repeating. Game XSD schemas are in `xmlschemas/`.

The question:

**Logging, writable paths, exception safety** (needed by M3 tasks 24 and 26): (a) which folder a mod can safely write its own log file and a debug copy of generated XML to on Windows — e.g. the game's Documents folder (`Mount and Blade II Bannerlord/...`), `BasePath`, configs/logs paths exposed by TaleWorlds.Library/TaleWorlds.Engine (`Utilities`, `Debug`, `PlatformFileHelper`...), or the module folder itself — with the exact API that returns the path and whether it works in both Steam and other installs; (b) how vanilla logs (`Debug.Print`, `MBDebug`, rgl log) and whether a mod may use it; (c) whether the game catches exceptions thrown from `MBSubModuleBase` overrides, campaign event handlers and dialog condition/consequence delegates, or whether such an exception crashes the game.

Where to start: Start at `decomp/TaleWorlds.Library/TaleWorlds.Library/BasePath.cs`, `decomp/TaleWorlds.Library/TaleWorlds.Library/Debug.cs`, search `decomp/TaleWorlds.Library` and `decomp/TaleWorlds.Engine` for `GetConfigsPath`, `Documents`, `MyDocuments`, `PlatformFileHelper`, `rgl_log`. For (c), look at how `MBGameManager`/`Module` call submodule overrides and how `MbEvent`/`ConversationSentence.RunCondition` invoke delegates (try/catch or not).

## Files
- Read: `decomp/**` (read-only), `xmlschemas/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q15` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q15 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q15 addressed>

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
