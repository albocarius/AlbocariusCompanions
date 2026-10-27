# Machine-readable exports (CLI)

For the editor (`gui/`, orchestra/SPEC_EDITOR.md §3): the schema, the trigger registry and validation results as JSON, so
the editor never hardcodes fields, conditions, effects or triggers. Code: `tools/AlbocariusCompanions.Cli`
(`SchemaExport`, `TriggerExport`, `DiagnosticExport`); tests: `tests/AlbocariusCompanions.Acceptance/ExportTests.cs`.

```
dotnet AlbocariusCompanions.Cli.dll export schema [outFile]
dotnet AlbocariusCompanions.Cli.dll export triggers [outFile]
dotnet AlbocariusCompanions.Cli.dll validate --json <path>
```

Without `outFile` the JSON goes to stdout. Exit codes as for the other commands: 0 ok, 1 validation errors (`validate`),
2 usage or an unwritable file. Every document has `exportVersion` (1), bumped on an incompatible change.

## `export schema`

Built from the Core model classes the loader reads (field names and types from their `[JsonProperty]`s) and the Core
rule classes (allowed values, ranges, patterns, slots). Descriptions, required flags and defaults are written in the CLI
in the words of docs/SCHEMA.md. Tests fail when a model property has no export entry (or the reverse), when an exported
field is not mentioned in docs/SCHEMA.md, or when the condition/effect catalogues do not cover their objects exactly.

| Key | Content |
|---|---|
| `schemaVersion` | the `schemaVersion` every file must carry |
| `files` | file name → object name: `companion.json` → `companion`, `dialogs.json` → `dialogs`, `lang/<code>.json` → `lang` |
| `objects` | object name → `{ "fields": [ … ] }`. Objects: `companion`, `equipment`, `voiceProfile`, `reaction`, `relation`, `dialogs`, `line`, `variable`, `conversation`, `node`, `conditions`, `goldCheck`, `itemCheck`, `effects`, `transfer`, `trigger`, `lang` |
| `conditions` | catalogue: `name`, `description`, `params` (the fields of `conditions` it uses, e.g. `var` → `var`, `min`, `max`, `eq`) |
| `effects` | catalogue, same shape, over the fields of `effects` |
| `effectOrder` | the order effects are applied in |
| `slots` | `companion`, `player`, `vanilla` (`slot`, `englishText`, `sentenceIds`), `required`, `optional` |
| `enums` | `voice`, `attributes`, `equipmentSlots`, `relationAttitudes`, `entries`, `speakers`, `reservedNext`, `variableTypes`, `variableScopes`, `builtinVariables`, `roles`, `deliveries`, `actors`, `subjects` |
| `patterns` | regexes: `companionId`, `localId`, `variableName`, `languageCode`, `playerToken` |
| `rules` | `englishCode`, `fullId`, `defaultAudio`, `relationRange`, `partyMoraleRange`, `reservedFolder` |

A field: `name`, `type`, `required`, `description`, and when they apply `default`, `allowed`, `min`, `max`, `pattern`.
Types: `string`, `int`, `number`, `bool`, `any` (a JSON value whose kind depends on context, e.g. `eq`), an object name
(`effects`, `transfer`, …), `<type>[]` (array) and `map<type>` (object with free keys). `allowed` on a `map` lists the
allowed keys (e.g. equipment slots); `min`/`max` on a `map` bound its values. Entries written `<companionId>`, `<id>`,
`<stringId>` in `allowed` stand for any id of that kind.

## `export triggers`

The trigger registry (`src/AlbocariusCompanions.Core/Triggers`), the same source as docs/TRIGGERS.md.

| Key | Content |
|---|---|
| `defaults` | `chance`, `expiresAfterHours`, `cooldownDays`, `lineDelivery`, `conversationDelivery` |
| `limits` | `interruptionCooldownDays`, `interruptionsPerDay`, `notificationsPerDay`, `standardInquiryMaxOptions` |
| `deliveries`, `roles` | allowed values |
| `subjectsWithId` | subject kinds written `<kind>:<id>` |
| `entityProperties` | entity kind → `properties`, `boolProperties` (the rest are text) |
| `triggers` | one per trigger: `name`, `category`, `common`, `description`, `polling`, `inMission`, `subjects` (kinds), `defaultSubject`, `subjectFields`, `fields` (`name`, `kind`, `entity`, `description`), `params` (`name`, `kind`, `required`, `description`; polling triggers), `deliveries` |

## `validate --json`

The same issues as `validate`, as one JSON object: `errors`, `warnings`, `diagnostics`. A diagnostic: `severity`
(`error`|`warning`), `code` (Core `IssueCodes`), `folder`, `file` (`companion.json`, `dialogs.json`, `lang/<code>.json`,
or null), `conversation`, `node` (a conversation node or line id, local), `message`. Core issues carry only a folder and a
message, so `file`/`conversation`/`node` are read from the message's fixed wording and the issue code; a part that
cannot be told is null.
