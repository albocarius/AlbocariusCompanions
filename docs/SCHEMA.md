# JSON schema reference

Companion data lives in `module/ModuleData/Companions/`. Every schema change updates this file
(SPEC_CORE_GAPS §1.1). The loader is strict: unknown fields are an error. Every JSON file starts with
`"schemaVersion": 1` (the only version this build of the mod supports; missing, not an integer or another value → error).

```
Companions/
  _shared/                     reserved: the player's lines (never a companion)
    player_lines.json
    lang/en.json, tr.json, …
  jeremus/                     folder name = companion id, [a-z0-9_]+
    companion.json
    dialogs.json               ids, slots, voice flags — no text
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
{ "schemaVersion": 1, "lines": [ { "id": "intro_01", "slot": "intro", "voiced": true } ] }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `lines` | array | yes | |
| `lines[].id` | string | yes | local id `[a-z0-9_]+`, unique within the companion. The full id `<companionId>_<id>` (e.g. `jeremus_intro_01`) is the localization key, voice-over id and log name, and must be unique across all companions and player lines |
| `lines[].slot` | string | yes | required slots: `intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`; optional vanilla dialog points (see below); reserved, no effect yet: `leave_threat`, `relation_complaint`, `like_reaction`, `dislike_reaction`, `idle`. Several lines in one slot → one is picked at random each time |
| `lines[].voiced` | bool | no | default `false` |
| `lines[].audio` | string | no | relative to the companion folder, no `..`, must end in `.ogg` (any case) — anything else is an error; default `audio/<local id>.ogg` |

Audio plays whatever the game language is; there is no per-language audio.

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

## `lang/<code>.json`

```json
{ "schemaVersion": 1, "lines": { "intro_01": "Greetings, Captain.", "backstory_01": "…" } }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `lines` | object | yes | line id → text (JSON string). Companion folders use local ids, `_shared` uses player line ids |

- File name: ISO 639-1 code (`en.json`, `tr.json`, `de.json`); anything else is an error. The game language is matched
  through its registered ISO codes (GAME_MAP Q14).
- `en.json` is required and needs a non-blank text for every line (error otherwise).
- A line missing from another language is a warning; English is shown instead (counted per language in the log).
- A text whose id matches no line is a warning (it is never shown).
- Voiced lines must not contain player-name tokens (`{PLAYER.NAME}`, `{PLAYER_NAME}`, …) in any language: voiced lines
  address the player as "Captain".

## `_shared/player_lines.json`

```json
{ "schemaVersion": 1, "lines": [ { "id": "player_ask_about", "slot": "ask_about" } ] }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `lines[].id` | string | yes | `[a-z0-9_]+`, unique, used as-is (no prefix); must not equal any companion line's full id |
| `lines[].slot` | string | yes | every player slot needs at least one line: `ask_about`, `ask_hire`, `hire_pay`, `hire_full`, `hire_later` |

Texts live in `_shared/lang/<code>.json` with the same rules as companion lang files. Player lines are never voiced, so
tokens are allowed; `hire_pay` may use `{ALBOCARIUS_HIRE_COST}` (the companion's hire cost). A missing or invalid
`_shared` folder is an error: companions still load, but no companion dialogs are registered.
