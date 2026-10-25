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
| `lines[].slot` | string | yes | required companion slots: `intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`; required player slots (the player says these lines): `player_ask_about`, `player_ask_hire`, `player_hire_pay`, `player_hire_full`, `player_hire_later`; optional vanilla dialog points (see below); reserved, no effect yet: `leave_threat`, `relation_complaint`, `like_reaction`, `dislike_reaction`, `idle`. Several lines in one slot → one is picked at random each time |
| `lines[].voiced` | bool | no | default `false`; a player slot line must not be voiced (error) |
| `lines[].audio` | string | no | relative to the companion folder, no `..`, must end in `.ogg` (any case) — anything else is an error; default `audio/<local id>.ogg`; not allowed on player slot lines |
| `conversations` | array | no | authored conversations, see below |

Audio plays whatever the game language is; there is no per-language audio.

### Player slots
The player's side of the mod's meeting and hire flow belongs to each companion, so every companion can be talked to in
its own way. Player lines are never voiced, so name tokens are allowed; `player_hire_pay` may use
`{ALBOCARIUS_HIRE_COST}` (the companion's hire cost). The flow: `player_ask_about` → `backstory`; `player_ask_hire` →
`hire_offer` → `player_hire_pay` (enough gold and room in the clan) → `hire_accept`, or `player_hire_full` (clan full) /
`player_hire_later` → `hire_decline`.

### Vanilla dialog point slots

Every vanilla point where a wanderer/companion speaks (GAME_MAP Q18) is an optional slot. A companion with lines in such a
slot says one of them instead of the vanilla text; the vanilla condition, consequence and tokens (`{SALUTATION}`, `{TITLE}`,
`{SETTLEMENT_NAME}`, …) still apply. An absent or empty slot keeps the vanilla line (vanilla translations included). The full
list with the vanilla English text is `src/AlbocariusCompanions.Core/Rules/VanillaDialogPoints.cs`; the companion template
(`tools/companion-template/`) has one line per slot, pre-filled with that text:
greetings and hub (`greet_recent`, `greet`, `main_hub`, `anything_else`, `farewell`, `job_status`), roles and missions
(`role_*`, `mission_offer`, `mission_ask`, `forage_ask`, `rejoin_ok`), dismissal (`fire_reaction`, `fire_farewell`,
`fire_cancel_reply`), becoming a lord (`lord_*`), grievances (`grievance_*`), rescue and prison break (`rescue_*`,
`prison_break_*`) and following in a settlement (`follow_ok`, `gather_ok`, `follow_stop_ok`).

New companion: `tools/new-companion.sh <id>` (Windows: `tools\new-companion.bat <id>`) copies the template; it refuses an
existing folder, an invalid id and `_shared`.

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
| `entry` | string | yes | `hub`: the start player node(s) are options in the companion's conversation menu (listed above the vanilla options). `greeting`: when the player talks to the companion (already met), a start companion node opens the conversation instead of the usual greeting |
| `once` | bool | no | default `false`; `true`: offered until it has started once (per campaign, kept in the save) |
| `when` | object | no | conditions of the conversation, see below; for `greeting`, `"met": false` is an error (the first meeting is the intro) |
| `start` | string array | yes | node ids: player nodes for `hub` (several = several menu options), companion nodes for `greeting` (the first listed whose condition holds is said) |
| `nodes` | array | yes | at least one |
| `nodes[].id` | string | yes | local id like a line id (same namespace as the companion's lines; `hub` and `end` are reserved); key of the text in the lang files; full id `<companionId>_<id>` |
| `nodes[].speaker` | string | yes | `npc` (the companion) or `player` |
| `nodes[].voiced`, `nodes[].audio` | | no | as for lines; player nodes are never voiced |
| `nodes[].next` | string array | yes | what follows: node ids of the same conversation, or exactly one of `["hub"]` (back to the menu) / `["end"]` (the conversation closes). Player nodes → the companion's possible replies (the first listed whose condition holds is said). Companion nodes → either the player's options (every option whose condition holds is shown) or the companion's next line. Never both kinds in one `next`; a player node is never followed by a player node. Loops back to earlier nodes are allowed |
| `nodes[].when` | object | no | conditions of this node, see below; a node whose condition fails is not offered / not said |
| `nodes[].effects` | object | no | applied when the node is said, see below |

**Conditions** (`when`; every field that is set must hold): `met` (bool), `hired` (bool: in the player's clan), `inParty` (bool:
in the player's party), `minRelation` / `maxRelation` (−100..100: the companion's relation with the player, min ≤ max),
`minGold` (≥ 0: the player's gold), `flags` (all set), `notFlags` (none set; a flag may not be in both).

**Effects**: `relation` (non-zero, −100..100; the game scales positive changes by its own model and clamps the result),
`gold` (non-zero: negative = the player pays the companion — the node is offered only with enough gold; positive = the
player receives), `setFlags`, `clearFlags` (a flag may not be in both), `join` (`true`: the companion joins the player's
clan and party without payment — offered only while not hired and the clan has room).

**Flags** are names `[a-z0-9_]+` per companion, saved with the campaign; they start unset in a new campaign.

Warnings: a node that cannot be reached from `start`; a `next` whose nodes all have conditions (if none holds the
conversation gets stuck — keep one without a condition). Node texts follow the lang file rules below.

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
