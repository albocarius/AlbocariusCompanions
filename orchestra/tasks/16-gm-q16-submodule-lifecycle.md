# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 16-gm-q16-submodule-lifecycle: GAME_MAP question Q16

## Goal
Answer Q16 by reading the decompiled game source and write it into the `## Q16 — ...` section of `docs/GAME_MAP.md`. Research only, no code.
Sections Q1–Q14 are VERIFIED — read the relevant ones and reference them instead of repeating. Game XSD schemas are in `xmlschemas/`.

The question:

**SubModule.xml and MBSubModuleBase lifecycle** (needed by M3 tasks 23 and 30): (a) the SubModule.xml format as parsed by the game (`ModuleInfo`/`SubModuleInfo` loading): root element, `Id`, `Name`, `Version` format (e.g. `v0.1.0`), `DependedModules`/`DependedModuleMetadatas` (load order, optional deps), `SubModules/SubModule` children (`Name`, `DLLName`, `SubModuleClassType`, `Tags`), and `Xmls`; (b) the exact module ids of the vanilla modules a campaign mod depends on (Native, SandBoxCore, Sandbox, StoryMode) and of `Bannerlord.Harmony` if it can be found; (c) the `MBSubModuleBase` overrides in call order (`OnSubModuleLoad`, `OnBeforeInitialModuleScreenSetAsRoot`, `OnGameStart(Game, IGameStarter)`, `InitializeGameStarter`, `RegisterSubModuleObjects`, `AfterRegisterSubModuleObjects`, `OnGameEnd`...) and which one vanilla uses to add campaign behaviors (`CampaignGameStarter.AddBehavior`); (d) where the DLL must sit (`bin/Win64_Shipping_Client`) and how the class is located.

Where to start: Start at `decomp/TaleWorlds.ModuleManager/TaleWorlds.ModuleManager/ModuleInfo.cs` and `SubModuleInfo.cs`, `decomp/TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/MBSubModuleBase.cs`, `Module.cs` (LoadSubModules), `MBGameManager.cs`, and a vanilla example such as `decomp/SandBox/SandBox/SandBoxSubModule.cs`. Vanilla SubModule.xml files are not in the local copy; derive the format from the parser. Bannerlord.Harmony is a community module: if its id cannot be found locally, mark UNCERTAIN (commonly `Bannerlord.Harmony`).

## Files
- Read: `decomp/**` (read-only), `xmlschemas/**` (read-only), `~/bannerlord/moduledata/**` (read-only), `~/bannerlord/dlls/**` (read-only), `docs/GAME_MAP.md` (earlier sections may help).
- Edit: `docs/GAME_MAP.md` — ONLY the `## Q16` section (between its header and the next `## Q` header). Do not touch other sections.
- Search with grep/rg on the decomp folder, e.g. `grep -rn "IsWanderer" decomp/TaleWorlds.CampaignSystem | head -50`. Read files in parts.

## Required section format (an acceptance test checks it)
```
## Q16 — <keep existing title>

Status: DRAFT

### Answer
<plain-language answer, every sub-question of Q16 addressed>

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
