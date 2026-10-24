# SPEC — Warband Companions (Bannerlord mod) — Foundation run

> Working name: `AlbocariusCompanions` (rename freely; keep it consistent everywhere).
> This spec is for the orchestrator. Read it fully before assigning any task.

## 0. Context

- Goal of the mod: bring Warband-style unique companions into Bannerlord. They are a **separate category from vanilla wanderers** but reuse vanilla wanderer movement/tavern mechanics. Each companion is defined purely by data; adding a new companion must require **no code changes**, only a new data folder.
- Goal of THIS run: a foundation where **one placeholder companion** can be added, spawned, found in a tavern and hired via a basic dialog. **No audio** in this run.
- Development happens on this machine (Linux). The game runs on a separate Windows machine. **Nobody can run the game during this run.** "Done" means: builds + tests pass + behavior is grounded in the decompiled game code.

### Paths (fixed for this run)

| Name | Path | Notes |
|---|---|---|
| `GAME_DLLS` | `~/bannerlord/dlls` | Copied game DLLs. Read-only. Never commit. Layout: `bin/*.dll` (main game) and `Modules/<Native|SandBoxCore|SandBox|StoryMode|CustomBattle>/*.dll`, plus the War Sails DLC module folder if the user copied it. |
| `GAME_XML` | `~/bannerlord/moduledata` | Copied vanilla ModuleData XML (SandBox, SandBoxCore). Read-only reference for ids, templates and formats. |
| `DECOMP_DIR` | `~/bannerlord/decomp` | Decompiled game source. Read-only reference. **Never commit, never copy into the repo.** |
| Repo | current repo root | |

## 1. Hard rules

1. **Never invent game API.** Every TaleWorlds class/method/field used in code must be documented in `docs/GAME_MAP.md` with the file in `DECOMP_DIR` it came from. If it is not in GAME_MAP, it is not allowed in code.
2. **Never commit or paste decompiled TaleWorlds code** into the repo. Summaries and signatures in GAME_MAP are fine; copied method bodies are not.
3. **Commit at the end of every phase** (and at meaningful checkpoints inside a phase) with message `phase N: <summary>`.
4. A task is done only when `dotnet build` passes for the whole solution and `dotnet test` passes.
5. **Stop condition:** if the same build/test failure survives 3 fix attempts, stop that task, write the problem into `docs/BLOCKERS.md` (error, what was tried, best guess), commit, and move on to the next task that does not depend on it.
6. Do not invent Warband companion stats, lore or dialog. Use the placeholder companion defined in §5.
7. Out of scope for this run: audio playback, voice generation, generating translation/language files, likes/dislikes reactions, companion-to-companion relation logic, MCM/settings UI. Their **schema fields exist** (see §4) but nothing reads them yet beyond validation.
8. **JSON is the single source of truth** for all companion data. If the game requires native XML (character templates, voiced lines, etc.), **generate it from JSON at build time** into `dist/`. Generated XML is never hand-edited and never committed. There must never be two hand-maintained sources for the same data.
9. **Optional content (War Sails DLC and any other optional module):** the mod must work with and without the DLC.
   - **Never reference DLC assemblies** in code (no `<Reference>` to DLC DLLs, no DLC types or static members). DLC DLLs/decompiled source are for reading ids only.
   - All game content referenced from JSON (skills, cultures, traits, items, equipment templates, settlements) is resolved **by string id at runtime**.
   - Resolution failure is never fatal: missing skill/trait → log warning, skip that entry; missing culture → use `cultureFallback`; missing equipment template → use `equipment.fallbackTemplateId`; if a required value and its fallback are both missing → log error, skip **that companion only**, keep loading the rest.
   - Every runtime lookup goes through one resolver class in the Game layer, so null-handling lives in one place.

## 2. Solution layout

```
AlbocariusCompanions.sln
src/
  AlbocariusCompanions.Core/        # netstandard2.0 — NO TaleWorlds references
  AlbocariusCompanions.Game/        # net472 — references Core + GAME_DLLS
tests/
  AlbocariusCompanions.Core.Tests/  # net8.0 (or newest installed SDK), xunit
module/                          # module skeleton: SubModule.xml, ModuleData/
docs/
  GAME_MAP.md
  BLOCKERS.md
dist/                            # build output in final module layout (gitignored)
```

- **Core** holds everything that can be tested without the game: models, JSON loading, registry, validation, slot definitions, line-id rules.
- **Game** is a thin layer: SubModule, campaign behavior, Harmony patches, dialog registration, logging. Keep it as small as possible.
- Game DLL references: `<Reference>` with `HintPath` into `GAME_DLLS` and `<Private>false</Private>`. Do not use NuGet reference-assembly packages.
- Harmony: compile against `Lib.Harmony` (NuGet, `Private=false`/`ExcludeAssets=runtime`). At runtime the mod depends on the **Bannerlord.Harmony** module, declared as a dependency in `SubModule.xml`.
- JSON: check whether `Newtonsoft.Json.dll` exists in `GAME_DLLS`. If yes, reference that exact file (Private=false) from Game, and use the matching Newtonsoft version from NuGet in Core/tests. If no, record it in BLOCKERS.md and pick the simplest option that works on net472 without shipping conflicting assemblies.
- A post-build step assembles `dist/AlbocariusCompanions/` in the exact Bannerlord module layout:
  ```
  dist/AlbocariusCompanions/
    SubModule.xml
    bin/Win64_Shipping_Client/AlbocariusCompanions.Game.dll
    bin/Win64_Shipping_Client/AlbocariusCompanions.Core.dll
    ModuleData/Companions/<companion_id>/...   # copied JSON + audio
    ModuleData/...                              # any XML generated from JSON (only if required, see rule 8)
  ```

## 3. Phases

### Phase 1 — Game map (`docs/GAME_MAP.md`)

Investigate `DECOMP_DIR` and answer each question with: class, method/field names, source file path, and a short plain-language explanation. Mark anything uncertain as **UNCERTAIN**.

1. **Wanderer spawning:** which campaign behavior spawns wanderers, when it runs (new game, daily, weekly…), how it decides how many to spawn (fixed number, derived from towns, etc.), and **exactly where/how it counts existing wanderers**.
2. **Which heroes count as wanderers** in that count (occupation? flags? hired vs unhired? alive?).
3. **Tavern placement and movement:** how wanderers are placed in a settlement and moved between settlements over time.
4. **Hero creation:** the API vanilla uses to create a wanderer hero from a character template; how culture, age, gender, body properties, skills, attributes, traits and equipment are applied.
5. **Character templates:** how wanderer templates are defined (XML in which module, which element/attributes), and whether a mod can create a hero at runtime **without** an XML template.
6. **Hiring:** the vanilla wanderer hire dialog flow and how a wanderer joins the player's clan (method used, cost handling, companion limit check).
7. **Dialog API:** how campaign behaviors register dialog lines (methods, token/state system, conditions, consequences, priority).
8. **Campaign lifecycle hooks:** which events to use for "new game created", "game loaded", "daily tick", and how campaign behaviors persist data (`SyncData` or equivalent).
9. **Existing saves:** what happens if the mod is enabled on an existing campaign — which hook to use to spawn missing companions.
10. **Harmony target** for excluding mod companions from the wanderer count (from Q1/Q2): the exact method to patch, prefix vs postfix, and why.
11. **Generation decision (from Q5):** state explicitly whether companions can be created at runtime directly from JSON data, or whether a native XML template must be generated at build time (rule 8). Recommend one, with reasons.
12. **Voice-over resolution** (mapping only — no audio implementation this run): `DefaultVoiceOverModel.GetSoundPathForCharacter` or equivalent, how the voiced-lines XML is loaded (`language_data.xml` → voiced lines XML), how a dialog text with a `{=ID}` prefix is matched to a voice entry, the file naming / fallback order (culture, gender, voice type, id), required file format (OGG expected) and lipsync XML handling. Reference guide (may be outdated, verify against source): https://docs.bannerlordmodding.lt/guides/custom_audio_with_lipsync/
13. **War Sails DLC:** the DLC's module id/folder name; how to detect at runtime whether it is loaded **without referencing its assemblies**; the string ids of the skills, cultures and traits it adds; whether War Sails runs as a separate campaign/game mode from the base sandbox and how to detect which one is active; what the runtime string-id lookup returns for a missing object (null vs exception). If the DLC module was not copied, mark the whole question UNCERTAIN and continue.
14. **Localization:** how module language files are structured and loaded (folder layout, file format, how a language is registered), how a text of the form `{=ID}Fallback text` is resolved, what is shown when a translation for the active language is missing, and how localization interacts with voiced lines (is the voice lookup per language, what plays when the UI language has no voice files).

Done when: every question has an answer or an explicit UNCERTAIN with what would resolve it.

### Phase 2 — Core

1. **Models** matching the schema in §4.
2. **Loader:** scans `ModuleData/Companions/*/`, loads `companion.json` and `dialogs.json` per folder. A broken companion folder must not break the others: collect errors, skip that companion, continue.
3. **Registry:** read-only lookup by companion id; also exposes the set of all mod companion ids (used later by the Harmony patch).
4. **Slots:** a single global list of dialog slots defined in Core (see §4.3). Required slots for this run are marked.
5. **Validation** (returns a list of errors/warnings, never throws):
   - companion id: lowercase `[a-z0-9_]+`, unique across all folders, equals folder name
   - required fields present; numeric ranges sane (skills 0–330, attributes 1–10, age 18–80 — adjust ranges to GAME_MAP findings and note it)
   - line ids unique globally and follow `<companionId>_<slot>_<nn>` (e.g. `test_companion_intro_01`)
   - every required slot has at least one line
   - **voiced lines must not contain player-name tokens** (any `{PLAYER...}`/`{PLAYER_NAME}`-style token). Voiced lines address the player as "Captain".
   - unknown slot names → error
   - `cultureFallback`, if present, must differ from `culture`; `equipment.fallbackTemplateId`, if present, must differ from `templateId`
   - `text` is an object keyed by language code; `en` is **required** and non-empty; keys must be lowercase language codes (`en`, `tr`, `de`, …)
   - the player-name-token rule for voiced lines applies to **every** language in `text`
   - `voice` must be one of the vanilla voice types found in GAME_MAP (expected: `curt`, `earnest`, `ironic`, `softspoken`)
   - `audio` field, if present, must be a relative path inside the companion folder (no `..`)
6. **Missing-audio report:** a function that lists voiced lines whose audio file does not exist. (Used later by the voice pipeline; must exist and be tested now.)
7. **Tests** for all of the above, including: valid companion, each validation rule failing on its own, one broken folder + one valid folder (valid one still loads), duplicate ids across folders, missing-audio report.

Done when: `dotnet test` green and coverage of every validation rule.

### Phase 3 — Game layer

Implement only with APIs documented in GAME_MAP.

1. **SubModule.xml** in `module/`: id, name, version `0.1.0`, dependencies (Native, SandBoxCore, Sandbox, StoryMode as needed per GAME_MAP, plus Bannerlord.Harmony), and the SubModule class entry.
2. **SubModule class:** loads the registry on startup from the module's own `ModuleData/Companions`, logs validation results, registers the campaign behavior, applies Harmony patches.
3. **Logging:** own log file (e.g. `AlbocariusCompanions.log` in a writable location found via GAME_MAP or the standard Bannerlord logs folder). Log startup, every loaded/skipped companion with reasons, every spawn, every hire, every caught exception. Never let an exception from this mod crash the game: wrap entry points.
4. **Campaign behavior:**
   - On new game: create each registry companion as a hero using the vanilla wanderer creation path (per GAME_MAP Q4/Q5) and place it like a vanilla wanderer (Q3). Mod companions then move between taverns using vanilla mechanics.
   - On game load: spawn any registry companion that does not yet exist (existing-save support, Q9). Never duplicate a companion.
   - Persist the mapping companion id → hero (by stable id, not by name).
5. **Wanderer count exclusion:** Harmony patch per GAME_MAP Q10 so mod companions are not counted toward the world wanderer target. Vanilla wanderer behavior must otherwise be unchanged.
6. **Dialogs:** register dialogs generically from `dialogs.json` (no companion-specific code). Every line's text is registered as `{=<line id>}<en text>` (localization-ready; no language files generated this run). For this run: intro, backstory, hire offer, accept, decline. Hiring uses the vanilla join-clan path and respects the vanilla companion limit and `hireCost`.
7. Character creation follows the Q11 decision: either runtime creation from JSON, or a build step that generates the native XML template from JSON into `dist/` (rule 8). Use `voice` from JSON for the character's voice type.
8. Ignore `audio`, `voiceProfile`, `likes`, `dislikes`, `relations` at runtime for now.

Done when: solution builds, `dist/AlbocariusCompanions/` has the correct layout, and every game API call is traceable to GAME_MAP.

## 4. Data schema

### 4.1 `ModuleData/Companions/<id>/companion.json`

```json
{
  "id": "test_companion",
  "name": "Test Companion",
  "isFemale": false,
  "voice": "earnest",
  "age": 32,
  "culture": "<culture string id, per GAME_MAP>",
  "cultureFallback": null,
  "bodyProperties": null,
  "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
  "skills": { "<skill string id>": 100 },
  "traits": { "<trait id>": 1 },
  "equipment": { "templateId": "<equipment roster/template id, per GAME_MAP>", "fallbackTemplateId": null },
  "hireCost": 500,
  "voiceProfile": { "reference": null, "style": null },
  "likes": [],
  "dislikes": [],
  "relations": []
}
```

- `cultureFallback` / `equipment.fallbackTemplateId`: optional; used when the primary id does not exist at runtime (e.g. DLC culture/items without the DLC). Recommended whenever the primary id comes from optional content.
- `bodyProperties`: `null` = use a reasonable default per GAME_MAP. Later filled with a real BodyProperties key.
- Field names for skills/traits/culture/equipment must use the game's own string ids found in Phase 1.
- `likes` / `dislikes`: future shape `{ "event": "<event id>", "reaction": <int>, "slot": "<dialog slot>" }`. Validate shape only if non-empty.
- `relations`: future shape `{ "companion": "<id>", "attitude": "friend|rival" }`. Validate shape only if non-empty; referenced companion may not exist yet → warning, not error.

### 4.2 `ModuleData/Companions/<id>/dialogs.json`

```json
{
  "lines": [
    { "id": "test_companion_intro_01", "slot": "intro", "text": { "en": "...", "tr": "..." }, "voiced": true, "audio": "audio/test_companion_intro_01.ogg" }
  ]
}
```

- `text`: language code → string. `en` is required and is the fallback for every other language. Other languages are optional.
- Every line's localization key **is its line id**. At runtime a line is registered as `{=<line id>}<en text>`, so the same id later resolves both the translation and the voice-over (see GAME_MAP Q12/Q14). In a later run, a build step will generate the game's language files from `text` (rule 8).
- `audio` optional. Missing audio file at runtime = silent fallback (later). Default audio path convention: `audio/<line id>.ogg`. A lipsync file `audio/<line id>.xml` may sit next to it. In a later run, a build step will copy/rename these into the game's voiced-lines layout and generate the voiced-lines XML (rule 8).

### 4.3 Slots

Required this run: `intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`.
Defined but optional: `leave_threat`, `relation_complaint`, `like_reaction`, `dislike_reaction`, `idle`.

## 5. Placeholder companion

Create `module/ModuleData/Companions/test_companion/` with obviously placeholder content (name "Test Companion", generic one-line dialog per required slot (`en` only, plus one line with a `tr` translation to exercise the schema), voiced lines say "Captain", no audio files). Pick a valid culture, skills and equipment template **from GAME_MAP findings**. This is a test fixture, not lore.

## 6. End-of-run report

Write `docs/RUN_REPORT.md`: what was completed per phase, what is UNCERTAIN in GAME_MAP, open BLOCKERS, and a **morning test checklist** for the human:
1. Mod appears in the launcher and loads without errors.
2. Log file shows the companion loaded and validated.
3. New campaign: companion exists and appears in a tavern.
4. Hire dialog works, cost is deducted, companion joins the clan.
5. Vanilla wanderer count looks unchanged.
6. Save → load: no duplicate companion.
7. Existing save + mod enabled: companion appears.
