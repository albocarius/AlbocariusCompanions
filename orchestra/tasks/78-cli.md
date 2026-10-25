# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 78-cli: authoring command-line tool

## Goal
New console project `tools/AlbocariusCompanions.Cli` with two commands, `validate` and `mermaid`. Acceptance tests:
`tests/AlbocariusCompanions.Acceptance/CliTests.cs` (read first; it is the precise spec). Background:
`orchestra/SPEC_BEHAVIOR_FOUNDATION.md` §2 I6.

## Project (already created by the orchestrator — do not change the csproj, the .sln or the acceptance csproj)
- `tools/AlbocariusCompanions.Cli/AlbocariusCompanions.Cli.csproj` (net8.0 Exe, RollForward Major, references Core only),
  listed in `AlbocariusCompanions.sln` and referenced by the acceptance test project.
- `tools/AlbocariusCompanions.Cli/Program.cs` exists with a stub. Implement there (or in more files in that folder):
  `namespace AlbocariusCompanions.Cli`; `public static class CliProgram` with
  `public static int Run(string[] args, TextWriter stdout, TextWriter stderr)`; `Main` returns `CliProgram.Run(args, Console.Out, Console.Error)`.

## Commands
- `validate <path>`:
  - `<path>` is a companion folder when it contains `companion.json`: load the catalog of its parent folder
    (`CompanionCatalog.Load(parent)`) and report only issues whose `Folder` is that folder's name. Otherwise `<path>` is a
    Companions directory: `CompanionCatalog.Load(path)`, report all issues.
  - one line per issue on stdout: `ERROR <code> [<folder>] <message>` or `WARNING <code> [<folder>] <message>`, in catalog order;
    then a last line `<E> error(s), <W> warning(s).`
  - exit 1 when E > 0, else 0.
- `mermaid <companionFolder> [conversationId]`: load the catalog of the parent folder; if the companion (folder name) is not
  in the registry, print its issues (same line format as validate) to **stderr**, nothing on stdout, exit 1. Otherwise write
  `MermaidExport.Render(package, conversationId)` to stdout exactly (no extra newline), exit 0. Unknown conversation id
  (`ArgumentException` from Render) → message naming it on stderr, nothing on stdout, exit 2.
- Usage errors (no args, unknown command, missing argument) → a text containing "Usage" on stderr, nothing on stdout, exit 2.
  A path that does not exist → message on stderr, exit 2.
- Use `stdout.Write(...)` / `stdout.WriteLine(...)` (the tests set NewLine to "\n").

## Files
- Read: the acceptance test, `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs`, `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `src/AlbocariusCompanions.Core/Dialogs/MermaidExport.cs`.
- Edit/create: `tools/AlbocariusCompanions.Cli/*.cs` (not the csproj), and `docs/SCHEMA.md`: add a short "CLI" paragraph next to the
  scaffold paragraph (how to run: `dotnet run --project tools/AlbocariusCompanions.Cli -- validate module/ModuleData/Companions`
  and `-- mermaid module/ModuleData/Companions/<id> [conversation]`, exit codes).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
