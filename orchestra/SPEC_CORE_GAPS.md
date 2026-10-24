# SPEC — AlbocariusCompanions — Milestone: Core gaps

> Follow-up to the foundation run (`orchestra/SPEC.md`). That spec's hard rules (§1) and the rules in `orchestra/HARD_RULES.md` still apply unless this document explicitly changes them.
> Goal: close the known gaps of the working prototype. **No new features.**

## 0. Scope

### In scope
1. Close the previous round (reports, logs).
2. Small fixes: OGG validation, hire cost rule, `requiredModules`, `schemaVersion`.
3. Authoring ergonomics: id derivation, scaffold script.
4. Dismissal bug.
5. Slot variation.
6. Player lines in JSON + language file generation.

### Out of scope (do not start, do not design)
- likes / dislikes / relations behavior
- flag / condition system (`setFlags`, `conditions`, `priority`, triggers, random comments)
- author-defined dialog branches
- companion-initiated conversations
- loading companions from other modules

Write any idea that comes up during the run into `docs/IDEAS.md` instead of implementing it.

## 1. Rules (additions)

1. Every schema change updates: the models, the loader, validation, the placeholder/test companions in the repo, and `docs/SCHEMA.md` (create it if missing — a plain reference of every JSON field, its type, whether it is required, and an example).
2. Every new game API used must be added to `docs/GAME_MAP.md` first (foundation rule still holds).
3. Existing behavior that is not listed here must not change. In particular: **reload behavior stays as it is** — if a companion hero already exists in the save in any state, the mod never respawns or re-activates it on load.
4. Commit per task (`feat(NN-slug): ...`) and `milestone core-gaps: <summary>` at the end.

## 2. Tasks

### T1 — Close the previous round
- Complete `docs/RUN_REPORT.md` for the foundation run, including a "known gaps" section (the list this spec addresses).
- Record out-of-plan work done during the day (voice/lipsync integration, name fix, log change) in `PLAN.md` and `WORKER_LOG`.
- Done: both files updated and committed.

### T2 — Small fixes
1. **OGG validation:** `audio` with any extension other than `.ogg` → validation **error** (not a silent skip). Test it.
2. **Hire cost:** the hire condition is `gold >= hireCost` (not `>`). A companion with `hireCost: 0` can be hired with 0 gold.
3. **`requiredModules`** replaces the fallback mechanism:
   - Remove `cultureFallback` and `equipment.fallbackTemplateId` from schema, models, validation and code.
   - Add optional `requiredModules: string[]` to `companion.json` (module ids, e.g. `["NavalDLC", "EOE_Armory"]` — use the real ids found in GAME_MAP).
   - At load: if any listed module is not loaded → log which module is missing, skip **that companion only** (no spawn).
   - If the companion's hero already exists in the save and a required module is now missing → do not touch the hero, only log a warning.
   - Module detection must not reference the other module's assemblies (use the method documented in GAME_MAP Q13).
4. **`schemaVersion`:** required integer field at the top of `companion.json`, `dialogs.json` and the new `player_lines.json` (T6). Current value: `1`. Unknown/greater version → validation error with a clear message. Loader keeps a single place where versions are checked.
- Done: tests for each rule, test companions updated, build + tests green.
5. In-game notifications: Show validation/runtime warnings and errors as in-game info messages (vanilla API, document it in GAME_MAP; no new mod dependencies). Queue messages produced before the UI is ready and display them once a campaign is started or loaded. Show each distinct message once per session. On campaign start/load, show a one-line summary: companions loaded / skipped, with “see log” when anything was skipped. Info-level log lines are file-only. Errors red, warnings yellow.

### T3 — Authoring ergonomics
1. **Folder name is the companion id:** remove `id` from `companion.json`. The folder name must match `[a-z0-9_]+` (validation error otherwise).
2. **Short line ids:** in `dialogs.json`, lines use a local id (`intro_01`). The loader derives the full id `<companionId>_<localId>` (e.g. `jeremus_intro_01`).
   - Local ids must be unique within the companion; full ids must be unique globally.
   - The full id is still used everywhere outside the JSON: localization key (`{=<full id>}`), audio file name ( `audio/<local id>.ogg` , validate it), logs.
   - Migrate the existing companions in the repo to the new format.
3. **Scaffold:** `tools/new-companion.sh <id>` and `tools/new-companion.bat <id>` creates `module/ModuleData/Companions/<id>/` from a template (`tools/companion-template/`) with valid placeholder content for every required slot, ready to pass validation. Refuses to overwrite an existing folder.
- Done: migrated companions load, a freshly scaffolded companion passes validation (test it), build + tests green.

New task T4.0 (before T4): Extract every vanilla dialog point where a wanderer or companion speaks or is spoken to (meeting, hiring, dismissal, and any other) into GAME_MAP: token flow, conditions, the vanilla text and its {=id}. Add each as a slot. The companion template (tools/companion-template/) contains all of them pre-filled with vanilla text, keeping the vanilla {=id} for lines the author does not change. Empty or untouched slot → vanilla behavior.
T4.0 addition: Extracted vanilla lines go into the companion template as plain English text in lang/en.json, using our own id system like any other line. No vanilla string ids, no textRef, no reuse of vanilla translations or vanilla voice-over. The template is a working starting point and a reference of every dialog point; authors are expected to replace the text.

### T4 — Dismissal bug
Problem: when the player dismisses a mod companion from the clan, the hero disappears from the map. Expected (Warband behavior): the companion becomes a wanderer again and resumes vanilla tavern movement, hireable again.

1. **GAME_MAP first** — new section, every answer with decomp file path + class/method:
   - Which action/event handles the player dismissing a companion; what happens to the hero (state, party, settlement).
   - How a player dismissal can be distinguished from every other way a hero leaves the clan (death, capture, other mods' removal/exile, kingdom events, etc.).
   - How a hero is turned back into an unhired wanderer and placed in a tavern using vanilla mechanics.
2. **Fix:** only for mod companions and only on **player dismissal**: return the companion to wanderer state and place it like a vanilla wanderer (same placement rules as initial spawn). Any other removal reason → do nothing.
3. Log the dismissal and the re-placement.
- Done: GAME_MAP section written, fix implemented with GAME_MAP-backed APIs only, build green. Add to the morning checklist: dismiss → companion reappears in a tavern → can be rehired; reload after dismissal → no duplicate.

### T5 — Slot variation
- If a slot has several lines for a companion, pick one at random each time the slot is used.
- Keep it deterministic-testable: the picking logic lives in Core behind an injectable random source; test it.
- Done: tests green.

### T6 — Player lines in JSON + language files
1. **Player lines file:** `module/ModuleData/Companions/_shared/player_lines.json`:
   ```json
   {
     "schemaVersion": 1,
     "lines": [
       { "id": "player_ask_about", "slot": "ask_about", "text": { "en": "Tell me about yourself." } }
     ]
   }
   ```
   - Move every player-side string that is currently hardcoded in C# into this file. No player-facing text may remain hardcoded.
   - Slot names for player lines are part of the global slot list in Core.
   - `_shared` is a reserved folder name: the companion loader must skip it, and `new-companion.sh` must refuse it.
   - Player line ids are used as-is (no companion prefix); they must not collide with any companion full id.
   - Same `text` rules as companion lines (`en` required, other languages optional).
2. **Language file generation (rule 8 of the foundation spec):** at build time, generate the game's module language files from the `text` maps of all companion lines and player lines, for every language that appears in any `text`. Output only to `dist/`. Use the format, folder layout and registration documented in GAME_MAP Q14.
   - Missing translation for a line in a language → the line is simply absent from that language's file (the game falls back per Q14). Log a count of missing translations per language at build time.
   - If the game's XSD for language files exists in `~/bannerlord/xmlschemas`, validate the generated files against it in a test.
3. Out of scope for this task: `conditions`, `setFlags`, `priority`, per-companion player line overrides.
- Done: no hardcoded player text in C#, generated language files in `dist/`, tests green.
T6 Changes!! IMPORTANT IF you have any questions regarding ambiguity about T6 ask before start
Text moves out of dialogs.json / player_lines.json into per-language files: <folder>/lang/<code>.json, a flat map localId → text. en.json is required and complete; other languages optional. The text field is removed from the schema. example structure below.
```
jeremus/
  dialogs.json      # id, slot, voiced, audio
  lang/
    en.json         # { "intro_01": "Greetings, Captain...", ... }
    tr.json         # { "intro_01": "Selam, Kaptan...", ... }
```
Validation: missing line in en.json → error; missing line in another language → warning (falls back to en); translation id not present in dialogs → warning.
Runtime/build approach (pick active language file at runtime vs generate game language XML) is decided by GAME_MAP Q14, including what happens to voiced lines when the UI language is not English. Map our language codes to the game’s language ids in GAME_MAP.

### T7 — Report
Write `docs/RUN_REPORT_CORE_GAPS.md`: per task what was done, what is UNCERTAIN, open BLOCKERS, and a test checklist for the human:
1. Mod loads, log shows all companions loaded/validated with schemaVersion 1.
2. Companion with a missing `requiredModules` entry is skipped with a clear log line.
3. Hire with exactly `hireCost` gold works.
4. Dismiss → companion reappears in a tavern → rehire works.
5. Reload after dismissal → no duplicate, no respawn.
6. Slot with several lines → different lines across conversations.
7. Switch game language to Turkish → translated lines show in Turkish, untranslated lines fall back to English.
8. Voiced lines still play with lipsync (no regression from the id change).
