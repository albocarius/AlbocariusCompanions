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
- Waiting for plan approval (PLAN_GATE=1).

## Decisions already fixed
- JSON: `Newtonsoft.Json.dll` 13.0.1 exists in `~/bannerlord/dlls/bin` → Game references that file
  (Private=false); Core/tests use NuGet `Newtonsoft.Json` 13.0.1.
- Harmony: NuGet `Lib.Harmony` compile-only (ExcludeAssets=runtime); runtime dependency on
  `Bannerlord.Harmony` module in SubModule.xml. No Harmony DLL in `~/bannerlord/dlls`.
- Decompiled sources lack TaleWorlds.Library / Localization / Engine / ModuleManager; when needed,
  decompile with `ilspycmd` into the session scratchpad (outside the repo, never committed).

## M1 — Game map (SPEC Phase 1) → `docs/GAME_MAP.md`
Research only, correctness-critical (rule 1 "never invent API") → all `self`.
- [ ] 01-gamemap-wanderers — Q1, Q2, Q3, Q10: spawn behavior, count logic, placement/movement, Harmony target.
  Read: `decomp/TaleWorlds.CampaignSystem` (CompanionsCampaignBehavior & friends), `decomp/SandBox`.
- [ ] 02-gamemap-creation — Q4, Q5, Q11: HeroCreator / CharacterObject templates, wanderer XML in
  `~/bannerlord/moduledata`, runtime-without-XML feasibility, generation decision.
- [ ] 03-gamemap-dialog-hire — Q6, Q7: hire dialog flow, AddCompanionAction / clan join, cost, limit; DialogFlow / AddPlayerLine API.
- [ ] 04-gamemap-lifecycle — Q8, Q9: OnNewGameCreated / OnGameLoaded / DailyTick, SyncData, existing-save hook.
- [ ] 05-gamemap-voice-l10n — Q12, Q14: DefaultVoiceOverModel, voiced-lines XML, `{=ID}` resolution,
  language_data.xml, missing-translation behaviour, voice-type ids (needed for Core validation).
- [ ] 06-gamemap-dlc — Q13: NavalDLC module id, runtime detection by name, added skill/culture/trait ids,
  game-mode detection, `MBObjectManager.GetObject` null-vs-throw.
- [ ] Milestone end: every question answered or UNCERTAIN + "what would resolve it"; commit `phase 1: game map`.

## M2 — Core (SPEC Phase 2)
- [ ] 07-core-models (self) — models for §4 schema, `ValidationIssue` (severity, code, companionId, message),
  Slot definitions (§4.3), line-id rule helper, Newtonsoft package. Design work → self.
  Edit: `src/AlbocariusCompanions.Core/**`, Core csproj. Accept: build + a few unit tests.
- [ ] 08-core-loader (worker) — scan `<root>/*/`, load companion.json + dialogs.json, collect errors,
  skip broken folder, continue. Accept: valid+broken folder, missing file, malformed JSON.
- [ ] 09-core-registry (worker) — read-only lookup by id, `AllIds` set. Accept: lookup, unknown id, ids set.
- [ ] 10-core-validation-companion (worker) — companion.json rules (id format/uniqueness/folder name,
  required fields, ranges per GAME_MAP, cultureFallback≠culture, fallbackTemplateId≠templateId,
  voice ∈ vanilla voice types, likes/dislikes/relations shape). Accept: one failing test per rule + valid companion.
- [ ] 11-core-validation-dialogs (worker) — line-id format + global uniqueness, required slots,
  unknown slot, `text` language keys + `en` required, no player-name tokens in voiced lines (all languages),
  `audio` relative & no `..`. Accept: one failing test per rule.
- [ ] 12-core-missing-audio (worker) — report voiced lines whose audio file is missing (default path
  `audio/<line id>.ogg` when `audio` absent). Accept: present/missing/default path cases.
- [ ] 13-placeholder-companion (self) — `module/ModuleData/Companions/test_companion/` per SPEC §5 with
  ids from GAME_MAP; acceptance test that loads it from disk and validates clean.
- [ ] Milestone end: full tests; commit `phase 2: core`.

## M3 — Game layer (SPEC Phase 3)
Untestable without the game; every call must trace to GAME_MAP → mostly `self`.
- [ ] 14-game-build (self) — Game csproj: HintPath refs into `~/bannerlord/dlls` (Private=false),
  Newtonsoft from game bin, Lib.Harmony compile-only; post-build target assembling
  `dist/AlbocariusCompanions/` (SubModule.xml, bin/Win64_Shipping_Client/*.dll, ModuleData/Companions/**).
  Accept: acceptance test asserting dist layout after build.
- [ ] 15-game-submodule-xml (worker) — `module/SubModule.xml` (id, name, version 0.1.0, deps from GAME_MAP,
  Bannerlord.Harmony, SubModule class). Accept: XML parse test checking ids/version/deps.
- [ ] 16-game-logging (worker) — file logger (path per GAME_MAP), safe-call wrapper that never rethrows.
  Accept: build; logger logic kept in Core if TaleWorlds-free so it is testable.
- [ ] 17-game-resolver (self) — single resolver for skills/cultures/traits/equipment/settlements by string id, fallbacks, warnings.
- [ ] 18-game-behavior-spawn (self) — campaign behavior: new-game spawn, game-loaded spawn of missing,
  SyncData id→hero mapping, no duplicates, wanderer-style placement.
- [ ] 19-game-harmony-count (self) — patch per GAME_MAP Q10 excluding registry ids.
- [ ] 20-game-dialogs-hire (self) — generic dialog registration from dialogs.json (`{=<line id>}<en>`),
  intro/backstory/hire offer/accept/decline, vanilla join-clan, limit + hireCost.
- [ ] 21-game-submodule (self) — SubModule class wiring: load registry, log validation, register behavior, apply Harmony; all entry points wrapped.
- [ ] Milestone end: build + tests, dist layout check, every API traced to GAME_MAP; commit `phase 3: game layer`.

## M4 — Report
- [ ] 22-run-report (self) — `docs/RUN_REPORT.md` per SPEC §6 (completed work, UNCERTAINs, BLOCKERS, morning checklist).
- [ ] Final: full tests, "Report" section below, final commit. No push.

## Report
(filled at the end)
