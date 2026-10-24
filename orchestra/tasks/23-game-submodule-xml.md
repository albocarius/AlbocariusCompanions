# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 23-game-submodule-xml: module/SubModule.xml

## Context
Read `orchestra/PROJECT.md` and GAME_MAP Q16 in `docs/GAME_MAP.md` (format as the game parses it). Real vanilla examples:
`~/bannerlord/moduledata_win/SandBox/SubModule.xml`, `~/bannerlord/moduledata_win/Bannerlord.Harmony/SubModule.xml`.
The build copies `module/` into `dist/AlbocariusCompanions/` (already working).

## Goal
Create `module/SubModule.xml`:
- Root `<Module>` with `Name` (value "Albocarius's Companions"), `Id` "AlbocariusCompanions", `Version` "v0.1.0",
  `DefaultModule` "false", `ModuleCategory` "Singleplayer", `ModuleType` "Community" — each as `<X value="..."/>`.
- `<DependedModules>` with `<DependedModule Id="..."/>` for exactly, in order: Bannerlord.Harmony, Native, SandBoxCore, Sandbox
  (note the lowercase b in "Sandbox"; no Optional attribute).
- `<SubModules>` with one `<SubModule>`: `Name` "AlbocariusCompanions", `DLLName` "AlbocariusCompanions.Game.dll",
  `SubModuleClassType` "AlbocariusCompanions.Game.SubModule", `<Assemblies><Assembly value="AlbocariusCompanions.Core.dll"/></Assemblies>`,
  and `<Tags/>`.
- An empty `<Xmls/>` (templates are generated at runtime, SPEC rule 8 amendment).
- UTF-8, 2-space indentation, no comments needed. Do not create the SubModule class (another task).
Files allowed to edit: `module/SubModule.xml` (new).

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/SubModuleXmlTests.cs` — read it first.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
