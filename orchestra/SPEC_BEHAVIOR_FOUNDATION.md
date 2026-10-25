# SPEC — AlbocariusCompanions — Milestone: Behavior foundation (overnight)

> Follows `SPEC.md` and `SPEC_CORE_GAPS.md`. Their hard rules and `orchestra/HARD_RULES.md` still apply.
> This run has two independent tracks that can be interleaved:
> - **Track R — Research:** GAME_MAP questions for the behavior system. Docs only.
> - **Track I — Implementation:** only the parts whose design is already decided (§3). Nothing that depends on Track R answers or on the selection-rules decision.

## 0. Out of scope (do not implement, do not design)

- Event triggers (battle, raid, settlement entered, trait change, time ticks…)
- Companion-initiated conversations, notifications as dialog delivery, inquiry pop-ups
- Cooldowns, interruption limits, candidate queue
- **Selection rules** (priority / specificity / order when several candidates match) — a separate design session will decide. Keep the current behavior unchanged.
- Companion ↔ companion relation, morale mechanics, leave threats, leaving
- Python/Qt editor

Ideas that come up → `docs/IDEAS.md`.

## 1. Track R — Research (GAME_MAP, new section "Behavior system")

One task per question, worker drafts with decomp file path + class/method for every claim, orchestrator verifies. Mark anything unresolved as UNCERTAIN with what would resolve it.

- **R1 Hero ↔ hero relations:** does vanilla track relations between two arbitrary heroes (not only player ↔ hero)? Does it work for wanderer/companion heroes? Read/write API, persistence, value range, side effects of changing it (notifications, AI behavior).
- **R2 Morale:** is morale party-level only, or is there any per-hero satisfaction/mood in vanilla? How party morale is computed; how a mod can apply a change (permanent vs temporary bonus, duration, tooltip/explanation text); what happens at low morale (desertion etc.).
- **R3 Game events:** for each event below: the `CampaignEvents` member (or other hook), its arguments, how to tell whether the player's party is the subject, and when it fires relative to the outcome:
  battle won / lost / player fled or retreated; companion wounded in battle; village raided (looted, burned); caravan or villager party attacked; town/castle sacked; prisoner lord executed / released; wages unpaid; food ran out / starvation; party morale changed; settlement entered / left; siege started / ended; player joined / left a kingdom; hero joined / left the player's clan; hero died.
- **R4 Player traits:** where trait XP/level changes happen (single hook point?), how to read current trait levels, which actions change which trait.
- **R5 Companion-initiated conversation:** can a conversation with a party member be opened programmatically while on the campaign map? Which API, in which game states it is allowed (not in battle, not in menus, not during sieges…), and whether it pauses time.
- **R6 Inquiry pop-ups:** API for a pop-up with text and 2+ options and callbacks; whether it pauses the game; constraints on when it can be shown; multi-option variant.
- **R7 Time:** campaign time APIs for "now", day/hour ticks, and computing "N days since X"; how to store a timestamp in a save.
- **R8 Location & surroundings:** current settlement of the player party, its type (town/castle/village) and culture; distance from the party to a settlement; nearest settlements; a village's produced goods (e.g. grapes) by item id.
- **R9 Items:** look up an item by string id; add/remove items to/from the player party inventory; count an item; behavior when the id does not exist. Also need to check gold transfer api between heroes and if companions in the party has seperate inventory or not
- **R10 Debug console commands:** how a mod registers in-game console commands, argument parsing, how to print output, whether the console is available in a normal (non-dev) game.
- **R11 Misc conditions:** clan tier; whether the player is at war (with anyone / a given faction); whether a specific hero is in the player's party.

## 2. Track I — Implementation (decided parts only)

### I1 — Variables (replace ad-hoc flags)
Add a `variables` block to `dialogs.json`:
```json
"variables": {
  "told_scar":  { "type": "bool", "scope": "shared" },
  "raids_seen": { "type": "int",  "scope": "local", "default": 0 }
}
```
- Types: `bool` (default `false`), `int` (default `0` unless given).
- Scopes: `local` (only this companion reads/writes) and `shared` (other companions may read/write it as `<companionId>.<name>`; the owner is still the declaring companion). No unowned global scope.
- Every companion has a built-in `int` variable `mood` (local, default 0). No mechanics use it yet; it is readable/writable like any other variable.
- Lifetime: permanent until changed.
- Any reference to an undeclared variable (in conditions or effects), a `local` variable of another companion, or a type mismatch (int op on bool, etc.) → **validation error**.
- Migrate existing `flags` / `notFlags` / `setFlags` / `clearFlags` usage: keep these keys working as sugar for bool variables, but the variables must be declared.
- Persistence: values saved per companion by name. On load, values whose variable no longer exists are ignored; newly declared variables get their default. A renamed variable loses its value (document this).

### I2 — Conditions
- Existing flat `when` object = implicit **all**.
- Add combinators, nestable: `{ "all": [ … ] }`, `{ "any": [ … ] }`, `{ "not": { … } }`.
- Add int comparisons: `{ "var": "raids_seen", "min": 3 }`, `max`, `eq`. Works for `shared` vars of other companions (`"var": "jeremus.told_scar"` for bool via `eq`/flags sugar).
- Keep all existing conditions (`hired`, `inParty`, `met`, `minGold`, `minRelation`, `maxRelation`, …) unchanged.

### I3 — Effects
- Transfers (replaces gold, giveItem, takeItem): { "transfer": { "gold": <n> | "item": "<id>", "count": <n>, "from": <actor>, "to": <actor> } }. Actors: player, self, <companionId>, world (source/sink). Gold uses each hero’s own gold (vanilla gold transfer API, map it in R9). Items use the party inventory; R9 must state which actors are meaningful for items (companions in the player’s party likely share it) — invalid actor/kind combinations → validation error.
- Add conditions hasGold: { who, min } and hasItem: { who, id, min }. A transfer whose from is not world without a matching hasGold/hasItem on the same path → warning.
- Keep the old gold key as sugar for { transfer: { gold: |n|, from/to: player/world } } until the sample content is migrated, then remove it.
- Item transfers after R9 is verified. Missing item id at runtime → skipped + warning.

### I4 — Voice on conversation nodes
- `voiced` (bool, default false) on `npc` nodes in `conversations`, same audio rule as `lines` (`audio/<full id>.ogg`).

### I5 — Validation (add)
- Dangling `next` (unknown node id, other than reserved `hub`/`end`), unreachable node, cycle with no path to `hub`/`end`.
- Declared variable never written by anything and never has a non-default default → warning ("never changes").
- Negative `gold` effect without a `minGold` ≥ amount condition on the same node or its path entry → warning. Same for `takeItem` without `hasItem`.
- **Repeatable reward:** a conversation that is not `once`, whose path can give positive `gold`/`giveItem`/relation, and has no condition that the same path makes false (e.g. sets a bool it checks with `notFlags`) → warning.
- Do **not** add the "unconditional option shadows later options" rule — it depends on the selection-rules decision.

### I6 — CLI (`tools/AlbocariusCompanions.Cli`, net8.0, references Core only)
- `validate <path>`: validate one companion folder or a whole `Companions` directory; prints errors/warnings; exit code non-zero on errors.
- `mermaid <companion> [conversation]`: export conversations as Mermaid flowcharts (player/npc nodes visually distinct, conditions and effects as edge/node labels).
- No `simulate` yet (depends on selection rules).

### I7 — In-game debug commands (needs R10 first)
Console commands, all prefixed `albocarius.`: list companions and their state; get/set a variable (`albocarius.set jeremus.told_scar true`); reset a companion's variables to defaults; reload JSON (if safely possible, otherwise skip and note in report). Every command wrapped in `Safe.Run`.

### I8 — Sample content
- Fix the `spare_coin` exploit in the sample companion (both paths must end the offer, e.g. set `got_coin`) and add the missing `minGold`.
- Migrate the sample to declared variables. All sample content must pass validation with zero warnings.

### Ordering
Track R and Track I interleave freely, except: I3 items after R9; I7 after R10. Commit per task.

## 3. Decided design (reference for this run)

- Player ↔ companion relation: vanilla relation.
- Variables: bool/int, local/shared, declared, permanent; `mood` built-in.
- Conditions: implicit all + any/all/not + int comparisons.
- Gold effect = player's gold change.
- Item dependencies come from `requiredModules`.
- When a companion leaves the party it keeps roaming taverns as today (no change).
- Save: state by name, unknown ignored, defaults for new.

## 4. Report

`docs/RUN_REPORT_BEHAVIOR_FOUNDATION.md`: Track R answers summary with UNCERTAINs, Track I per task, open BLOCKERS, and test checklist:
1. Existing companions still load, talk, hire, voice + lipsync (no regression).
2. Sample `the_scar` → `spare_coin` flow works; coin can be obtained only once.
3. `albocarius.set` / list commands work in the console (if R10 allows).
4. CLI: `validate` on the companions folder → zero errors/warnings; `mermaid` output renders.
5. Save → load keeps variable values.
