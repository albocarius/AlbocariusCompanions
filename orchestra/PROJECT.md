# Project map — AlbocariusCompanions

Bannerlord mod: Warband-style unique companions, defined purely by data (JSON).
The request is `orchestra/SPEC.md` (read it for rules; §1 Hard rules are binding).

## Stack
- Target game version: Bannerlord 1.4.8. Harmony: Lib.Harmony 2.4.2 (compile-only; runtime from the Bannerlord.Harmony module).
- .NET SDK 10.0.402 from `~/.dotnet-ms` (Microsoft build; set via orchestra/config.env). The distro SDK 10.0.113 crashes intermittently — do not use it. Game runs on a separate Windows machine; nobody can run it here.
- `src/AlbocariusCompanions.Core` — netstandard2.0, **no TaleWorlds references**. Models, JSON loading,
  registry, validation, slots, line-id rules. Everything testable lives here.
- `src/AlbocariusCompanions.Game` — net472, references Core + game DLLs (HintPath, Private=false).
  Thin layer: SubModule, campaign behavior, Harmony patches, dialog registration, logging.
- `tests/AlbocariusCompanions.Core.Tests` — net10.0 xunit, unit tests for Core (worker may add tests here).
- `tests/AlbocariusCompanions.Acceptance` — net10.0 xunit, orchestrator-owned acceptance tests. **Worker must not edit.**
- `module/` (to be created) — SubModule.xml + ModuleData/Companions/<id>/{companion.json,dialogs.json}.
- `docs/GAME_MAP.md` — every TaleWorlds API used in code must be listed here with its decomp source file.
- `docs/BLOCKERS.md` — unresolved problems (error, attempts, best guess).
- `dist/` — post-build output in Bannerlord module layout (gitignored).

## Commands
- Build+test: `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
- Baseline (2026-10-24, commit eff1e6a): build OK, 0 tests in both test projects, exit 0.

## External, read-only paths (never commit, never copy into repo)
- Game DLLs: `~/bannerlord/dlls/bin/*.dll`, `~/bannerlord/dlls/Modules/<Native|SandBoxCore|SandBox|StoryMode|CustomBattle|NavalDLC>/...`
  - `Newtonsoft.Json.dll` exists in `~/bannerlord/dlls/bin`.
- Vanilla XML: `~/bannerlord/moduledata/{SandBox,SandBoxCore}` (partial) and `~/bannerlord/moduledata_win/` = full XML/XSD of
  Native, SandBoxCore, SandBox, StoryMode, NavalDLC, CustomBattle + every SubModule.xml + Bannerlord.Harmony's SubModule.xml,
  copied from the user's live install (game v1.4.8). Read-only, never commit. Prefer it over the partial copy.
- Decompiled source: `decomp/` (symlink to `~/bannerlord/decomp`). Contains TaleWorlds.CampaignSystem, .Core,
  .MountAndBlade, .ObjectSystem, SandBox*, StoryMode*, NavalDLC*. **Not** decompiled: TaleWorlds.Localization, TaleWorlds.Engine, TaleWorlds.ModuleManager — use `ilspycmd` on the DLL into a
  scratch dir outside the repo if needed.
- XSD schemas: `xmlschemas/` (symlink to `~/bannerlord/xmlschemas`, gitignored, read-only) = the game's `XmlSchemas`
  folder. The game validates each module XML listed under `Xmls/XmlNode` in SubModule.xml against `XmlSchemas/<id>.xsd`
  (`ModuleHelper.GetXsdPath`). Use these as the format reference (e.g. `NPCCharacters.xsd`, `GameText.xsd`).
- NavalDLC = War Sails DLC. Never reference its assemblies or types.

## Conventions
- Namespaces `AlbocariusCompanions.Core.*` / `AlbocariusCompanions.Game.*`.
- Nullable enabled, LangVersion latest. Validation returns error/warning lists, never throws.
- Commits: `phase N: <summary>` for SPEC phases; orchestra commits per ORCHESTRATOR.md.

## Must not break
- Core must stay free of TaleWorlds references (it is consumed by net10.0 tests).
- No decompiled TaleWorlds code in the repo (summaries/signatures only, in GAME_MAP).
- No DLC assembly references anywhere.

## Environment notes
- Windows test machine (game install, Bannerlord.Harmony v2.4.2.248 from Workshop): `ssh aliboron@100.94.114.51` (key via ssh-agent).
  Game: `C:\GAMES\steamapps\common\Mount & Blade II Bannerlord`. Mod log folder: `Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\`.
  Native logs: `C:\ProgramData\Mount and Blade II Bannerlord\logs\`. Never write to the game folder without asking the user.
- (Resolved) the distro dotnet 10.0.113 intermittently aborted at CLI startup with `Internal CLR error (0x80131506)` (~10-15% of runs, not
  code-related; RAM is mostly taken by the local model server). Not fixed by disabling node reuse, a GC heap limit or
  W^X. check_worker.sh retries up to 3 times on that message; when running TEST_CMD by hand, just rerun.
- `orchestra/logs/` is gitignored; the rest of `orchestra/` is tracked.
- Code graph: `graphify-out/` (git-excluded), refresh with `orchestra/graph.sh update`.
