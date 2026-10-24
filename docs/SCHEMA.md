# JSON schema reference

Companion data lives in `module/ModuleData/Companions/<folder>/`. Every schema change updates this file
(SPEC_CORE_GAPS §1.1). The loader is strict: unknown fields are an error.

## `companion.json`

| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| `schemaVersion` | int | yes | must be `1` (the only version this build of the mod supports) | `1` |
| `id` | string | yes | `[a-z0-9_]+`, equals the folder name, unique | `"test_companion"` |
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
| `hireCost` | int | yes | ≥ 0 | `500` |
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
{ "schemaVersion": 1, "lines": [ { "id": "test_companion_intro_01", "slot": "intro", "text": { "en": "Greetings, Captain." }, "voiced": true } ] }
```

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | must be `1` (the only version this build of the mod supports) |
| `lines` | array | yes | |
| `lines[].id` | string | yes | `<companionId>_<slot>_<nn>`, globally unique; localization key `{=id}` and voice-over id |
| `lines[].slot` | string | yes | required slots: `intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`; optional (no effect yet): `leave_threat`, `relation_complaint`, `like_reaction`, `dislike_reaction`, `idle` |
| `lines[].text` | object | yes | language code → text; `en` required; no `{PLAYER…}` tokens in voiced lines |
| `lines[].voiced` | bool | no | default `false` |
| `lines[].audio` | string | no | relative to the companion folder, no `..`, must end in `.ogg` (any case) — anything else is an error; default `audio/<line id>.ogg` |

Audio: `audio/<line id>.ogg`; an optional Rhubarb lipsync file with the same name and `.xml` sits next to it.
