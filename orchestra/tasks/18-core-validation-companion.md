# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 18-core-validation-companion: companion.json validation

## Context
Read `orchestra/PROJECT.md` and SPEC §2 Phase 2 / §4 in `orchestra/SPEC.md`. Core is netstandard2.0, no TaleWorlds references,
JSON via Newtonsoft.Json (already referenced). Models, issue codes and rule constants already exist — use them, do not duplicate:
- `src/AlbocariusCompanions.Core/Models/*.cs` (CompanionDefinition, DialogFile/DialogLine, CompanionPackage)
- `src/AlbocariusCompanions.Core/Validation/ValidationIssue.cs`, `IssueCodes.cs` (issues are keyed by **folder name**: `ValidationIssue.Folder`)
- `src/AlbocariusCompanions.Core/Rules/*.cs` (DialogSlots, LineIds, CompanionRules: regexes, ranges, voice types)

## Goal
Implement `CompanionChecks.Validate(IReadOnlyList<CompanionPackage>)` in `src/AlbocariusCompanions.Core/Validation/CompanionChecks.cs`. Returns issues, never throws (also not for an empty `new CompanionDefinition()`). All issues are errors except `UnknownRelationTarget` (warning). `Folder` = package.FolderName.
- id: missing/blank → `MissingField`; not `CompanionRules.IdPattern` → `InvalidId`; != folder name → `IdFolderMismatch`; same id in several packages → `DuplicateId` on **every** package with that id.
- Required (missing or blank string → `MissingField`, message contains the JSON field name): id, name, isFemale, voice, age, culture, attributes, skills, equipment, equipment.templateId (message contains `templateId`), hireCost.
- attributes: each of `CompanionRules.AttributeNames` required (`MissingField`, message contains the attribute name); unknown key → `UnknownAttribute` (message contains the key); value outside AttributeMin..AttributeMax → `OutOfRange`.
- skills: every value in SkillMin..SkillMax else `OutOfRange`. age in AgeMin..AgeMax else `OutOfRange`. hireCost >= 0 else `OutOfRange`. Bounds inclusive.
- voice must be in `CompanionRules.VoiceTypes` (case-sensitive) else `InvalidVoice` (a missing voice is only `MissingField`).
- cultureFallback equal to culture → `SameAsFallback` (message contains `cultureFallback`); equipment.fallbackTemplateId equal to templateId → `SameAsFallback` (message contains `fallbackTemplateId`). null fallbacks are fine.
- likes/dislikes entries: event non-blank, reaction present, slot known (`DialogSlots.IsKnown`) — otherwise one `InvalidReaction` per bad entry.
- relations entries: companion non-blank and attitude in `CompanionRules.RelationAttitudes` else `InvalidRelation`; a valid entry whose companion id is not among the packages' ids → `UnknownRelationTarget` warning.
- Optional fields (cultureFallback, bodyProperties, traits, voiceProfile, likes, dislikes, relations, fallbackTemplateId) may be absent/null.
- Exactly one error per broken rule: a test that breaks one rule expects exactly one error.
Files allowed to edit: `src/AlbocariusCompanions.Core/Validation/CompanionChecks.cs`, `tests/AlbocariusCompanions.Core.Tests/**`.

## Acceptance tests (do not edit)
`tests/AlbocariusCompanions.Acceptance/Core/CompanionValidationTests.cs` and the helper `tests/AlbocariusCompanions.Acceptance/Core/Fixtures.cs`.
Read the tests first: they are the precise contract (codes, severities, ordering, messages that must contain a field/slot/key name).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
(dotnet 10.0.113 on this machine sometimes aborts with "Internal CLR error" — that is infra, just rerun.)
You may add unit tests under `tests/AlbocariusCompanions.Core.Tests/`.
