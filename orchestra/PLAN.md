# PLAN — AlbocariusCompanions foundation run

Source of truth: `orchestra/SPEC.md`. Milestones M1–M4 = SPEC phases 1–3 + end-of-run report.
Commit policy: per-task commits as in ORCHESTRATOR.md; at the end of every SPEC phase an extra
commit `phase N: <summary>` (SPEC rule 3). Stop condition (SPEC rule 5) applies to every task:
3 failed fixes of the same error → `docs/BLOCKERS.md`, commit, move on.

Owner legend: `self` = orchestrator, `worker` = local model via orchestra/worker.sh.
Acceptance tests for worker tasks live in `tests/AlbocariusCompanions.Acceptance/` (orchestrator-owned).
Every worker task file (`orchestra/tasks/NN-slug.md`) starts with the full text of `orchestra/HARD_RULES.md`
(also appended to WORKER.md) — user requirement.

## Status
- Phase 0 bootstrap done (commit 8aa1f47). Baseline: build OK, 0 tests.
- Plan approved with changes (2026-10-24). Working on M1, task 01.

## Decisions already fixed
- JSON: `Newtonsoft.Json.dll` 13.0.1 exists in `~/bannerlord/dlls/bin` → Game references that file
  (Private=false); Core/tests use NuGet `Newtonsoft.Json` 13.0.1.
- Harmony: NuGet `Lib.Harmony` compile-only (ExcludeAssets=runtime); runtime dependency on
  `Bannerlord.Harmony` module in SubModule.xml. No Harmony DLL in `~/bannerlord/dlls`.
- Decompiled sources lack TaleWorlds.Library / Localization / Engine / ModuleManager; when needed,
  decompile with `ilspycmd` into the session scratchpad (outside the repo, never committed).

## M1 — Game map (SPEC Phase 1) → `docs/GAME_MAP.md`
Discovery = worker (one task per question), verification = self (user decision 2026-10-24).
Task files: `orchestra/tasks/NN-gm-qNN-*.md`. Worker edits only its `## QN` section, Status DRAFT.
Acceptance: `GameMapTests` (format, cited paths exist, cited symbol occurs in cited file, no long code blocks);
the `Questions` list is extended by one entry per assigned task.
Verification (self): open every cited location, fix wrong claims, set `Status: VERIFIED`, commit `docs(gm-qNN): verified`.
Missing DLLs (Library, Localization, ModuleManager, Engine, SaveSystem) decompiled into `~/bannerlord/decomp` (= `decomp/`).
- [x] 01-gm-q01-wanderer-spawn (worker → verify)
- [ ] 02-gm-q02-wanderer-count (worker → verify)
- [ ] 03-gm-q03-tavern-placement (worker → verify)
- [ ] 04-gm-q04-hero-creation (worker → verify) — also value ranges for validation
- [ ] 05-gm-q05-char-templates (worker → verify) — also culture/equipment ids for placeholder
- [ ] 06-gm-q06-hiring (worker → verify)
- [ ] 15-gm-q07-dialog-api (worker → verify)
- [ ] 16-gm-q08-lifecycle (worker → verify)
- [ ] 17-gm-q09-existing-saves (worker → verify)
- [ ] 18-gm-q10-harmony-target (worker → verify)
- [ ] 19-gm-q11-generation-decision (worker → verify)
- [ ] 20-gm-q12-voice-over (worker → verify) — also vanilla voice type ids
- [ ] 21-gm-q13-war-sails (worker → verify)
- [ ] 22-gm-q14-localization (worker → verify)
- [ ] Milestone end: all sections VERIFIED or UNCERTAIN with resolution; commit `phase 1: game map`.

## M2 — Core (SPEC Phase 2)
- [ ] 15-core-models (self) — models for §4 schema, `ValidationIssue` (severity, code, companionId, message),
  Slot definitions (§4.3), line-id rule helper, Newtonsoft package. Design work → self.
  Edit: `src/AlbocariusCompanions.Core/**`, Core csproj. Accept: build + a few unit tests.
- [ ] 16-core-loader (worker) — scan `<root>/*/`, load companion.json + dialogs.json, collect errors,
  skip broken folder, continue. Accept: valid+broken folder, missing file, malformed JSON.
- [ ] 17-core-registry (worker) — read-only lookup by id, `AllIds` set. Accept: lookup, unknown id, ids set.
- [ ] 18-core-validation-companion (worker) — companion.json rules (id format/uniqueness/folder name,
  required fields, ranges per GAME_MAP, cultureFallback≠culture, fallbackTemplateId≠templateId,
  voice ∈ vanilla voice types, likes/dislikes/relations shape). Accept: one failing test per rule + valid companion.
- [ ] 19-core-validation-dialogs (worker) — line-id format + global uniqueness, required slots,
  unknown slot, `text` language keys + `en` required, no player-name tokens in voiced lines (all languages),
  `audio` relative & no `..`. Accept: one failing test per rule.
- [ ] 20-core-missing-audio (worker) — report voiced lines whose audio file is missing (default path
  `audio/<line id>.ogg` when `audio` absent). Accept: present/missing/default path cases.
- [ ] 21-placeholder-companion (self) — `module/ModuleData/Companions/test_companion/` per SPEC §5 with
  ids from GAME_MAP; acceptance test that loads it from disk and validates clean.
- [ ] Milestone end: full tests; commit `phase 2: core`.

## M3 — Game layer (SPEC Phase 3)
Untestable without the game. Rule: every API call traces to a VERIFIED GAME_MAP entry; self reviews worker diffs against GAME_MAP.
- [ ] 22-game-build (self) — Game csproj: HintPath refs into `~/bannerlord/dlls` (Private=false),
  Newtonsoft from game bin (Private=false), Lib.Harmony compile-only; post-build target assembling
  `dist/AlbocariusCompanions/` (SubModule.xml, bin/Win64_Shipping_Client/{Game,Core}.dll, ModuleData/Companions/**).
  **Core's Newtonsoft copy must not go to dist** (user decision). Accept: acceptance test on dist layout incl. no Newtonsoft.Json.dll in dist.
- [ ] 23-game-submodule-xml (worker) — `module/SubModule.xml` (id, name, version 0.1.0, deps from GAME_MAP,
  Bannerlord.Harmony, SubModule class). Accept: XML parse test checking ids/version/deps.
- [ ] 24-game-logging (worker) — file logger (path per GAME_MAP), safe-call wrapper that never rethrows;
  TaleWorlds-free logic kept in Core so it is testable.
- [ ] 25-game-resolver (worker, review self) — single resolver for skills/cultures/traits/equipment/settlements by string id, fallbacks, warnings.
- [ ] 26-game-hero-creation (self) — create hero from registry data per GAME_MAP Q4/Q5/Q11 (voice type from JSON).
- [ ] 27-game-behavior-spawn (worker, review self) — campaign behavior: new-game spawn, game-loaded spawn of missing,
  SyncData id→hero mapping, no duplicates, wanderer-style placement; uses 25 + 26.
- [ ] 28-game-harmony-count (self) — patch per GAME_MAP Q10 excluding registry ids.
- [ ] 29-game-dialogs-hire (worker, review self) — generic dialog registration from dialogs.json (`{=<line id>}<en>`),
  intro/backstory/hire offer/accept/decline, vanilla join-clan, limit + hireCost.
- [ ] 30-game-submodule (self) — SubModule class wiring: load registry, log validation, register behavior, apply Harmony; all entry points wrapped.
- [ ] Milestone end: build + tests, dist layout check, every API traced to GAME_MAP; commit `phase 3: game layer`.

## M4 — Report
- [ ] 30-run-report (self) — `docs/RUN_REPORT.md` per SPEC §6 (completed work, UNCERTAINs, BLOCKERS, morning checklist).
- [ ] Final: full tests, "Report" section below, final commit. No push.

## Report
(filled at the end)
