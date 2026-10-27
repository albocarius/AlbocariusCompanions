# Simulator protocol (CLI `serve`)

The simulator plays a conversation with one companion step by step, with the same Core code the game uses for
conditions (`ConversationConditions`), the selection rule (`ConversationSelection`, SCHEMA "Selection rule") and variable
effects (`ConversationEffects`). It is meant for an editor (the planned Qt editor): no human-readable output.

```
dotnet AlbocariusCompanions.Cli.dll serve [companionsRoot]
```

One JSON object per line on stdin (a request), one JSON object per line on stdout (its response), in order. Blank lines
are ignored. The process ends at end of input or after a `quit` request (exit code 0). With `companionsRoot` the server
first loads it as a `load` request would and writes that response to **stderr** (stdout carries only answers to
requests). Engine: `src/AlbocariusCompanions.Core/Simulation` (`SimulatorProtocol`, `SimSession`, `SimState`).

## Requests and responses

Every request has `op` and may have `id` (any JSON value, echoed back). Every response has `id` (null when the
request had none or was not JSON) and `ok`. On failure: `{ "id": …, "ok": false, "error": "<message>" }` — a bad
line never stops the server.

| op | request fields | response fields (besides `id`, `ok`) |
|---|---|---|
| `hello` | | `version` (1) |
| `load` | `root`: a `Companions` directory | `companions` (ids that loaded), `errors`, `warnings`, `issues` (`severity`, `code`, `folder`, `message`). Replaces anything loaded before and closes every session |
| `start` | `companion`, `entry` (`hub` \| `greeting`), `state`? (see State), `seed`? (int; random ties are repeatable with it) | `session` (a new id such as `"s1"`), `step` (see Step) |
| `choose` | `session`, `conversation`, `node`: one of the shown options of the session's last step | `step` |
| `hub` | `session` | `step` with the hub options (back to the menu in the same talk) |
| `hire` | `session` | `hired` (bool), `reason` when refused, `state`. The mod's fixed hire dialog: needs the companion not hired, the clan not full and the player's gold ≥ `hireCost`; pays `hireCost` to the companion, sets met, hired and inParty |
| `endTalk` | `session` | — A new talk begins: kept selection picks are dropped (the game does this when a conversation ends) |
| `getState` | `session` | `state` |
| `setState` | `session`, `state` | `state` (the full state after the change) |
| `close` | `session` | — |
| `quit` | | — then the process exits |

`start` also begins a new talk. `entry: "greeting"` is the player talking to the companion: a greeting conversation may
open the talk (selection rule); without one the talk opens at the hub, as in the game. `entry: "hub"` opens at the menu.

## State

Everything conditions and effects read or write. Every field is optional in a request; missing ones take the defaults.

```json
{
  "companions": { "test_companion": { "met": true, "hired": true, "inParty": true, "relation": 0, "gold": 0 } },
  "player": { "gold": 1000, "items": { "grain": 3 } },
  "clanIsFull": false,
  "variables": { "test_companion.told_scar": true, "test_companion.mood": 0 },
  "started": [ "test_companion:the_scar" ]
}
```

| Field | Meaning | Default |
|---|---|---|
| `companions.<id>.met` / `hired` / `inParty` | met the player / in the player's clan / in the player's party | `false` |
| `companions.<id>.relation` | relation with the player, −100..100 | `0` |
| `companions.<id>.gold` | the hero's own gold | `0` |
| `player.gold` | the player's gold | `0` |
| `player.items` | item string id → count in the party inventory | empty |
| `clanIsFull` | the clan is at its companion limit (hire and `join` refused) | `false` |
| `variables` | `"<companionId>.<name>"` → `true`/`false` or an integer, for declared variables (and the built-in `mood`) of loaded companions; an unknown name or a wrong type is an error | the declared default |
| `started` | `"<companionId>:<conversationId>"` of conversations that have started (for `once`) | empty |

Responses always return the full state: every loaded companion's declared variables with their current values, and a
`companions` entry for every companion that was read or written. Unknown fields in a request state are an error.

Not in the state yet: clan tier, war state, location, time, traits — no condition reads them. A new condition that reads
something adds a field here with a documented default (old requests keep working).

**Known differences from the game:** relation effects are applied raw (the game scales positive changes by its own
model and rounds randomly, GAME_MAP Q21); gold transfers are capped at the giver's gold as in the game; after a player
line whose `next` is `hub` the game shows its "anything else?" line before the menu, the simulator goes straight to
the hub options; the companion's fixed lines (intro, backstory, hire, vanilla dialog points) are not simulated except
`hire`.

## Step

```json
{
  "kind": "options",
  "at": "hub",
  "selections": [],
  "said": [
    { "conversation": "the_scar", "node": "p_scar_ask", "speaker": "player", "text": "…", "selection": null, "effects": [] },
    { "conversation": "the_scar", "node": "scar_story", "speaker": "npc", "text": "…",
      "selection": { "set": "npc", "candidates": [ { "id": "scar_story", "priority": 0, "passes": true, "conditions": [] } ],
                     "topPriority": 0, "group": [ "scar_story" ], "winner": "scar_story" },
      "effects": [] }
  ],
  "options": [ { "conversation": "the_scar", "node": "p_scar_sorry", "priority": 0, "shown": true, "conditions": [] } ],
  "state": { }
}
```

- `kind`: `options` (the player chooses next), `end` (the talk closed: the kept picks are dropped), `stuck` (no
  companion reply holds; the game would hang there — the failed selection is in `selections`).
- `at` (for `options`): `hub`, or `<conversation>/<node>` — the node the options follow.
- `selections`: a choice made without a line being said: the `greeting` set when the talk opened with `entry:
  "greeting"` (its candidates are greeting conversations), or the failed `npc` set of a `stuck` step.
- `said`: every line said by this call, in order: the chosen player line, then each companion line that followed, until
  the player must choose again, the hub, or the end (at most 50 companion lines in a row). A companion line carries its
  `selection`; `effects` lists what was applied (`variable <ref>`, `relation +2 (raw) -> 2`, `gold 100 player -> self`,
  `item 1 x grain player -> world`, `join`, or `skipped …` when a referenced companion is not loaded).
- `options`: every option of that point, shown or not, in menu order (hub: conversations by priority, then file order;
  inside a conversation: the `next` order). `shown` is what the game would show; `priority` is the conversation's on the
  hub, the node's elsewhere (player options are never selected — all shown ones are listed).
- `selection` (a set): `set` (`greeting` | `npc`), `candidates` (each with `id`, `priority` (absent = 0), `passes`,
  `conditions`), `topPriority` (highest priority among the passing candidates, null when none passes), `group` (the
  passing candidates with that priority), `winner` (one of `group`, at random on a tie; kept for the rest of the talk).
- `conditions`: one entry per checked field: `field`, `holds`, `expected`, `actual`, and `nested` for `all`, `any`,
  `not`. Field names follow `dialogs.json` (`met`, `hired`, `inParty`, `minRelation`, `maxRelation`, `minGold`,
  `hasGold.<who>`, `hasItem.<id>`, `flags.<name>`, `notFlags.<name>`, `var.<name>.min|max|eq`, `all`, `any`, `not`),
  plus `companionLoaded` (a referenced companion is not loaded: the whole condition fails), `once`, `greeting.met`,
  `startNodeHolds` (a greeting needs one start node that holds), `transfer.from.<actor>` and `join.notHired` /
  `join.clanHasRoom` (conditions implied by effects). `passes`/`shown` come from the game code; `conditions` explain them.

## Triggers

A trigger session runs the trigger engine over every loaded companion (docs/TRIGGERS.md, docs/SCHEMA.md "Triggers"). It
has its own state (same shape as above; `companions.<id>.role` sets a companion's role, `player.partyMorale` sums
`partyMorale` effects) and a clock in campaign hours that only moves forward.

| Op | Request fields | Response |
|---|---|---|
| `triggers` | `state`?, `seed`? | `session` (`t<n>`), `usedTriggers` (the triggers loaded content uses: the game listens only to these) |
| `fire` | `session`, `trigger`, `payload`?, `hours`? | `hours`, `candidates`, `queue` |
| `deliver` | `session`, `hours`?, `onMap`? (default true), `canInterrupt`? (default true) | `hours`, `delivered` (or null), `dropped`, `queue` |
| `queue` | `session` | the queue object |
| `clearCooldowns` | `session` | the queue object after clearing item/companion cooldowns and today's counters |
| `close` | `session` | — (also closes trigger sessions) |

`payload`: field → value, as the trigger lists its fields. An entity field takes an object of its properties, e.g.
`{"victim": {"id": "lord_1", "culture": "vlandia", "inPlayerClan": true}, "detail": "Executed"}`; properties left out read
as `""`/`false`. Unknown fields, properties or wrong kinds are an error.

Each candidate: `companion`, `content`, `kind` (`line`|`conversation`), `delivery`, `subject` (the one used: its own or
the trigger's default), `outcome` (`queued` or the first failed check: `subject`, `where`, `no hero`, `not in party`,
`cooldown`, `already queued`, `chance`, `when`), the check results `subjectMatches`, `whereMatches`, `present`,
`offCooldown`, `alreadyQueued`, `chanceHit`, `whenHolds` (null = not reached), and `conditions` (as in "Step", with
`event.<path>` and `role` entries) when `when` was evaluated. A companion not listed in the state is not in the party.

`queue`: `items` (key, companion, content, trigger, delivery, priority, firedAt, expiresAt, inMission; delivery order:
priority, then fired time), `itemCooldowns` (content key → hour last delivered), `companionInterruptions` (companion →
hour of its last conversation/inquiry), `interruptionsToday`, `notificationsToday`.

`delivered`: `key`, `companion`, `content`, `trigger`, `delivery`, and `text` (a line) or `node` + `text` (the start node
the selection rule picked); an inquiry adds `options` (`node`, `text`, `shown`) and `popup` (`standard` for 1–2
options, `multiSelection` for more). Effects are not applied by the simulator's delivery. `dropped`: items removed on the
way (`expired`, `conditions no longer hold`, …) with their `reason`. The limits are the game's (docs/TRIGGERS.md).

## Example session

```
→ {"id":1,"op":"load","root":"module/ModuleData/Companions"}
← {"id":1,"companions":["test_companion"],"errors":0,"warnings":0,"issues":[],"ok":true}
→ {"id":2,"op":"start","companion":"test_companion","entry":"hub","state":{"player":{"gold":1000}}}
← {"id":2,"step":{"kind":"options","at":"hub",…},"session":"s1","ok":true}
→ {"id":3,"op":"hire","session":"s1"}
← {"id":3,"hired":true,"state":{…"player":{"gold":500,…}…},"ok":true}
→ {"id":4,"op":"hub","session":"s1"}
→ {"id":5,"op":"choose","session":"s1","conversation":"the_scar","node":"p_scar_ask"}
→ {"id":6,"op":"choose","session":"s1","conversation":"the_scar","node":"p_scar_sorry"}
→ {"id":7,"op":"choose","session":"s1","conversation":"spare_coin","node":"p_coin_offer"}
← {"id":7,"step":{"kind":"end","said":[…"p_coin_offer"…"coin_thanks"…],"state":{…"gold":400…}},"ok":true}
```
(`tests/AlbocariusCompanions.Acceptance/ServeTests.cs` runs exactly this sequence.)
