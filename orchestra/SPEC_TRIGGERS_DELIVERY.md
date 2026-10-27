# SPEC — AlbocariusCompanions — Milestone: Triggers & delivery

> Follows the previous specs; their hard rules and `orchestra/HARD_RULES.md` still apply.
> Goal: companions react to the world. Conversations and lines can be started by game events and polled conditions, and reach the player as a conversation, a notification or a pop-up. Built on the event catalogue (GAME_MAP Q35–Q47).

## 0. Decisions

- **All events exposed.** Every event classified *useful* or *niche* in Q35–Q42 becomes an author-facing trigger. *Not useful* events are not exposed.
- **Exception — unverified UI-raised events:** events that may fire only when the player opens a notification (ransom offered, peace offered, marriage offered, vassal/mercenary offer, and any other Q40 item marked this way) are **not exposed yet**. List them in the report with the in-game check that would verify each.
- **Ticks and lifecycle events are internal.** `DailyTickEvent`, `HourlyTickEvent`, `WeeklyTickEvent`, game load/new game etc. drive the engine (polling triggers, queue, cooldowns); they are not author triggers themselves.
- **Subject filtering:** every trigger fired for the whole world (hero killed, levelled up, war declared…) must be filterable by subject, so companions do not react to every lord on the map.
- **Docs come from code.** One trigger registry in code is the single source for validation, the simulator, the future editor and the documentation.
- A curated "common triggers" list is documentation only — every trigger is always available.

## 1. Tasks

### T1 — Trigger registry (Core)
One registry entry per trigger:
- `name` (author-facing), `category`, `common` (bool, for the docs list), one-line `description`
- source: the `CampaignEvents` member (or `poll:<kind>` for polling triggers)
- payload fields available to filters and conditions (name, type, meaning — e.g. `settlement`, `hero`, `winnerSide`)
- valid subject values for this trigger
- `inMission` flag: fires during a battle/scene (delivery must wait, see T5)

**Naming pass** (before anything is written into content): every name unique, camelCase, past tense for things that happened (`villageLooted`), present for states starting/ending only when needed. Fix at least: `hideoutCleared` collision (hideout vs alley), alley events named `hideout…`, `tournamentWon` for `TournamentFinished`, the three village raid names (`villageRaidStarted` / `villageLooted` / `villageRaided`) — names must say what differs. Put the full old → new table in the report.

### T2 — Generated documentation
`docs/TRIGGERS.md` generated from the registry by a CLI command (`docs triggers`): grouped by category, each trigger with description, subjects, payload fields, an example; a "Common triggers" section at the top. A test fails if the committed file differs from the generated output.

### T3 — Schema
Conversations and slot lines may have a `trigger`:
```json
"trigger": {
  "on": "villageLooted",
  "subject": "player",
  "where": { "settlement.culture": "vlandia" },
  "chance": 0.5,
  "delivery": "conversation",
  "expiresAfterHours": 24,
  "cooldownDays": 7
}
```
- `subject`: one of the trigger's valid values — `self`, `player`, `playerParty` (anyone in the player's party), `playerClan`, `companion:<id>`, `hero:<stringId>`, `settlement:<stringId>`, `any`. Default per trigger in the registry (never `any` by default for world-wide events).
- `where`: equality filters on payload fields from the registry. Unknown field → validation error.
- Payload is readable in `when` conditions as `event.<field>` for the duration of the triggered content.
- `chance` 0..1, default 1.
- `delivery`: `conversation` | `notification` | `inquiry` (T5).
- `expiresAfterHours`: queued and not delivered by then → dropped. Default 24 in-game hours.
- `cooldownDays`: per content item, default 0.
- `when` still applies (evaluated at fire time and again at delivery time).
- Triggered content requires the companion to be in the player's party unless `"allowAbsent": true`.
- Validation: unknown trigger, invalid subject for that trigger, `delivery` incompatible with the content (e.g. `inquiry` needs exactly one npc text and 1..N player options), `where` on unknown fields.

### T4 — Polling triggers
Implemented on internal ticks, same schema:
- `nearSettlement` — params `settlement`, `radius`, `rearmRadius` (> radius). Fires once per approach (hysteresis from GAME_MAP Q43). Hourly check unless Q43 says otherwise.
- `daily` — once per in-game day; combine with `chance` for random comments.
- `varCrossed` — params `var`, `value`, `direction` (`up`/`down`); fires when the variable crosses the value.
- `roleStarted` / `roleEnded` — params `role` (`governor`, `partyLeader`, `caravanLeader`, `alleyLeader`, `questAlternative`); polled per Q45/Q47.
- Add the matching **conditions**: `role: "<role>"` (current role of the companion).

### T5 — Delivery
- **conversation:** companion-initiated map conversation (Q25 rules: only when the map is the active state, not at sea, not during a map event or encounter). Uses the conversation's `start` (npc-first) nodes.
- **notification:** one line shown bottom-left (in-game message), text only in this milestone.
- **inquiry:** pop-up (Q26). Npc text + player options as buttons (2 → standard inquiry, more → multi-selection). Choosing an option applies its effects like a conversation node.
- **Queue:** fired items wait until delivery is possible (e.g. after a battle for `inMission` triggers), are ordered by the selection rule (priority, ties random), and expire per `expiresAfterHours`.
- **Limits:** per-companion cooldown between interruptions (mod-wide constant, e.g. 1 in-game day), per-item `cooldownDays`, and a global cap on interruptions per in-game day (mod-wide constant). Notifications count toward the cap separately from conversations/inquiries (constants for both). Document the constants.
- **Performance:** attach listeners only for triggers used by loaded content.
- Queue, cooldowns and polling state are saved with the campaign (`CampaignTime`).

### T6 — Morale effect
Effect `{ "partyMorale": <int> }` using `RecentEventsMorale` (Q22; decays, shown as "Recent Events"). Positive and negative. Validation: reasonable range (document it).

### T7 — Simulator and debug
- `serve` mode: fire a trigger with a payload, see which content is a candidate, filters/conditions results, queue order and the delivered item.
- Console: `albocarius.fire <trigger> [payload k=v …]`, `albocarius.queue` (show queue and cooldowns), `albocarius.clearcooldowns`.

### T8 — Sample content
Add to `test_companion` at least: one `conversation`-delivered reaction (e.g. `playerBattleEnded`), one `notification` (e.g. `daily` with `chance`), one `inquiry` with two options, one `nearSettlement`, one `varCrossed` on `mood`, one using `partyMorale`. All pass validation with zero warnings.

## 2. Out of scope
Likes/dislikes and leaving as named systems (they will be content patterns on top of this), companion ↔ companion friction content, adding options to lord conversations, voiced notifications, the Qt editor.

## 3. Report
`docs/RUN_REPORT_TRIGGERS_DELIVERY.md`: per task, naming table (old → new), excluded UI-raised triggers with their verification steps, constants chosen, UNCERTAINs, BLOCKERS, and test checklist:
1. No regression (load, talk, hire, voice, conversations, transfers, noble, console).
2. Win a battle → the companion starts a conversation on the map afterwards (not mid-battle).
3. `daily` notification appears; never more than the cap per day.
4. Inquiry pop-up with two options; each applies its effects.
5. Approach the configured settlement → fires once; leave and come back → fires again.
6. Raise `mood` past the threshold with `albocarius.set` → `varCrossed` content fires.
7. `partyMorale` effect visible under "Recent Events".
8. `albocarius.fire` and `albocarius.queue` work; save → load keeps queue and cooldowns.
