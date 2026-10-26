# SPEC — AlbocariusCompanions — Milestone: Selection rules + event research

> Follows the previous specs; their hard rules and `orchestra/HARD_RULES.md` still apply.
> Two tracks, interleaved: **Track I** implements the decided selection rule and the tools that depend on it. **Track R** researches everything the trigger/delivery design will need. No trigger implementation in this milestone.

## 0. Decisions (reference)

- **Selection rule:** among candidates whose conditions pass, the highest `priority` wins; `priority` absent = `0`; ties → uniform random. Applies everywhere the system chooses between alternatives: conversations competing for the same entry point, lines in the same slot, npc replies in a `next` list.
  - Player options in a `next` list are not "selected" — every player option whose conditions pass is shown (unchanged).
- **Lords:** a mod companion that becomes a lord keeps vanilla lord dialogs. Our conversations may be **added** to the lord's conversation (extra options), never replace vanilla lord dialog. The mod's focus stays on companions in the player's party.
- **`noble`:** new optional `companion.json` field (bool, default `false`). Meaning: making a non-noble companion a lord costs relation with other lords; a noble one does not. Mechanics after R3.
- **Roles (governor, party leader, caravan leader, alley leader, quest alternative solution):** all allowed. No dedicated dialog slots. Our conversations can react through **conditions** (e.g. `role: "caravanLeader"`), designed after R4.
- Companion limit: mod companions count (vanilla). Tournaments: allowed.

## 1. Track I — Implementation

### I1 — Selection rule
- Implement §0 in Core, behind the existing injectable random source; unit-test priority wins, absent = 0, ties random (seeded), player options all shown.
- `priority` (int, default 0) allowed on: conversations, lines, npc nodes. Document in SCHEMA.md with an example.
- Replace any current "first match" / order-based behavior. Update sample content so its behavior is unchanged or intentionally documented.

### I2 — Shadowing validation
Warning when, inside one selection set (same entry point / same slot / same `next` npc replies), a candidate has **no conditions** and a priority **strictly higher** than other candidates — those lower candidates can never be chosen. Equal priority is fine (random).

### I3 — Simulation engine + `serve` mode
- Simulation engine in Core (same code the game uses), step by step: given a state and the current node, it returns the candidates, the result of each condition, the winning priority group, and the new state with effects applied.
- State covers everything conditions can read: variables (incl. other companions' shared ones), hired/inParty/met, gold, relation, items, clan tier, etc. Missing fields → documented defaults.
- No human-readable CLI output. Add a `serve` mode to the CLI: the process stays open and exchanges one JSON request/response per line over stdin/stdout. The future Qt editor will use this. Document the request/response format in `docs/SIMULATOR_PROTOCOL.md`.
- Tests run against the engine; one integration test drives `serve` with a short request sequence.

### I4 — `noble` field
Schema + model + validation only (bool, default false). No behavior yet.

### I5 — Vanilla fallback safety net must not be silent
The vanilla dialog fallback code that remains as a runtime safety net must log a warning and show an in-game warning (once per session per slot) whenever it is used. Normally it never triggers because all template slots are required.

### I6 — Remaining V3 "decide" items
Decided: lords (allowed, see §0), companion limit (counts), governor / party leader / alley leader / caravan / tournaments / quest alternative solution (allowed). List any remaining V3 *decide* items not covered here in the report for the user; change nothing for them.

## 2. Track R — Research (GAME_MAP, new section "Triggers & roles")

Worker drafts per task with decomp path + class/method per claim; orchestrator verifies. UNCERTAIN where unresolved. DLC sources stay excluded.

- **R1 Full CampaignEvents catalog:** every member of `CampaignEvents` (not only the ones in Q23). For each: arguments, when it fires (before/after the outcome), how to tell the player's party / clan is involved, and a one-line **usefulness note** for companion reactions with a suggested trigger name (or "not useful" + why). Group by theme (battle, crime, prisoners, party, settlement, kingdom/diplomacy, clan/heroes, economy, quests/issues, family, time). Also list polling-only signals (things with no event that need a daily/hourly tick).
  Split into several tasks by theme if needed.
- **R2 Proximity trigger:** how to detect "the player party is approaching settlement X" (no event expected): hourly vs more frequent tick, how to compute map distance (units, cost), a sensible radius scale, and hysteresis so it fires once per approach (enter radius → fire → re-arm only after leaving a larger radius). Example: a companion from Onira speaks when the party nears Onira.
- **R3 Lord flow:** how the player turns a companion into a lord/vassal (clan creation, fief grant — every path), events fired, whether vanilla already changes relations with other lords on this, and the exact point to apply a `noble`-based relation change. After the change: how to detect that a hero is a former mod companion now leading a clan, and how to **add** options to the vanilla lord conversation for such a hero (tokens/entry points) without replacing vanilla lines.
- **R4 Roles:** for governor, party leader, caravan leader, alley leader, and companion sent on a quest's alternative solution: how to check whether a hero currently has the role, and the events (or polling) for assignment / removal / role ending (e.g. caravan destroyed, alternative solution finished or failed).
- **R5 Marriage between heroes:** vanilla marriage action(s) and conditions (age, existing spouse, clans, kingdom), whether two companions in the player's clan can marry each other, consequences (clan membership, children/pregnancy, inheritance), the vanilla courtship flow vs a direct action, and any side effects on the player.

## 3. Report

`docs/RUN_REPORT_SELECTION_AND_EVENTS.md`: Track I per task, Track R summary (R1 as a compact table: event → trigger name → usefulness), UNCERTAINs, BLOCKERS, and test checklist:
1. No regression in game (load, talk, hire, voice, conversations, transfers, console).
2. Two conversations competing for `hub` with different priorities → higher one always offered; equal priority → both appear across several talks.
3. `serve` mode: a scripted request sequence on the sample (hire → `the_scar` → `spare_coin`) returns the expected candidates, winner and state changes (gold transfer, `got_coin` set).
4. CLI `validate` reports a shadowing warning on a deliberately broken test fixture.
