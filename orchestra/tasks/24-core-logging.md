# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 24-core-logging: mod log file and exception guard

## Context
Read `orchestra/PROJECT.md` and GAME_MAP Q15 in `docs/GAME_MAP.md`: the game catches NO exception from mod code, so every
entry point the game calls must be wrapped. This task builds the testable part in Core (netstandard2.0, plain System.IO,
no TaleWorlds references). The Game layer later passes the absolute log path (Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\).

## Goal
Implement `src/AlbocariusCompanions.Core/Logging/ModLogger.cs` and `Safe.cs` (keep all public signatures):
- Line format: `yyyy-MM-dd HH:mm:ss.fff [LEVEL] message` (invariant culture) with LEVEL = INFO, WARN, ERROR; timestamps from the clock.
- `StartSession(header)`: create the folder if missing, truncate the file, write the header as an INFO line.
- `Info/Warning/Error`: append one line (create folder/file if needed). `Error(message, exception)`: line plus `exception.ToString()` (type, message, stack).
- `WriteSideFile(fileName, content)`: write/overwrite a file in the log's folder, return its full path; null on failure.
- Never throw from any member (catch IO/permission/argument exceptions; a broken path just means nothing is written).
- `Safe.Run(log, context, action)`: run; on exception log `ERROR` with the context and the exception, never rethrow.
  `Safe.Run<T>(log, context, func, fallback)`: same, returning the fallback on exception. Must not throw even if logging fails.
- Thread safety: serialize writes with a lock.
Files allowed to edit: `src/AlbocariusCompanions.Core/Logging/**`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/LoggingTests.cs` — read it first; it is the contract.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
