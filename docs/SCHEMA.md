# JSON schema reference

Companion data lives in `module/ModuleData/Companions/`. Every schema change updates this file
(SPEC_CORE_GAPS §1.1). The loader is strict: unknown fields are an error. Every JSON file starts with
`"schemaVersion": 1` (the only version this build of the mod supports; missing, not an integer or another value → error).

```
Companions/
  jeremus/                     folder name = companion id, [a-z0-9_]+ ("_shared" is reserved and skipped)
    companion.json
    dialogs.json               lines (companion + player), conversations — no text
    lang/en.json               required, a text for every line
    lang/tr.json, …            optional, ISO 639-1 file names
    audio/intro_01.ogg         default audio of line intro_01 (+ optional Rhubarb intro_01.xml)
```

## `companion.json`

The companion id is the folder name; there is no `id` field.

| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| `schemaVersion` | int | yes | `1` | `1` |
| `name` | string | yes | non-empty | `"Test Companion"` |
| `isFemale` | bool | yes | | `false` |
| `voice` | string | yes | `curt`, `earnest`, `ironic`, `softspoken` (vanilla voice types) | `"earnest"` |
| `age` | int | yes | 18..80 | `32` |
| `culture` | string | yes | game culture id | `"empire"` |
| `bodyProperties` | string \| null | no | BodyProperties key; null = template default | `null` |
| `attributes` | object | yes | all six of `vigor`, `control`, `endurance`, `cunning`, `social`, `intelligence`, each 1..10 | `{ "vigor": 4, … }` |
| `skills` | object | yes | game skill id → 0..330 | `{ "OneHanded": 80 }` |
| `traits` | object | no | game trait id → level | `{ "Valor": 1 }` |
| `equipment` | object | yes | see below | |
| `hireCost` | int | yes | ≥ 0; hireable with exactly this much gold | `500` |
| `noble` | bool | no | default `false`. Noble birth. When the player makes a non-noble companion a lord (grant-a-fief conversation), the player loses 4 relation with every clan leader of the player's kingdom (not the player's clan, the new clan or mercenaries) and sees "The lords of the realm frown upon raising a commoner to nobility." The new lord's relations are not changed. A noble companion costs nothing. The amount is fixed for the whole mod | `false` |
| `voiceProfile` | object | no | `{ "reference": string\|null, "style": string\|null }` (authoring notes, unused in game) | |
| `likes` | array | no | `{ "event", "reaction", "slot" }` — shape-validated only, no effect yet | `[]` |
| `dislikes` | array | no | same as `likes` | `[]` |
| `relations` | array | no | `{ "companion", "attitude": "friend"\|"rival" }`, not self — shape only | `[]` |
| `requiredModules` | string array | no | module ids, exactly as in that module's `SubModule.xml` `<Id>`; the companion is skipped when a listed module is not loaded; no blank or duplicate entry | `["NavalDLC"]` |

### `equipment`

| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| `templateId` | string | yes | game equipment roster id | `"npc_wanderer_equipment_template_empire"` |
| `battle` | object | no | slot → item id; slots `Item0`–`Item3`, `Head`, `Body`, `Leg`, `Gloves`, `Cape`, `Horse`, `HorseHarness` | `{ "Body": "monk_robe" }` |
| `civilian` | object | no | same as `battle` | `{ "Head": "pilgrim_hood" }` |

## `dialogs.json`

```json
{
  "schemaVersion": 1,
  "lines": [
    { "id": "intro_01", "slot": "intro", "voiced": true },
    { "id": "p_ask_about_01", "slot": "player_ask_about" }
  ],
  "conversations": [ … ]
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `lines` | array | yes | |
| `lines[].id` | string | yes | local id `[a-z0-9_]+`, unique within the companion (lines and conversation nodes share one namespace). The full id `<companionId>_<id>` (e.g. `jeremus_intro_01`) is the localization key, voice-over id and log name, and must be unique across all companions |
| `lines[].slot` | string | yes (no for a triggered line) | required companion slots: `intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`; required player slots (the player says these lines): `player_ask_about`, `player_ask_hire`, `player_hire_pay`, `player_hire_full`, `player_hire_later`; required vanilla dialog points (see below); reserved, no effect yet: `leave_threat`, `relation_complaint`, `like_reaction`, `dislike_reaction`, `idle`. Several lines in one slot → one is chosen by the selection rule (see "Selection rule") and kept for the rest of the talk |
| `lines[].trigger` | object | no | a triggered line (see "Triggers"): no `slot`, never used by a fixed dialog, delivered as a `notification` only |
| `lines[].when` | object | no | conditions as for conversations (see "Conditions"); the line is a candidate only while they hold. A slot whose every line has a `when` → warning (it can end up with no line) |
| `lines[].priority` | int | no | default 0; see "Selection rule" |
| `lines[].voiced` | bool | no | default `false`; a player slot line must not be voiced (error) |
| `lines[].audio` | string | no | relative to the companion folder, no `..`, must end in `.ogg` (any case) — anything else is an error; default `audio/<local id>.ogg`; not allowed on player slot lines |
| `conversations` | array | no | authored conversations, see below |
| `variables` | object | no | declared conversation variables, see "Variables" below |

Audio is bound to the dialog line, not to a language: the line's file in the companion's `audio/` folder (default
`audio/<local id>.ogg`) plays with that line in every game language.

### Player slots
The player's side of the mod's meeting and hire flow belongs to each companion, so every companion can be talked to in
its own way. Player lines are never voiced, so name tokens are allowed; `player_hire_pay` may use
`{ALBOCARIUS_HIRE_COST}` (the companion's hire cost). The flow: `player_ask_about` → `backstory`; `player_ask_hire` →
`hire_offer` → `player_hire_pay` (enough gold and room in the clan) → `hire_accept`, or `player_hire_full` (clan full) /
`player_hire_later` → `hire_decline`.

### Vanilla dialog point slots

Every vanilla point where a wanderer/companion speaks (GAME_MAP Q18) is a **required** slot (since 2026-10-26): a mod
companion overrides everything vanilla would make it say. It says one of its lines instead of the vanilla text; the vanilla
condition, consequence and tokens (`{SALUTATION}`, `{TITLE}`, `{SETTLEMENT_NAME}`, …) still apply. A companion missing any
slot fails validation: the log lists every missing slot in one line, the game shows "Companion '<id>' is not valid and was
not loaded; details in the log." The vanilla line remains only as a runtime safety net, and it is never silent: when a mod companion
shows vanilla text — a slot without usable text, or a vanilla/quest/DLC/other-mod line that no slot covers — a warning
goes to the log and the game (once per companion and line per session) naming the sentence id, so it can become a slot. The full
list with the vanilla English text is `src/AlbocariusCompanions.Core/Rules/VanillaDialogPoints.cs`; the companion template
(`tools/companion-template/`) has one line per slot, pre-filled with that text:
greetings and hub (`greet_recent`, `greet`, `main_hub`, `anything_else`, `farewell`, `job_status`), roles and missions
(`role_*`, `mission_offer`, `mission_ask`, `forage_ask`, `rejoin_ok`), dismissal (`fire_reaction`, `fire_farewell`,
`fire_cancel_reply`), becoming a lord (`lord_*`), grievances (`grievance_*`), rescue and prison break (`rescue_*`,
`prison_break_*`) and following in a settlement (`follow_ok`, `gather_ok`, `follow_stop_ok`).

New companion: `tools/new-companion.sh <id>` (Windows: `tools\new-companion.bat <id>`) copies the template; it refuses an
existing folder, an invalid id and `_shared`.

### CLI

Author-time checks and exports (`tools/AlbocariusCompanions.Cli`; all the logic is in `src/AlbocariusCompanions.Core`):
`dotnet run --project tools/AlbocariusCompanions.Cli -- validate module/ModuleData/Companions` prints one line per finding —
`ERROR <code> [<folder>] <message>` / `WARNING …` — followed by `<E> error(s), <W> warning(s).`. A path that holds a
`companion.json` is a single companion: only that folder's issues are printed (its parent is still loaded, so cross-companion
checks run). `dotnet run --project tools/AlbocariusCompanions.Cli -- mermaid module/ModuleData/Companions/<id> [conversation]`
writes that companion's `conversations` (SCHEMA "Conversations") as Mermaid flowcharts, or one of them by id. `serve [companionsRoot]` runs the conversation simulator for an editor: JSON lines on stdin/stdout,
see `docs/SIMULATOR_PROTOCOL.md`. Exit codes:
0 clean or warnings only; 1 validation errors (`mermaid` prints the broken companion's issues on stderr); 2 usage, a path
that does not exist, or an unknown conversation id. (As built, the standalone run crashes on a missing `Newtonsoft.Json`:
Core hides its package with `PrivateAssets=all` and the CLI csproj is orchestrator-owned — see `docs/BLOCKERS.md`.)

### Conversations
Extra companion ↔ player dialogs: a small graph of companion (`npc`) and `player` nodes.

```json
{
  "id": "the_scar", "entry": "hub", "once": true, "when": { "hired": true },
  "start": ["p_scar_ask"],
  "nodes": [
    { "id": "p_scar_ask", "speaker": "player", "next": ["scar_story"] },
    { "id": "scar_story", "speaker": "npc", "voiced": true, "next": ["p_scar_sorry", "p_scar_enough"] },
    { "id": "p_scar_sorry", "speaker": "player", "next": ["scar_thanks"],
      "effects": { "relation": 2, "setFlags": ["told_scar"] } },
    { "id": "p_scar_enough", "speaker": "player", "next": ["hub"] },
    { "id": "scar_thanks", "speaker": "npc", "next": ["end"] }
  ]
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `id` | string | yes | `[a-z0-9_]+`, unique among the companion's conversations |
| `entry` | string | yes (absent for a triggered conversation) | `hub`: the start player node(s) are options in the companion's conversation menu (listed above the vanilla options). `greeting`: when the player talks to the companion (already met), a start companion node opens the conversation instead of the usual greeting |
| `once` | bool | no | default `false`; `true`: offered until it has started once (per campaign, kept in the save) |
| `when` | object | no | conditions of the conversation, see below; for `greeting`, `"met": false` is an error (the first meeting is the intro) |
| `priority` | int | no | default 0. `hub`: menu order among the companion's hub conversations (higher first, ties in file order). `greeting`: among the greetings offered, the highest opens the talk (selection rule) |
| `start` | string array | yes | node ids: player nodes for `hub` (several = several menu options), companion nodes for `greeting` and triggered conversations (one is chosen by the selection rule) |
| `trigger` | object | no | started by a game event instead of the menu (see "Triggers"); then no `entry` |
| `nodes` | array | yes | at least one |
| `nodes[].id` | string | yes | local id like a line id (same namespace as the companion's lines; `hub` and `end` are reserved); key of the text in the lang files; full id `<companionId>_<id>` |
| `nodes[].speaker` | string | yes | `npc` (the companion) or `player` |
| `nodes[].voiced`, `nodes[].audio` | | no | as for lines; player nodes are never voiced |
| `nodes[].next` | string array | yes | what follows: node ids of the same conversation, or exactly one of `["hub"]` (back to the menu) / `["end"]` (the conversation closes). Player nodes → the companion's possible replies (one is chosen by the selection rule). Companion nodes → either the player's options (every option whose condition holds is shown) or the companion's next line. Never both kinds in one `next`; a player node is never followed by a player node. Loops back to earlier nodes are allowed |
| `nodes[].when` | object | no | conditions of this node, see below; a node whose condition fails is not offered / not said |
| `nodes[].priority` | int | no | companion nodes only, default 0; see "Selection rule". On a player node it has no effect (warning) |
| `nodes[].effects` | object | no | applied when the node is said, see below |

**Selection rule.** Where the game must choose one of several alternatives — the greeting that opens a talk, a
greeting's start node, the companion's reply among one `next` list, the line of a slot — the candidates whose conditions
hold are compared by `priority` (absent = 0): the highest wins; equal priorities are chosen at random. The choice is kept
for the rest of the talk (asked again later in the same talk, the companion says the same) and made again only when the
kept one no longer holds. Player options are never chosen between: every option whose condition holds is shown, and hub
conversations are all listed, ordered by `priority`. A candidate without conditions and with a strictly higher priority
than another candidate of the same set hides that one for ever (validation warning).

```json
"next": ["coin_refuse", "coin_fine"],
{ "id": "coin_refuse", "speaker": "npc", "when": { "maxRelation": 20 }, "priority": 1, "next": ["hub"] },
{ "id": "coin_fine", "speaker": "npc", "next": ["end"] }
```
Relation ≤ 20: both hold, `coin_refuse` wins by priority; above 20 only `coin_fine` holds.

**Conditions** (`when`; one object, every field that is set must hold — an implicit "all"):
- `met` (bool), `hired` (bool: in the player's clan), `inParty` (bool: in the player's party), `minRelation` / `maxRelation`
  (−100..100: the companion's relation with the player, min ≤ max), `minGold` (≥ 0: the player's gold);
- `hasGold: { "who": …, "min": … }` (the actor's own gold: `who` is `player`, `self` (the companion talked to) or another
  companion's id, never `world`; `min` is required, ≥ 1) and `hasItem: { "who": "player", "id": …, "min": … }` (the item
  count in the player's party inventory: `id` an item string id, `min` default 1; items live only in the party inventory,
  so `player` is the only `who`);
- `flags` (bool variables that must all be true), `notFlags` (bool variables that must all be false; a name may not be in both);
- `var` + `min` / `max` (int variable within the bounds, min ≤ max) or `var` + `eq` (equals: `true`/`false` for a bool, a
  whole number for an int); `min`/`max`/`eq` need a `var`, `eq` is not combined with `min`/`max`;
- `all: [ … ]` (every object holds), `any: [ … ]` (at least one holds), `not: { … }` (the object does not hold); each element
  is again a condition object, nestable; `all`/`any` may not be empty.

```json
"when": { "hired": true, "any": [ { "flags": ["told_scar"] }, { "var": "raids_seen", "min": 3 } ], "not": { "flags": ["got_coin"] } }
```

**Effects**: `relation` (non-zero, −100..100; the game scales positive changes by its own model and clamps the result),
`transfer` (gold or items moved between two actors, see below), `join` (`true`: the companion joins the player's clan and
party without payment — offered only while not hired and the clan has room), and variable writes, applied in this order:
`setFlags` (bools → true), `clearFlags` (bools → false), `set` (`{ "<var>": true | false | <int> }`, value of the
variable's type), `add` (`{ "<int var>": <non-zero int> }`). One node writes a variable at most once.

**Transfers** (`"effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } }`): exactly one of `gold`
(a positive amount) or `item` (an item string id, with optional `count` ≥ 1, default 1), plus `from` and `to` (both
required, and they must name two different actors). An actor is `player`, `self` (the companion talked to), `world`
(a source/sink: gold or items appear / disappear) or another companion's id (the companion's own id means the same as
`self`). Gold is each hero's own, so gold moves between any two different actors; items live only in the player's party
inventory (companions in the party share it, the others have none), so an item transfer is only between `player` and
`world`. A transfer happens when the node is said, and the node is offered only when its source (unless `world`) holds
the gold or items.

**Repeatable reward** (warning): a conversation that is not `once` and can reward the player — relation up, or gold/items
to the player from anyone else — along a path whose own writes never make one of its conditions false. Conditions that
count are the conversation's `when` and the `when` of the nodes from the start to the reward (top level and nested `all`);
e.g. `notFlags: ["got_coin"]` with `setFlags: ["got_coin"]` on that path. A write after the reward does not count.

**Unchecked transfer** (warning): a transfer whose `from` is not `world` warns unless a matching check — `hasGold` for the
same actor with a high enough `min` (or `minGold` for a transfer from `player`), `hasItem` for that item and count — sits
in the node's own `when`, the conversation's `when`, or the `when` of any node it can be reached from along `next` (top
level and nested `all` only; `any`/`not` do not count).

### Triggers
A conversation or a slot-free line with `trigger` is started by the game: an event (a battle ended, a village was
looted, …) or a polling check (`nearSettlement`, `daily`, `varCrossed`, `roleStarted`, `roleEnded`). Every trigger,
its fields and subjects: `docs/TRIGGERS.md` (generated from the trigger registry).

```json
"trigger": {
  "on": "villageLooted",
  "subject": "playerClan",
  "where": { "village.culture": "vlandia" },
  "chance": 0.5,
  "delivery": "conversation",
  "expiresAfterHours": 24,
  "cooldownDays": 7
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `on` | string | yes | a trigger name from docs/TRIGGERS.md; unknown → error |
| `subject` | string | no | who/where the event must be about: `self` (this companion), `player`, `playerParty` (anyone in the player's party), `playerClan`, `companion:<id>`, `hero:<stringId>`, `settlement:<stringId>`, `any`. Which ones a trigger accepts and its default: docs/TRIGGERS.md; others → error |
| `where` | object | no | payload path → value that must be equal: `"<field>"` (an entity field compares its id) or `"<field>.<property>"`, e.g. `"village.culture": "vlandia"`. Unknown field/property or a value of the wrong type → error |
| `chance` | number | no | 0..1, default 1 |
| `delivery` | string | no | `conversation` (default for conversations): the companion starts it on the map; `notification` (only choice for lines): one line in the message log; `inquiry`: a pop-up |
| `expiresAfterHours` | number | no | ≥ 0, default 24: queued and not delivered by then → dropped |
| `cooldownDays` | number | no | ≥ 0, default 0: in-game days before this item can fire again |
| `allowAbsent` | bool | no | default `false`: the companion must be in the player's party |
| `params` | object | polling triggers | `nearSettlement`: `settlement` (id), `radius` (> 0), `rearmRadius` (> radius; fires once per approach). `varCrossed`: `var` (an int variable of this companion or `mood`), `value` (int), `direction` (`up`/`down`). `roleStarted`/`roleEnded`: `role` (`governor`, `partyLeader`, `caravanLeader`, `alleyLeader`, `questAlternative`). Other triggers take none |

Content rules: a triggered conversation has no `entry` and starts with companion nodes. `notification`: the selected
start node is shown; it must end (`next: ["end"]`). `inquiry`: exactly one start companion node (the pop-up text) whose
`next` are one or more player nodes (the buttons; 1–2 → standard pop-up, 3 or more → multi-choice pop-up), each ending
(`next: ["end"]`); choosing one applies its effects. `when` is checked when the trigger fires and again at delivery.

Delivery limits (mod-wide): a companion interrupts (conversation or inquiry) at most once every 3 in-game days; at most
1 interruption per in-game day in total; at most 3 notifications per in-game day. Fired items wait in a queue (e.g.
until a battle is over), ordered by the selection rule, and are dropped after `expiresAfterHours`.

### Variables
Declared per companion in dialogs.json; permanent per campaign until changed.

```json
"variables": {
  "told_scar":  { "type": "bool", "scope": "shared" },
  "raids_seen": { "type": "int",  "scope": "local", "default": 0 }
}
```

| Field | Type | Required | Rule |
|---|---|---|---|
| key | string | yes | the name, `[a-z0-9_]+`; `mood` is built in and may not be declared |
| `type` | string | yes | `bool` or `int` |
| `scope` | string | no | `local` (default: only this companion uses it) or `shared` (other companions read/write it as `<companionId>.<name>`; this companion still owns it) |
| `default` | bool / int | no | `false` / `0` if absent; must match the type |

- **Built-in `mood`**: every companion has a local int `mood`, default 0. No game mechanic uses it yet.
- **References**: `name` = this companion's variable, `<companionId>.<name>` = another companion's **shared** one. Every
  reference must be declared, with the right type for where it is used (`flags`/`setFlags`… need a bool, `min`/`max`/`add`
  an int) — otherwise a validation error and the companion is skipped. Using another companion's `local` variable (or its
  `mood`) is an error.
- **Missing companion**: when the referenced companion is not loaded at all (no folder, skipped for errors, or a required
  module is missing) the reference is only a **warning**: the condition containing it is false as a whole (even inside
  `not`/`any`) and a write to it is skipped; the referencing companion still loads.
- **Never changes** (warning): a declared variable that no effect of any loaded companion writes and whose `default` is
  its type default (`false` / `0`) can never hold anything else.
- **Saving**: each written value is saved by companion and name. On load, values of variables that no longer exist are
  ignored, a value whose type changed falls back to the default, and new variables start at their default. A variable
  that was never written follows its current `default`. **Renaming a variable loses its saved value.**
- `flags` / `notFlags` / `setFlags` / `clearFlags` are shorthand for bool variables (the variables must be declared).
  Saves from before variables (M6 flags) are converted on load: a flag becomes `true` for a declared bool of the same name.

Error: nodes from which no `next` path (conditions ignored) leads to a node whose `next` is `["hub"]` or `["end"]` — the
conversation would loop there forever, so every node needs a way out.

Warnings: a node that cannot be reached from `start`; a `next` whose nodes all have conditions (if none holds the
conversation gets stuck — keep one without a condition); a transfer or `hasGold` naming a companion that is not loaded —
that companion still loads, but the actor's gold and items are unknown: a transfer from it is never offered and a
`hasGold` about it is false. Node texts follow the lang file rules below.

## `lang/<code>.json`

```json
{ "schemaVersion": 1, "lines": { "intro_01": "Greetings, Captain.", "backstory_01": "…" } }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `lines` | object | yes | local id → text (JSON string): every line and conversation node |

- File name: ISO 639-1 code (`en.json`, `tr.json`, `de.json`); anything else is an error. The game language is matched
  through its registered ISO codes (GAME_MAP Q14).
- `en.json` is required and needs a non-blank text for every line (error otherwise).
- A line missing from another language is a warning; English is shown instead (counted per language in the log).
- A text whose id matches no line is a warning (it is never shown).
- Voiced lines must not contain player-name tokens (`{PLAYER.NAME}`, `{PLAYER_NAME}`, …) in any language: voiced lines
  address the player as "Captain".
