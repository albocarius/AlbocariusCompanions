# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 74-validation-authoring: authoring warnings over the loaded companions

## Goal
Two **warnings** (never errors) computed over all loaded companions in `CompanionCatalog.Load`. Acceptance tests:
`tests/AlbocariusCompanions.Acceptance/Core/AuthoringWarningsTests.cs` (read it first; it is the precise spec).
Background: `orchestra/SPEC_BEHAVIOR_FOUNDATION.md` §2 I5; schema in `docs/SCHEMA.md` (Conversations, Variables).

## Where
- New `src/AlbocariusCompanions.Core/Validation/AuthoringChecks.cs`: `public static class AuthoringChecks` with
  `public static List<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> loaded)` (warnings only).
- Call it in `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs` once, on the final `kept` list, after the
  existing cross-companion passes, and add its issues to the result (like `TransferChecks.CrossCompanion` is added).
  Do not change the skip loop. Skipped companions (errors, missing modules) are not in `kept` and are ignored entirely.
- New codes in `IssueCodes`: `VariableNeverChanges = "variable_never_changes"`, `TransferNotChecked = "transfer_not_checked"`.

## Warning 1 — variable never changes
For each declared variable of a loaded companion (dialogs.json `variables`; never the built-in `mood`): if no effect in
any loaded companion's conversations writes it AND its default is the type default (bool: absent or false; int: absent or 0)
→ warning on the owner's folder, message naming the variable as `'<name>'`, e.g.
`Variable 'asked' is never written and starts at its type default, so it never changes.`
Writes are `setFlags`, `clearFlags`, `set`, `add` (use `VariableUses.InEffects` in `Dialogs/VariableUses.cs`), with the
reference resolved via `VariableRules.TryParseReference(writerCompanionId, reference, out var r)` → owner `r.Owner`, name
`r.Name` (so "x", "<own id>.x" and another companion's "jeremus.x" are all resolved correctly).

## Warning 2 — transfer without a matching check
For each node whose transfer (`Transfers.Of(node.Effects)` in `Dialogs/Transfers.cs`, which also turns the old `gold`
key into a transfer) has a `From` other than `world`: warn unless a **matching check** is found in one of these
condition objects: the node's own `when`, the conversation's `when`, or the `when` of any node of the same conversation
from which this node can be reached along `next` (an ancestor; ignore conditions when walking). Inside a condition object
only the top level and nested `all` elements count (recursively) — never inside `any` or `not`.
- gold transfer of G from actor A matches `hasGold` with the same actor (compare with `Transfers.NormalizeActor(x, ownId)`
  on both sides, so the own id equals `self`) and `min >= G`; for A = `player`, `minGold >= G` also matches.
- item transfer of N × I (N = `Transfers.CountOf`) from `player` matches `hasItem` with `who` player, `id == I` and `(min ?? 1) >= N`.
Warning on the companion's folder; the message names the conversation and the node id (`'<node>'`) and the needed check,
e.g. `Conversation 'old_wound' node 'p_wound_skip' transfers 100 gold from 'player' without a matching hasGold (min 100) on its path.`

## Docs
`docs/SCHEMA.md`: one sentence for each warning in the Conversations/Variables sections.

## Files
- Read: the acceptance test, `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`, `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs`,
  `src/AlbocariusCompanions.Core/Dialogs/{VariableUses,Transfers}.cs`, `src/AlbocariusCompanions.Core/Rules/VariableRules.cs`,
  `src/AlbocariusCompanions.Core/Models/{ConversationDefinition,DialogDefinition,VariableDeclaration}.cs`, `src/AlbocariusCompanions.Core/Validation/TransferChecks.cs` (style reference).
- Edit: new `Validation/AuthoringChecks.cs`, `Validation/IssueCodes.cs`, `Registry/CompanionCatalog.cs`, `docs/SCHEMA.md`. You may add unit tests in `tests/AlbocariusCompanions.Core.Tests`.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` (all tests must pass, including
`PlaceholderCompanionTests`, which loads the real sample companion and allows no new warnings).
