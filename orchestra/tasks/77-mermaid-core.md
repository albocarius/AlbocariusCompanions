# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 77-mermaid-core: export conversations as Mermaid flowcharts (Core)

## Goal
New `public static class MermaidExport` in `src/AlbocariusCompanions.Core/Dialogs/MermaidExport.cs` that renders a
companion's conversations (dialogs.json "conversations", see `docs/SCHEMA.md`) as a Mermaid flowchart. Acceptance tests:
`tests/AlbocariusCompanions.Acceptance/Core/MermaidExportTests.cs` — read it first; `FixtureChart` there is the exact
expected output for the fixture conversation and is the format reference.

## API
- `public static string Render(CompanionPackage package, string? conversationId = null)` — all conversations in file order,
  or only the one with that id; unknown id → `ArgumentException` (message names the id).
- `public static string Describe(ConditionSet when)` and `public static string Describe(EffectSet effects)` — the compact
  texts used in labels (public for tests).

## Format (lines joined with "\n", output ends with "\n")
1. `flowchart TD`
2. Per conversation `<c>` (its id), all lines indented two spaces, Mermaid ids `<c>__<node id>`:
   - entry: `<c>__entry(["<c> (<entry>[, once])"])`, plus `<br/>when: <Describe(conversation.when)>` inside the quotes when the conversation has a non-empty when.
   - each node in file order: npc `<c>__<n>["<n>: <text>"]`, player `<c>__<n>[/"<n>: <text>"/]`; when the node has effects
     with a non-empty description, append `<br/>effects: <Describe(effects)>` inside the quotes.
     `<text>` = the node's English text (`package.Text(nodeId, "en")`), `(no text)` when missing.
   - edges: entry → each start node, then for each node in file order each `next` target in order. Target `hub`/`end` →
     `<c>__hub` / `<c>__end`. Plain edge `A --> B`; when the target node has a non-empty `when`: `A -->|"<Describe(when)>"| B`.
   - terminals, only if used, hub first: `<c>__hub(("hub"))`, `<c>__end(("end"))`.
   - `class <comma-separated player node ids> player`, then `class <npc node ids> npc` (file order; omit a line with no nodes).
3. Once at the end: `classDef player fill:#e8f0fe,stroke:#4a6fa5` and `classDef npc fill:#fdf2e3,stroke:#b07a2a` (indented).
Every `"` inside a quoted label is written as `#quot;`.

## Describe(ConditionSet) — parts joined with " and ", in this order
met/hired/inParty: `met` / `!met` (same for hired, inParty); `relation >= N` (minRelation), `relation <= N` (maxRelation),
`gold >= N` (minGold); each flags entry `x`, each notFlags entry `!x`; var: `v >= n` (min), `v <= n` (max), `v = true|false|n` (eq);
hasGold `<who> gold >= <min>`; hasItem `<who> has <min ?? 1> <id>`; all `(<a> and <b>)`; any `(<a> or <b>)` (each element
described recursively); not `not (<a>)`. Empty object → empty string.

## Describe(EffectSet) — parts joined with ", ", in this order
relation `relation +N` / `relation -N`; gold `gold +N` / `gold -N`; transfer (`EffectSet.Transfer`, not the gold sugar)
`<gold> gold <from> -> <to>` or `<count ?? 1> <item> <from> -> <to>`; each setFlags `x = true`; each clearFlags `x = false`;
each set entry `x = <value>` (true/false/number); each add entry `x += <n>` (n as is, e.g. `mood += -1`); join → `join`.

## Files
- Read: `src/AlbocariusCompanions.Core/Models/{ConversationDefinition,CompanionPackage,DialogDefinition}.cs`, the acceptance test, `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
- Edit: new `src/AlbocariusCompanions.Core/Dialogs/MermaidExport.cs` only (you may add unit tests in `tests/AlbocariusCompanions.Core.Tests`).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
