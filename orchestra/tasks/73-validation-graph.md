# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 73-validation-graph: conversation nodes that can never reach an exit

## Goal
Add one validation rule to Core: in a conversation, a node from which no path along `next` leads to a node whose
`next` is `["hub"]` or `["end"]` is an **error** (the conversation would loop forever there).
Acceptance tests: `tests/AlbocariusCompanions.Acceptance/Core/NoExitTests.cs` (read it first, it is the precise spec).

## Rules
- New code `IssueCodes.ConversationNoExit = "conversation_no_exit"` in `src/AlbocariusCompanions.Core/Validation/IssueCodes.cs`
  (in the "dialogs.json conversations" group).
- In `src/AlbocariusCompanions.Core/Validation/ConversationChecks.cs`, per conversation (next to `ReportUnreachable`):
  compute, ignoring conditions, every node that can reach an exit (a node whose next is hub/end is an exit; a node can
  reach an exit if any of its next targets can). A `next` id that is not a node of the conversation counts as an exit for
  this rule (it is already reported as `UnknownNode`; do not report it twice).
- All nodes (reachable from start or not) that cannot reach an exit → **one** error per conversation, folder = the
  companion folder, message: `Conversation '<id>': nodes 'a', 'b' can never reach 'hub' or 'end'; add a way out.` with the
  node ids in conversation file order, each in single quotes.
- Existing warnings (unreachable nodes, may-stall) stay unchanged.
- Update `docs/SCHEMA.md` (Conversations section, the "Warnings:" paragraph area) with one sentence about this error.

## Files
- Read: `src/AlbocariusCompanions.Core/Validation/ConversationChecks.cs`, the acceptance test, `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
- Edit: `src/AlbocariusCompanions.Core/Validation/ConversationChecks.cs`, `src/AlbocariusCompanions.Core/Validation/IssueCodes.cs`, `docs/SCHEMA.md`.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(ignore only failures of `GameMapTests` q=21..31, which other tasks are still researching.)
