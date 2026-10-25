# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 71-transfer-core: transfer effects and hasGold/hasItem conditions (Core only)

## Goal
Add item/gold **transfers** to conversation node effects and **hasGold / hasItem** conditions, in Core only
(`src/AlbocariusCompanions.Core`). Acceptance tests: `tests/AlbocariusCompanions.Acceptance/Core/TransferTests.cs`
(read it first; it is the precise spec). Background: `orchestra/SPEC_BEHAVIOR_FOUNDATION.md` §2 I3; game facts in
`docs/GAME_MAP.md` Q29 (you need not read it: the rules below already apply it). Do not touch `src/AlbocariusCompanions.Game`.

## JSON shape (dialogs.json, inside a node's "effects" / any "when" object)
```json
"effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } }
"effects": { "transfer": { "item": "grain", "count": 3, "from": "world", "to": "player" } }
"when":    { "hasGold": { "who": "self", "min": 100 }, "hasItem": { "who": "player", "id": "grain", "min": 2 } }
```
Actors: `player`, `self` (the companion being talked to), `world` (source/sink: gold or items appear/disappear),
or another companion's id (`[a-z0-9_]+`, same rule as companion ids; the companion's own id means the same actor as `self`).

## Models (src/AlbocariusCompanions.Core/Models/ConversationDefinition.cs)
- `EffectSet.Transfer` (`[JsonProperty("transfer")] TransferEffect?`); new class `TransferEffect` with
  `Gold` (int?), `Item` (string?), `Count` (int?), `From` (string?), `To` (string?) — JSON names gold/item/count/from/to.
- `ConditionSet.HasGold` (`[JsonProperty("hasGold")] GoldCheck?`: `Who` string?, `Min` int?) and
  `ConditionSet.HasItem` (`[JsonProperty("hasItem")] ItemCheck?`: `Who` string?, `Id` string?, `Min` int?).
- The loader is strict (unknown JSON fields are load errors) — nothing to do, just use JsonProperty like the other classes.

## New: src/AlbocariusCompanions.Core/Dialogs/Transfers.cs (public static class Transfers)
- constants `Player = "player"`, `Self = "self"`, `World = "world"`.
- `public static TransferEffect? Of(EffectSet? effects)`: returns `effects.Transfer` if set (same instance); else the old
  `gold` key as sugar: gold < 0 → `{ Gold = -gold, From = "player", To = "world" }`, gold > 0 → `{ Gold = gold, From = "world", To = "player" }`; else null.
- `public static int CountOf(TransferEffect t)` → `t.Count ?? 1`.

## Validation (ConversationChecks.ValidateEffects / ValidateConditions; errors use IssueCodes.InvalidEffect / InvalidCondition)
Transfer (InvalidEffect, one error per broken transfer is enough; tests expect exactly ONE error for each invalid case):
exactly one of gold/item; gold > 0; item non-empty; count ≥ 1 and only with item; from and to required and each a valid actor
(case-sensitive); from ≠ to (own companion id counts as self); items only between `player` and `world` (companions in the
player's party share the party inventory, others have none — Q29); `transfer` and the old `gold` key on one node → error.
hasGold (InvalidCondition): who required, a valid actor but not world; min required and ≥ 1.
hasItem (InvalidCondition): who must be `player`; id non-empty; min optional (default 1), if given ≥ 1.
Nested conditions (all/any/not) are validated recursively already — make sure hasGold/hasItem inside them are checked too.
Also update the stall heuristic `HasCondition` in ConversationChecks: today it counts `node.Effects?.Gold < 0`; make it
"the node's `Transfers.Of(effects)` has a From other than world" (an implied condition), and count hasGold/hasItem in `IsSet`.

## Other companions as actors (catalog level)
A companion-id actor (transfer from/to, hasGold who) that is not a loaded companion → **warning**
`IssueCodes.MissingCompanionReference` on the referencing folder (never an error). Add this to the cross-companion pass in
`src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs` (see how `VariableChecks.CrossCompanion(kept)` is used there;
add e.g. `TransferChecks.CrossCompanion(kept)` in a new file `Validation/TransferChecks.cs`, warnings only, concatenated
to the issues; keep the existing loop logic intact).

## Runtime (src/AlbocariusCompanions.Core/Dialogs/ConversationConditions.cs)
`IConversationContext` already has `int? GoldOf(string actor)` (null = unknown companion) and `int PlayerItemCount(string itemId)`.
- In `Evaluate`: hasGold → `GoldOf(who)` null or < min → false; hasItem → `PlayerItemCount(id) < (min ?? 1)` → false.
- In `EffectsCanApply`: replace the `effects.Gold < 0` check with: `var t = Transfers.Of(effects)`; if t.From != world:
  gold → `(GoldOf(t.From) ?? -1) < t.Gold` → false; item → `PlayerItemCount(t.Item) < CountOf(t)` → false. Keep the join check.

## Docs
`docs/SCHEMA.md`, section "Conversations": document transfer, actors, hasGold/hasItem (and that `gold` is shorthand for a
player↔world transfer), in the style of the surrounding text.

## Files
- Read: the acceptance test above, `src/AlbocariusCompanions.Core/Dialogs/*.cs`, `src/AlbocariusCompanions.Core/Validation/{ConversationChecks,VariableChecks,IssueCodes}.cs`, `src/AlbocariusCompanions.Core/Registry/CompanionCatalog.cs`, `src/AlbocariusCompanions.Core/Models/ConversationDefinition.cs`.
- Edit: those Core files, new `Dialogs/Transfers.cs`, new `Validation/TransferChecks.cs`, `docs/SCHEMA.md`. You may add unit tests in `tests/AlbocariusCompanions.Core.Tests`.
- Do not edit: `src/AlbocariusCompanions.Game`, `tests/AlbocariusCompanions.Acceptance`, `orchestra/`.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(all tests, including TransferTests, must pass; GameMapTests for questions still being researched by others may fail — ignore only failures of `GameMapTests` q=21..31.)
