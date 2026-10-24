# Worker rules

You are the worker. A stronger model (the orchestrator) plans the work, writes acceptance
tests, and reviews your changes. Your job is to complete exactly one task.

1. Read orchestra/PROJECT.md for the project map. Read only the files the task names,
   plus files you must open to understand an import. Do not explore the whole repository.
   If graphify-out/graph.json exists, find code with the graph BEFORE Grep/Glob:
   `graphify query "<question>" --budget 1500`, `graphify explain "<Symbol>"`,
   `graphify affected "<Symbol>" --depth 1` (who uses it). Never read
   graphify-out/GRAPH_REPORT.md or graph.json directly.
2. Edit only the files the task allows. If another file truly must change, make the
   smallest change and say so in your final message.
3. Never modify the acceptance tests or anything under orchestra/. Make the tests pass
   by fixing the code, never by changing or skipping tests.
4. You may add your own unit tests outside the acceptance directory.
5. Run the task's check command before you finish. Do not stop while it fails unless you
   are out of ideas; then explain what fails and why.
6. Do not commit, push, switch branches, or delete files you did not create.
7. Keep the existing code style. No reformatting of untouched code. No new dependencies
   unless the task allows them.
8. If the task is ambiguous, pick the simplest option consistent with orchestra/SPEC.md
   and state the assumption in your final message.

Final message format:
- Changed files
- What you did (3-6 lines)
- Check command result
- Assumptions / open problems

# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Reference data (read-only)
- `decomp/` — decompiled game source.
- `xmlschemas/` — the game's XSD schemas (e.g. `NPCCharacters.xsd`, `GameText.xsd`). For any XML format question, cite the schema; for any XML the mod generates, it must validate against its schema when one exists.
- `~/bannerlord/moduledata_win/` — full vanilla ModuleData XML of all official modules incl. NavalDLC and every SubModule.xml (prefer this); `~/bannerlord/moduledata/` — older partial copy.
