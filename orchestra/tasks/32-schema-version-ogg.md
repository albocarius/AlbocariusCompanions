# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 32-schema-version-ogg: schemaVersion in the JSON files + OGG-only audio

## Context
Read `orchestra/PROJECT.md`, `orchestra/SPEC_CORE_GAPS.md` (§1 rule 1, T2.1 and T2.4) and `docs/SCHEMA.md`.
Core is netstandard2.0, no TaleWorlds references, JSON via Newtonsoft.Json (already referenced). Existing code to use, not duplicate:
- `src/AlbocariusCompanions.Core/Loading/CompanionLoader.cs` (strict Newtonsoft settings: unknown members are errors)
- `src/AlbocariusCompanions.Core/Models/CompanionDefinition.cs`, `DialogDefinition.cs`
- `src/AlbocariusCompanions.Core/Validation/IssueCodes.cs`, `ValidationIssue.cs`, `DialogChecks.cs` (`ValidateAudio`)

## Goal
### 1. schemaVersion (loader, one place)
- New file `src/AlbocariusCompanions.Core/Loading/SchemaVersions.cs`: `public static class SchemaVersions` with
  `public const int Current = 1;`, `public const string PropertyName = "schemaVersion";` and ONE method that checks the
  `schemaVersion` token of a parsed JSON document for a given folder + file name and returns the issue (or null). This is
  the only place in the code base that checks versions (player_lines.json and lang files will use it later).
- The loader parses each file (companion.json, dialogs.json) into a JObject first; invalid JSON → the existing
  `InvalidJson` error (unchanged). Then the version check; any version issue → loader error, that folder is skipped, other
  folders still load. Then deserialize with the existing strict settings.
- New issue codes in `IssueCodes` (Loader group): `MissingSchemaVersion = "missing_schema_version"` (absent or null),
  `InvalidSchemaVersion = "invalid_schema_version"` (not an integer: string, float, bool, array, object),
  `UnsupportedSchemaVersion = "unsupported_schema_version"` (integer ≠ Current, incl. 0 and negatives).
  Every message names the file (e.g. `broken/companion.json`), the word `schemaVersion`, and for unsupported the found
  value and the supported value, e.g. `broken/dialogs.json has schemaVersion 2; this version of the mod supports schemaVersion 1.`
- Models: `[JsonProperty("schemaVersion")] public int? SchemaVersion { get; set; }` on `CompanionDefinition` and `DialogFile`
  (first property), so the strict deserializer accepts the field and the value is kept.
### 2. OGG-only audio (validation)
- New issue code `InvalidAudioExtension = "invalid_audio_extension"` (dialogs.json group).
- In `DialogChecks.ValidateAudio`: when `audio` is set and passes the existing safe-path check, and its extension is not
  `.ogg` (case-insensitive; `x.ogg.wav`, `x.oggx`, no extension are all errors) → error. Voiced and unvoiced lines alike.
  An unsafe path (InvalidAudioPath) reports only that error, not both. Message contains the line id, the path and `.ogg`.
- Keep the silent skip in `Audio/VoicedLinesXml.cs` as is (defensive), do not edit that file.
### 3. Data and docs
- Add `"schemaVersion": 1` as the first field of `module/ModuleData/Companions/test_companion/companion.json` and `dialogs.json`.
- `docs/SCHEMA.md`: add the `schemaVersion` row to both tables (int, required, must be 1, example `1`) and the OGG rule to `audio`.
- Update unit-test JSON in `tests/AlbocariusCompanions.Core.Tests/**` that now fails because it lacks `schemaVersion`.

## Files allowed to edit
`src/AlbocariusCompanions.Core/Loading/**`, `src/AlbocariusCompanions.Core/Models/CompanionDefinition.cs`,
`src/AlbocariusCompanions.Core/Models/DialogDefinition.cs`, `src/AlbocariusCompanions.Core/Validation/IssueCodes.cs`,
`src/AlbocariusCompanions.Core/Validation/DialogChecks.cs`, `module/ModuleData/Companions/test_companion/*.json`,
`docs/SCHEMA.md`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/SchemaVersionTests.cs`, `AudioExtensionTests.cs` and the helper `Fixtures.cs`
(which now writes `"schemaVersion": 1`). All other existing acceptance tests must stay green. Read the tests first: they are
the precise contract.

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
