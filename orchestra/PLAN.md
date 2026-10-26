Active milestone: **M8 vanilla guards** — source of truth `orchestra/SPEC_VANILLA_GUARDS.md` (section M8 below).
Previous: M7 behavior foundation (`orchestra/SPEC_BEHAVIOR_FOUNDATION.md`), M6 conversations (no spec file), M5 core gaps (`orchestra/SPEC_CORE_GAPS.md`); SPEC §1 rules and
`orchestra/HARD_RULES.md` still apply. The foundation run (M1–M4) is archived at the end of this file.
Commit policy: per task `feat(NN-slug): ...` / `docs(NN-slug): ...`; at the end `milestone vanilla-guards: <summary>`.
Stop condition (SPEC rule 5): 3 failed fixes of the same error → `docs/BLOCKERS.md`, commit, move on.

Owner legend: `self` = orchestrator, `worker` = local model via orchestra/worker.sh.
Acceptance tests for worker tasks live in `tests/AlbocariusCompanions.Acceptance/` (orchestrator-owned).
Every worker task file (`orchestra/tasks/NN-slug.md`) starts with the full text of `orchestra/HARD_RULES.md` — user requirement.

## Status
- Foundation done and played in game (see archive + docs/RUN_REPORT.md). Repo clean at 5730899 "phase 4: run report".
- M5 done 2026-10-24 (docs/RUN_REPORT_CORE_GAPS.md): Core.Tests 201, Acceptance 319. Not deployed (Windows laptop closed);
  in-game checklist in the report, incl. renaming the audio files to local ids.
- M6 done 2026-10-25 (docs/RUN_REPORT_CONVERSATIONS.md): per-companion player lines, conversations. Core.Tests 215,
  Acceptance 359. Not tested in game.
- M7 done 2026-10-26 (docs/RUN_REPORT_BEHAVIOR_FOUNDATION.md): variables, conditions, transfers, authoring warnings, CLI,
  console commands, GAME_MAP Q21–Q32. Core.Tests 264, Acceptance 620. In-game checklist 2–5 passed (user, 2026-10-26).
- M8 approved 2026-10-26 (E1, E2 as planned; E3 and V3 with the conditions below). In progress.
- Helpers live in the session scratchpad only (next_core.sh, winrun.sh, xsdcheck); nothing depends on them.

## M5 rules (SPEC_CORE_GAPS §0–§1, applied to every task)
- **No new features.** Out of scope: likes/dislikes/relations behavior, flags/conditions/priority/triggers, author dialog
  branches, companion-initiated talks, loading companions from other modules. Ideas go to `docs/IDEAS.md`.
- Every schema change updates models + loader + validation + repo companions (test_companion, test fixtures) +
  `docs/SCHEMA.md` in the same task.
- Every new game API goes into `docs/GAME_MAP.md` (section + API index row) before code uses it (ApiIndexTests).
- **Reload behavior is frozen:** a companion hero that exists in the save in any state is never respawned or re-activated
  on load (today: only the JSON name is re-applied — existing behavior, keep it).
- Never overwrite the user's audio on Windows. Deploys to the Windows game folder only with the user's approval.

## M5 — Core gaps (SPEC_CORE_GAPS §2)
Order follows dependencies: schema changes first (T2/T3 ids), then the slot list (T4.0) and language files (T6), the
scaffold last because its template must contain the final slot list and lang format.

### T1 — close the foundation round
- [x] 31-close-foundation (self) — RUN_REPORT "known gaps" aligned with this spec's list; WORKER_LOG rows for the
  out-of-plan work (7981805 name fix, aa503f1 voice/lipsync, 394f143 town names in log); fix stale boxes in the archive
  (M1 15–22, M2/M3 milestone ends were done). Also create `docs/IDEAS.md` and `docs/SCHEMA.md` (current schema, every
  field: type, required, example) so later tasks only update them.

### T2 — small fixes
- [x] 32-schema-version-ogg (worker; merged with 33) — schemaVersion: — Core: one `SchemaVersions` check point used by the loader; `schemaVersion` (int,
  required, current 1) at the top of companion.json and dialogs.json (player_lines.json and lang files join in 49). Missing / not int /
  greater than supported → validation error with a clear message. Accept: one test per case; test_companion updated.
  OGG part (was 33): `audio` with an extension other than `.ogg` → validation **error** (today a silent skip
  in VoicedLinesXml). Accept: `.wav`, no extension, `.OGG` ok.
- [x] 34-hire-cost-gte (self) — CompanionDialogs hire_pay condition `Gold >= hireCost`; hireCost 0 hireable with 0 gold.
  Our own condition (not vanilla), no new API.
- [x] 35-required-modules-core (worker) — remove `cultureFallback` and `equipment.fallbackTemplateId` (models, validation
  codes, CompanionResolver, ResolverTests, test_companion, SCHEMA.md); add optional `requiredModules: string[]`
  (non-empty strings, no duplicates). Tests may use `EOE_Armory` as an example of a module that is not loaded. Core `RequiredModules.Missing(package, Func<string,bool> isModuleActive)`.
  Accept: removed fields rejected as unknown by the strict loader, filter tests with a fake predicate.
- [x] 36-required-modules-game (self) — at OnSubModuleLoad: `ModuleHelper.IsModuleActive(id)` (GAME_MAP Q13, add API row),
  skip only that companion with a log line naming the missing module (no template, no spawn, no dialogs). A saved hero of
  a skipped companion: untouched, one warning (spawn pass lists mapped ids that have no template). Example ids in docs:
  `NavalDLC` (GAME_MAP Q13); `EOE_Armory` is only an illustrative example (user).
- [x] 37-gm-notifications (self) — GAME_MAP entry: `InformationManager.DisplayMessage` + `InformationMessage(text, Color)`,
  when the UI can show it (campaign started/loaded). Verify in decomp.
- [x] 38-notifications-core (self; done together with 39 while the worker ran 32) — Core `NotificationQueue`: collects warnings/errors from ModLogger (info stays
  file-only), dedupes by text per session, drains once; summary line builder "N companions loaded, M skipped — see log".
  Accept: dedupe, order, severity, summary with/without skips.
- [x] 39-notifications-game (self) — hook queue into ModLogger, flush on OnSessionLaunched / OnGameLoadFinished
  (errors red, warnings yellow), summary once per campaign start/load.

### T3 — authoring ergonomics
- [x] 40-folder-is-id (self; one schema migration together with 41 and 49, so fixtures and test_companion change once) — remove `id` from companion.json; id = folder name, `[a-z0-9_]+` else error; `_shared` is
  reserved and skipped by the loader. Migrate test_companion + fixtures, SCHEMA.md.
- [x] 41-local-line-ids (self) — dialogs.json lines use local ids (`intro_01`); loader derives full id
  `<companionId>_<localId>`; local unique per companion, full unique globally (incl. player line ids, T6). Full id used for
  `{=…}`, voice-over id, logs; audio default becomes `audio/<local id>.ogg` (VoicedLinesXml, MissingAudioReport,
  validation of the line-id rule replaced). Migrate test_companion. The user renames the Windows audio files
  (`test_companion_intro_01.ogg` → `intro_01.ogg`); listed in the checklist, never done by us.

### T4.0 — vanilla dialog points as slots
- [x] 42-gm-vanilla-dialog-points (worker as 42-gm-q18-vanilla-dialog-points → self verify) — new GAME_MAP section: every vanilla dialog point
  where a wanderer/companion speaks or is spoken to (meeting, hiring, dismissal, others): token flow, conditions, vanilla
  English text and its `{=id}`, decomp path + method for each.
- [x] 43-slots-from-vanilla (self) — add each point as a slot in the Core slot list (required vs optional). Empty slot →
  vanilla behavior (our line not registered, vanilla line stays visible for mod companions; review the Q7 hire-option
  Harmony prefix against this). Template content is written in 49/50 (plain English in `lang/en.json`, our id system, no
  vanilla string ids / textRef / vanilla voice).

### T4 — dismissal bug
- [x] 44-gm-dismissal (self: researched while the worker ran Q18) — GAME_MAP section: which action/event handles player dismissal and
  what happens to the hero; how to tell player dismissal from death, capture, other mods, kingdom events; how vanilla
  turns a hero back into an unhired wanderer and places it in a tavern.
- [x] 45-dismissal-fix (self) — mod companions only, player dismissal only: back to wanderer state + placement like
  initial spawn (PickSpawnTown); log dismissal and re-placement. Other removal reasons → nothing. Must not break the
  frozen reload rule. Checklist: dismiss → tavern → rehire; reload after dismissal → no duplicate.

### T5 — slot variation
- [x] 46-slot-picker-core (self, while the worker ran 35) — Core picker: several lines in a slot → one at random per use, behind an injectable
  random source (`IRandomSource`). Accept: deterministic tests with a fake source, single-line slot, empty slot.
- [x] 47-slot-variation-game (self) — wire the picker into CompanionDialogs (pick per conversation turn; the chosen
  line's `{=full id}` drives text and voice), Game random source = `MBRandom`.

### T6 — player lines + per-language files (decisions below)
Decision from 48 (GAME_MAP Q14 note 2): dialog lines are registered once as `{=!}{<VAR>}`; the condition sets `<VAR>` to
`{=<full id>}<text in the active language>` (vanilla VOICED_LINE pattern). This also gives T5 its random pick and keeps voice
and lipsync working in every language; a mid-session language change needs no re-registration.
- [x] 48-gm-q14-language-decision (self) — extend Q14: how to read the active game language at dialog registration
  (API + when it is valid, what happens on a language change mid-session); mapping ISO 639-1 code → game language id
  (`en`→`English`, `tr`→`Türkçe`, …). Voice stays language-independent (decision 5).
- [x] 49-lang-files-core (self) — schema: `text` removed from dialogs/player lines; `<folder>/lang/<code>.json` =
  `{ "schemaVersion": 1, "lines": { "<localId>": "<text>" } }`; `en.json` required and complete. Validation: missing in en
  → error; missing in another language → warning (falls back to en); id not in dialogs → warning; file name not ISO 639-1
  → error. Player-name token rule for voiced lines applies to the lang files. `_shared/player_lines.json` (`schemaVersion`,
  lines with id/slot) + `_shared/lang/<code>.json`; player slots join the global slot list; player ids as-is, collision
  with companion full ids → error. Core `TextFor(lineId, languageCode)` with en fallback. Migrate test_companion, SCHEMA.md.
- [x] 50-player-lines-game (self) — every player-facing string in C# moves to player_lines; dialogs registered with the
  active language's text (en fallback) via 48; missing-translation count per language logged at start-up. Acceptance test
  that greps Game sources for hard-coded English dialog text. **No language XML is generated** (decision 2); only the
  voiced-lines XML is still written at start-up.

Done as one self change (commit 9253894): 36, 40, 41, 47, 49, 50 — the schema migration and the runtime dialog rewrite share
every fixture, so they went in together. Missing translations are logged as one count per language (file only), not shown
in game. The user renames the Windows audio files to local ids (`intro_01.ogg`).

### Scaffold (T3.3, after 43 and 49)
- [x] 52-scaffold (self) — `tools/new-companion.sh <id>` + `.bat`, template in `tools/companion-template/` with every
  slot pre-filled (vanilla English text from 42 in `lang/en.json`, our ids). Refuses an existing folder, an invalid id and
  `_shared`. Accept: test scaffolds into a temp dir and the result passes CompanionCatalog validation.

### T7 — report
- [x] 53-run-report-core-gaps (self) — `docs/RUN_REPORT_CORE_GAPS.md`: per task done / UNCERTAIN / BLOCKERS + the 8-item
  human checklist of SPEC_CORE_GAPS T7 (+ dismissal items from T4, + user renames audio files to local ids).
- [x] Milestone end: full TEST_CMD, commit `milestone core-gaps: <summary>`. No push.

### Decisions (user, 2026-10-24 — answers to the T6 questions)
1. `schemaVersion` in **every** JSON file, lang files included: `{ "schemaVersion": 1, "lines": { "<localId>": "<text>" } }`.
2. **No build step and no text XML.** When dialogs are registered, the text comes from `lang/<code>.json` of the active game
   language, else from `en.json`. At start-up only the voiced-lines XML keeps being written.
3. Lang file names are ISO 639-1 (`en.json`, `tr.json`); the code ↔ game language id mapping is fixed after the GAME_MAP work (48).
4. Only an absent or empty slot falls back to vanilla.
5. Audio is bound to the dialog line, not to a language: `audio/<id>.ogg` from the companion's audio folder plays with its line in every game language.
- `EOE_Armory` was only an example; it may be used as a "module not loaded" case in tests.
- The user renames the audio files on Windows after 41.

## M6 — per-companion player lines + companion conversations (user request 2026-10-25, no spec file)
User: "make the player lines separate for each companion … extra/special dialogs with companion and player, the dialog
system should support it"; answers: remove `_shared` (player lines per companion, in the scaffold template too) and build
a complete conversation system now (not a basic one). Overrides the M5 out-of-scope items "author dialog branches,
companion-initiated talks, flags/conditions". Vanilla dialog points stay as they are (user: mod companions do not use
vanilla dialogs; the template override is fine).
- [x] 54-player-lines-per-companion (self) — player slots `player_ask_about`, `player_ask_hire`, `player_hire_pay`,
  `player_hire_full`, `player_hire_later` in each companion's dialogs.json (required, never voiced), texts in its lang
  files; `_shared` removed (a leftover `_shared` folder is skipped with a warning); template + test_companion + fixtures.
- [x] 55-gm-conversations (self) — GAME_MAP Q20: hub/greeting entry tokens and priorities, `hub` return tokens, save of
  `List<string>`, relation/gold/join actions; API rows.
- [x] 56-conversations (self) — `conversations` in dialogs.json: entry `hub`/`greeting`, `once`, `when` (met, hired,
  inParty, min/maxRelation, minGold, flags, notFlags), nodes (npc/player, `next` = nodes | `hub` | `end`, `when`,
  `effects`: relation, gold, setFlags, clearFlags, join; voiced/audio like lines). Core: models, validation, graph →
  sentence edges, condition evaluation; Game: registration, flags saved in the campaign; test_companion example; SCHEMA.md.
- [x] 57-report (self) — docs/RUN_REPORT_CONVERSATIONS.md with the in-game checklist; milestone commit, no push.

## M7 — Behavior foundation (SPEC_BEHAVIOR_FOUNDATION)
Rules for every task: SPEC §0 out-of-scope list (no triggers, no companion-initiated talks, no cooldowns, **no selection
rules** — current candidate order stays as it is, no companion↔companion relation/morale mechanics, no editor); ideas →
`docs/IDEAS.md`. Every schema change updates models + loader + validation + test_companion + fixtures + scaffold template +
`docs/SCHEMA.md` in the same task. Every new game API → GAME_MAP section + API index row before code uses it.
Ordering constraints: I3 items after R9 verified; I7 after R10 verified.

### Design decisions taken here (spec gaps; change at the plan gate if wrong)
- D1 Writing variables: effects `"set": { "<var>": <bool|int> }` and `"add": { "<var>": <int> }` (int only, may be negative).
  `setFlags`/`clearFlags` stay as sugar for `set` true/false. Shared vars of another companion are written as `<companionId>.<name>`.
- D2 Variable names: `[a-z0-9_]+`, `mood` reserved (built-in, may not be declared). A dotted reference `<id>.<name>` is
  resolved across the catalog after per-folder validation (user, plan gate 2026-10-25):
  - referenced companion **loaded** but the variable is undeclared or `local` (or the type does not match) → error, the
    referencing companion is skipped;
  - referenced companion **not loaded** (missing folder, skipped, required module missing) → warning; at runtime a condition
    containing that reference is false, an effect writing to it is skipped, the referencing companion still loads.
    Test: Ahmet references `jeremus.told_scar`, Jeremus absent → Ahmet loads, the conversation is not offered.
- D3 Condition shape: `when` stays an object; in addition to today's keys it may hold `all: [ … ]`, `any: [ … ]`,
  `not: { … }` (each element is again a `when` object) and `var` + `min`/`max`/`eq`. One object = AND of everything in it.
  Today's `minGold` stays as sugar for `hasGold: { who: player, min }`.
- D4 Save: variables saved under a new key `_variables` as `List<string>` of `"<companionId>.<name>=<value>"` (the Q20-
  verified type; no new save types). The existing `_conversationFlags` key keeps the "once/started" markers; old flag keys
  (`<companion>:<flag>`) found there on load become `true` for a declared bool of that name, else are dropped (logged).
  Values with a type that no longer matches the declaration → default (logged).
- D5 Transfers keep today's "implied condition": a node whose transfer source cannot pay/has not the item is not offered
  (as `gold < 0` today). The hasGold/hasItem warning is about making it explicit for authors. Relation effect unchanged.
- D6 `gold` effect is removed in 71 after the sample is migrated (spec I3); `giveItem`/`takeItem` never existed, so I5's
  rules are written against `transfer` (from ≠ world without matching hasGold/hasItem → warning).
- D7 I4 already exists (M6: `voiced`/`audio` on nodes, `audio/<local id>.ogg`): task 76 only adds missing tests if any.
- D8 CLI: new project `tools/AlbocariusCompanions.Cli` (net8.0, `RollForward=Major` so it runs on the installed .NET 10),
  added to the solution; Mermaid export logic lives in Core (`Dialogs/MermaidExport`) so it is unit-tested.

### Track R — research → GAME_MAP "Behavior system" (worker drafts, self verifies every claim against decomp)
One task per question, numbered GAME_MAP Q21–Q31; each claim cites decomp path + class/method; unresolved → UNCERTAIN +
what would resolve it. No code. Acceptance per task: GameMapTests section-present test (heading + Answer/Evidence/Uncertain).
- [x] 58-gm-q21-relations (worker) — R1 hero↔hero relations.
- [x] 59-gm-q22-morale (worker) — R2 party morale / per-hero mood.
- [x] 60-gm-q23-events (worker) — R3 part 1 (combat, war). First attempt timed out with nothing written → split.
- [x] 60b-gm-q32-events (worker) — R3 part 2 (wages, food, morale, settlements, sieges, kingdom, clan, death) → GAME_MAP Q32.
- [x] 61-gm-q24-traits (worker) — R4 player traits.
- [x] 62-gm-q25-open-conversation (worker) — R5 programmatic conversation on the map.
- [x] 63-gm-q26-inquiry (worker) — R6 inquiry pop-ups.
- [x] 64-gm-q27-time (worker) — R7 campaign time + save timestamps.
- [x] 65-gm-q28-location (worker) — R8 location & surroundings.
- [x] 66-gm-q29-items-gold (worker, **first in queue**) — R9 items, inventory, gold between heroes, companion inventories.
- [x] 67-gm-q30-console (worker, **second in queue**) — R10 console commands.
- [x] 68-gm-q31-misc (worker) — R11 clan tier, war state, hero in party.

### Track I — implementation (self unless noted; worker research runs in parallel)
- [x] 69-variables-core (self) — I1 + I2 in Core: `variables` block (types/scopes/defaults, built-in `mood`), D1 effects,
  D3 conditions (all/any/not, var min/max/eq, flags sugar on declared bools incl. dotted shared refs), validation (undeclared,
  foreign local, type mismatch, bad names, min>max…, catalog-level cross-companion check D2), `VariableStore` (typed values,
  get/set/add, defaults, save list encode/decode with D4 migration), `ConversationConditions` over the store. Then the
  sample migration part of I8 (declare `told_scar`, `got_coin`). Accept: Acceptance tests per rule + Core.Tests.
- [x] 70-variables-game (self) — Game: `_variables` SyncData + D4 load, context/effects over `VariableStore`, log lines.
  Build-only verifiable; in-game items in the report.
- [x] 71-transfer-core (worker; self writes acceptance tests first — user) — I3 Core part (model, validation, evaluation): `transfer` effect + `hasGold`/`hasItem` conditions model/validation, actor
  rules, missing-check warning, `gold` effect → sugar → removed after sample migration (D6). Item fields validated per R9
  (waits for 66 verified). Accept: tests per actor/kind combination.
- [x] 72-transfer-game (self, after 66) — gold between heroes / world, items via party inventory per R9; missing item id at
  runtime → skipped + warning.
- [x] 73-validation-graph (worker) — I5: cycle with no path to `hub`/`end` → error (dangling `next` and unreachable already
  exist from M6; tests confirm). Accept: staged tests.
- [x] 74-validation-variables (as 74-validation-authoring) (worker) — I5: declared variable never written and default = type default → warning
  "never changes"; transfer from ≠ world without hasGold/hasItem on the node or its path entry → warning (D6).
- [x] 75-validation-repeatable (self) — I5 repeatable reward warning (path analysis: positive gold/item/relation to the
  player, conversation not `once`, no condition on that path that the path itself makes false). No "shadowing" rule.
- [x] 76-voiced-nodes (self) — I4 check (D7): tests for `voiced` on npc nodes → voiced-lines XML + missing-audio report.
- [x] 77-mermaid-core (worker) — Core `MermaidExport.Render(package, conversationId?)`: flowchart per conversation, player
  vs npc node shapes/classes, `hub`/`end` terminals, conditions on edges, effects in node labels. Accept: golden-string tests.
- [x] 78-cli (worker) — I6 CLI project: `validate <path>` (one companion folder or a `Companions` dir; errors/warnings,
  exit 1 on errors), `mermaid <companionDir> [conversation]`. Accept: tests call the CLI entry point on fixtures.
- [x] 79-debug-commands (self, after 67) — I7 `albocarius.list`, `.get`, `.set`, `.reset`, `.reload` only if safe (else
  noted); each wrapped in `Safe.Run`; parsing/formatting in Core and unit-tested.
- [x] 80-sample-content (self) — I8 rest: `spare_coin` exploit fixed (both paths set `got_coin`), explicit hasGold, sample on
  `transfer`; `validate` on `module/ModuleData/Companions` → zero errors/warnings (acceptance test); scaffold template updated.
- [x] 81-report (self) — `docs/RUN_REPORT_BEHAVIOR_FOUNDATION.md` (Track R summary + UNCERTAINs, Track I per task,
  BLOCKERS, the 5-item checklist); milestone commit. No push.

### M7 Report
- Built: Track I complete (69–80, D1–D8, D2 as changed by the user; `gold` key removed); Track R Q21–Q32 verified
  (R3 split into Q23 + Q32 after a timeout).
- Tests: Core.Tests 264/264, Acceptance 620/620.
- Worker: 15 tasks (11 research incl. the split, 4 implementation), all PASS on the first attempt after the split;
  review fixes on 71 (nested hasGold actor warning) and 78 (CLI Newtonsoft runtime dependency).
- Not verified: everything in game (checklist in the report); console availability in retail (Q30 UNCERTAIN).
- Open questions: none blocking; see "Decisions to confirm" in the report.

## M8 — Vanilla guards (SPEC_VANILLA_GUARDS)
Rules: no new features; every exclusion only for mod companions (registry ids, `ModRuntime.IsModHero`), vanilla heroes
unchanged; each prevented action logged once per occurrence (what was prevented, companion id); every patched/used game
member gets a GAME_MAP section + API index row first; Harmony prefixes fall back to vanilla on exceptions (existing style).
Version: SubModule.xml + Game assembly `0.1.0` → **`0.2.1`** at the end (user: middle +1 for the M7 release, rightmost +1
for this spec), SubModuleXmlTests updated.

Decisions taken here (change at the gate if wrong):
- E1 V1 target: prefix on `CompanionRolesCampaignBehavior.OnHeroRelationChanged` (private, the only caller of the removal,
  Q21): for a pair where the companion side is a mod hero, skip the method (it only does the removal check) and log when the
  vanilla condition would have removed it (new relation < −10 after a negative change). `KillCharacterAction` itself is not
  patched (death and other removals stay vanilla).
- E2 V3 "exclude" items are implemented one Harmony prefix each, in the same style; "decide" items are only listed.
- E3 V4 raw relation: read `CharacterRelationManager.GetHeroRelation` / `Hero.GetBaseHeroRelation`, write
  `Hero.SetPersonalRelation` (clamped, silent) — per Q21; console `albocarius.relation <a> <b> [value]` with the text side in
  Core (like DebugCommands).
  User at the gate: pick **one** read API for the raw value and justify it in GAME_MAP; verify and note whether
  `SetPersonalRelation` fires the relation-changed event.
- V3 (user at the gate): every "exclude" classification is verified by the orchestrator in decomp, and the report gives a
  one-line reason next to each.

Tasks:
- [ ] 82-v1-relation-removal (self) — E1: GAME_MAP Q21 addendum (patch target, why not KillCharacterAction), API row,
  prefix + log; checklist item.
- [ ] 83-gm-q33-grievances (worker → self verifies) — V2 research: CompanionGrievanceBehavior (triggers, conversation
  opening — Q25 — texts, consequences: relation, leaving, removal) → GAME_MAP Q33.
- [ ] 84-v2-grievance-exclusion (self, after 83) — mod companions never get grievances or their consequences; log.
- [ ] 85-gm-q34-companion-audit (worker → self verifies; split by area if it times out) — V3: every vanilla behavior /
  action / model targeting player companions or wanderers (IsPlayerCompanion, Occupation.Wanderer, CompanionOf, clan
  companions) → table: what, trigger, consequence, keep / exclude / decide → GAME_MAP Q34. Non-DLC sources only.
- [ ] 86-v3-exclusions (self, after 85) — prefixes only for "exclude" rows; nothing for "decide".
- [ ] 87-v4-relation-helper (self) — E3: GAME_MAP Q21 addendum (raw vs effective for two companions of the player clan),
  Game helper `CompanionRelations` (raw get / silent set) used for every companion↔companion access, console
  `albocarius.relation`; Core text side unit-tested.
- [ ] 88-v5-dismissal-check (self, after 82/84/86) — confirm the T4 dismissal patch (`companion_fire_on_consequence` →
  `RemoveCompanionAction.ApplyByFire` + tavern placement) is not hit by any new exclusion and vice versa; GAME_MAP Q19
  addendum; fix if needed.
- [ ] 89-report (self) — version 0.2.1, `docs/RUN_REPORT_VANILLA_GUARDS.md` (per task, V3 table with the *decide* list
  highlighted, UNCERTAIN, BLOCKERS, 5-item checklist), milestone commit, deploy only if asked.

## Decisions already fixed
- Character templates: generated from JSON at runtime in memory + debug copy on disk (user, 2026-10-24; SPEC rule 8 amendment).
- Toolchain: Microsoft .NET SDK 10.0.402 in `~/.dotnet-ms` (distro SDK crashes).
- XML formats: reference `xmlschemas/` (game XSDs, user-provided 2026-10-24). Every XML generated at build time is validated against its schema in tests where one exists.
- JSON: `Newtonsoft.Json.dll` 13.0.1 exists in `~/bannerlord/dlls/bin` → Game references that file
  (Private=false); Core/tests use NuGet `Newtonsoft.Json` 13.0.1.
- Harmony: NuGet `Lib.Harmony` 2.4.2 compile-only (ExcludeAssets=runtime; latest, works with game 1.4.8 per user); runtime dependency on
  `Bannerlord.Harmony` module in SubModule.xml. No Harmony DLL in `~/bannerlord/dlls`.
- Decompiled sources lack TaleWorlds.Library / Localization / Engine / ModuleManager; when needed,
  decompile with `ilspycmd` into the session scratchpad (outside the repo, never committed).

## Archive — foundation run (orchestra/SPEC.md)

### M1 — Game map (SPEC Phase 1) → `docs/GAME_MAP.md`
Discovery = worker (one task per question), verification = self (user decision 2026-10-24).
Task files: `orchestra/tasks/NN-gm-qNN-*.md`. Worker edits only its `## QN` section, Status DRAFT.
Acceptance: `GameMapTests` (format, cited paths exist, cited symbol occurs in cited file, no long code blocks);
the `Questions` list is extended by one entry per assigned task.
Verification (self): open every cited location, fix wrong claims, set `Status: VERIFIED`, commit `docs(gm-qNN): verified`.
Missing DLLs (Library, Localization, ModuleManager, Engine, SaveSystem) decompiled into `~/bannerlord/decomp` (= `decomp/`).
- [x] 01-gm-q01-wanderer-spawn (worker → verify)
- [x] 02-gm-q02-wanderer-count (worker → verify)
- [x] 03-gm-q03-tavern-placement (worker → verify)
- [x] 04-gm-q04-hero-creation (worker → verify) — also value ranges for validation
- [x] 05-gm-q05-char-templates (worker → verify) — also culture/equipment ids for placeholder
- [x] 06-gm-q06-hiring (worker → verify)
- [x] 15-gm-q07-dialog-api (worker → verify)
- [x] 16-gm-q08-lifecycle (worker → verify)
- [x] 17-gm-q09-existing-saves (worker → verify)
- [x] 18-gm-q10-harmony-target (worker → verify)
- [x] 19-gm-q11-generation-decision (worker → verify)
- [x] 20-gm-q12-voice-over (worker → verify) — also vanilla voice type ids
- [x] 21-gm-q13-war-sails (worker → verify)
- [x] 22-gm-q14-localization (worker → verify)
- [x] Milestone end: all sections VERIFIED or UNCERTAIN with resolution; commit `phase 1: game map`.
- [x] 15-gm-q15-logging-paths (worker → verify) — added for M3 tasks 24/26 (writable folder, logging, exception safety)
- [x] 16-gm-q16-submodule-lifecycle (worker → verify) — added for M3 tasks 23/30 (SubModule.xml format, module ids, override order)

### M2 — Core (SPEC Phase 2)
Acceptance tests for 16–20b are written and staged in `orchestra/staged-tests/` (they fail with NotImplementedException);
each file is moved into `tests/AlbocariusCompanions.Acceptance/Core/` only when its task is assigned, so TEST_CMD stays green for other workers.
Task files `orchestra/tasks/16..20b-*.md` are ready. Shared helper `Core/Fixtures.cs` is already in the acceptance project.
- [x] 15-core-models (self) — models for §4 schema, `ValidationIssue` (severity, code, companionId, message),
  Slot definitions (§4.3), line-id rule helper, Newtonsoft package. Design work → self.
  Edit: `src/AlbocariusCompanions.Core/**`, Core csproj. Accept: build + a few unit tests.
  Done in a temporary worktree, merged into `orchestra/foundation` (cea5c0a), worktree removed. Public API stubs for 16–20b throw NotImplementedException.
- [x] 16-core-loader (worker) — scan `<root>/*/`, load companion.json + dialogs.json, collect errors,
  skip broken folder, continue. Accept: valid+broken folder, missing file, malformed JSON.
- [x] 17-core-registry (worker) — `CompanionRegistry`: read-only lookup by id, `Ids` set. Accept: lookup, unknown id, ids set, duplicate ctor guard.
- [x] 18-core-validation-companion (worker) — companion.json rules (id format/uniqueness/folder name,
  required fields, ranges per GAME_MAP, cultureFallback≠culture, fallbackTemplateId≠templateId,
  voice ∈ vanilla voice types, likes/dislikes/relations shape). Accept: one failing test per rule + valid companion.
- [x] 19-core-validation-dialogs (worker) — line-id format + global uniqueness, required slots,
  unknown slot, `text` language keys + `en` required, no player-name tokens in voiced lines (all languages),
  `audio` relative & no `..`. Accept: one failing test per rule.
- [x] 20-core-missing-audio (worker) — report voiced lines whose audio file is missing (default path
  `audio/<line id>.ogg` when `audio` absent). Accept: present/missing/default path cases.
- [x] 20b-core-catalog (worker) — `CompanionCatalog.Load`: loader + validator + registry; folders with errors skipped by folder name. Needs 16–19.
- [x] 20c-core-npc-xml (worker; tests staged, task file ready) — Core generator: resolved companion data → `NPCCharacters` XML document (template
  `occupation="Special"`, `is_template="true"`, id = companion id, culture/voice/age/is_female, skills, traits, equipment set).
  Acceptance: output validates against `xmlschemas/NPCCharacters.xsd`; field mapping tests. (SPEC rule 8 amendment, GAME_MAP Q11.)
- [x] 21-placeholder-companion (self) — `module/ModuleData/Companions/test_companion/` per SPEC §5 with
  ids from GAME_MAP; acceptance test that loads it from disk and validates clean.
- [x] Milestone end: full tests; commit `phase 2: core`.

### M3 — Game layer (SPEC Phase 3)
Untestable without the game. Rule: every API call traces to a VERIFIED GAME_MAP entry; self reviews worker diffs against GAME_MAP.
- [x] 22-game-build (self) — Game csproj: HintPath refs into `~/bannerlord/dlls` (Private=false),
  Newtonsoft from game bin (Private=false), Lib.Harmony compile-only; post-build target assembling
  `dist/AlbocariusCompanions/` (SubModule.xml, bin/Win64_Shipping_Client/{Game,Core}.dll, ModuleData/Companions/**).
  **Core's Newtonsoft copy must not go to dist** (user decision). Accept: acceptance test on dist layout incl. no Newtonsoft.Json.dll in dist.
  Done: `GameDlls` property (default `$(HOME)/bannerlord/dlls`, build errors if missing), Lib.Harmony 2.4.2 compile-only (latest, works with game 1.4.8 per user),
  Core's Newtonsoft `PrivateAssets=all`, `AssembleDist` target copies only Game+Core DLLs and mirrors `module/`.
  Tests: `DistLayoutTests` (layout, only mod DLLs, module mirror, Core has no TaleWorlds refs, no NavalDLC refs, Newtonsoft 13.0.0.0).
- [~] 22b-xml-schema-tests (self) — superseded: NPCCharacters XSD tests live in 20c (generated in memory, not in dist);
  SubModule.xml has no XSD (structural test in 23); no language/voiced-line files this run.
  Original text: — acceptance tests validating every XML in `dist/` against the game XSDs:
  for each `Xmls/XmlNode` in dist SubModule.xml, validate its file against `xmlschemas/<id>.xsd` when that schema exists
  (same rule as `ModuleHelper.GetXsdPath`); generated character template → `NPCCharacters.xsd`; language files /
  voiced lines → their schema per GAME_MAP Q12/Q14, or a structural test where no schema exists (state which).
  SubModule.xml has no XSD → structural test (task 23). Fails if `xmlschemas/` is missing.
- [x] 23-game-submodule-xml (worker; tests staged, task ready) — `module/SubModule.xml` (id, name, version 0.1.0, deps from GAME_MAP,
  Bannerlord.Harmony, SubModule class). Accept: XML parse test checking ids/version/deps.
- [x] 24-core-logging (worker; tests staged, task ready) — Core `ModLogger` (System.IO, never throws) + `Safe.Run` guard.
  Game glue (path via `PlatformFileType.User`, GAME_MAP Q15) is part of 30.
- [x] 25-core-resolver (worker; tests staged, task ready) — SPEC rule 9 logic in Core behind `IGameObjectLookup`; the Game implementation
  (`MBObjectManager.GetObject<T>` + `Equipment.IsItemFitsToSlot`) is part of 26. Original scope:
  fixed items: `GetObject<ItemObject>` + `Equipment.IsItemFitsToSlot`, drop misfits with a warning, all dropped → template set (GAME_MAP Q5).
- [x] 26-game-hero-creation (self) — code in Game/{GameObjectLookup,CompanionTemplates,CompanionHeroFactory}.cs; builds against the game DLLs;
  runtime behaviour only verifiable in-game. GAME_MAP "API index" + ApiIndexTests added (SPEC rule 1). Original scope: — at `AfterRegisterSubModuleObjects`: resolve ids (25), generate XML (20c), `MBObjectManager.LoadXml`,
  write debug copy to the log folder; hero via `HeroCreator.CreateSpecialHero` + `SetNewOccupation(Wanderer)`, attributes via `HeroDeveloper` (GAME_MAP Q4/Q5/Q11).
- [x] 27-game-behavior-spawn (worker, review self; task ready, skeleton class in place) — campaign behavior: new-game spawn, game-loaded spawn of missing,
  SyncData id→hero mapping, no duplicates, wanderer-style placement; uses 25 + 26.
- [x] 28-game-harmony-count (self) — Game/HarmonyPatches.cs (two prefixes, try/catch → vanilla on failure) — patch per GAME_MAP Q10 excluding registry ids; plus prefix on
  `LordConversationsCampaignBehavior.conversation_hero_hire_on_condition` returning false for registry heroes (GAME_MAP Q7).
- [x] 29-game-dialogs-hire (worker, review self; task ready, skeleton class in place) — generic dialog registration from dialogs.json (`{=<line id>}<en>`),
  intro/backstory/hire offer/accept/decline, vanilla join-clan, limit + hireCost.
- [x] 30-game-submodule (self) — Game/SubModule.cs + ModRuntime.cs; GameAssemblyTests check the class/patch types via metadata — SubModule class wiring: load registry, log validation, register behavior, apply Harmony; all entry points wrapped.
- [x] Milestone end: build + tests, dist layout check, every API traced to GAME_MAP; commit `phase 3: game layer`.

### M4 — Report
- [x] 30-run-report (self) — `docs/RUN_REPORT.md` per SPEC §6 (completed work, UNCERTAINs, BLOCKERS, morning checklist).
- [x] Final: full tests, "Report" section below, final commit. No push.

### Report
- Built: GAME_MAP Q1–Q16 verified + API index; Core (models, loader, catalog, validation, resolver, NPCCharacters and
  voiced-lines XML builders, logging); Game layer (SubModule.xml, runtime templates, spawn/persist/re-link behavior,
  generic dialogs + hire, Harmony guards, voice-over postfix); placeholder `test_companion`.
- Tests: Core.Tests 140/140, Acceptance 242/242.
- Out of plan (after the in-game test, self): JSON name fix (7981805), voice-over + Rhubarb lipsync without a build step
  (aa503f1), town names in the log (394f143).
- Verified in game by the user: load, tavern spawn, dialogs, hire, JSON name, voice-over, lipsync.
- Not verified: dismissed companion roaming, vanilla wanderer count, no-duplicate save→load, existing save without mod.
- Open questions: nord culture fallback semantics; GOG/Epic writability of the module folder. Details: docs/RUN_REPORT.md.
