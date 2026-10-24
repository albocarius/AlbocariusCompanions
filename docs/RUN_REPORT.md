# Run report — AlbocariusCompanions foundation

Date: 2026-10-24. Branch `orchestra/foundation`. Target: Bannerlord 1.4.8, Bannerlord.Harmony 2.4.2.
Build + tests: `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` →
Core.Tests 140/140, Acceptance 242/242 (Microsoft .NET SDK 10.0.402).

The run goal was a foundation with one placeholder companion that can be spawned, found in a tavern and hired via dialog.
It was played on the user's Windows install the same evening; see "In-game results".

## Completed per phase

### Phase 1 — Game map (`docs/GAME_MAP.md`)
- Q1–Q16 answered by the local worker (one task per question, decomp path + symbol for every claim) and verified by the
  orchestrator against the decompiled source; wrong claims fixed (Q1 kill condition/set fill, Q4 trait definitions).
- `## API index`: every TaleWorlds symbol used by `src/AlbocariusCompanions.Game` with its decomp file and the Q it is
  verified in. `ApiIndexTests` enforces SPEC rule 1.
- Later orchestrator notes from in-game findings: Q2 (hero naming), Q12 note 2 (voice pipeline timing and path matching).

### Phase 2 — Core (`src/AlbocariusCompanions.Core`, netstandard2.0, no TaleWorlds)
- JSON models, strict loader, catalog (a broken folder is skipped, the others load), registry, duplicate ids.
- Validation (errors/warnings, never throws): companion fields and ranges, voice types, equipment slots and fixed items,
  likes/dislikes/relations shape (incl. no self-relation), dialog slots, line ids `<id>_<slot>_<nn>`, `en` required,
  no player-name tokens in voiced lines, safe relative audio paths.
- Missing-audio report; resolver for SPEC rule 9 (culture/equipment fallbacks behind `IGameObjectLookup`).
- Runtime NPCCharacters XML builder (template `occupation="Special"`, validated against `xmlschemas/NPCCharacters.xsd`
  in tests); voiced-lines XML builder; logger (`Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\`) and
  `Safe.Run` wrappers.

### Phase 3 — Game layer (`src/AlbocariusCompanions.Game`, net472)
- `SubModule.xml` (deps Bannerlord.Harmony, Native, SandBoxCore, Sandbox).
- `SubModule`: loads companions, writes the voiced-lines file, applies Harmony, registers the behavior, registers
  templates from JSON at runtime (`MBObjectManager.LoadXml`) with a debug copy on disk.
- Hero creation from the template (age honoured, JSON skills/attributes pinned, Wanderer occupation, JSON name).
- Campaign behavior: spawn on new game and on load of an existing save, companion id → hero map saved with the campaign,
  re-link by template id, never duplicates; town of the companion's culture preferred.
- Generic dialogs from `dialogs.json` (intro, backstory, hire offer/accept/decline) and the vanilla hire flow
  (gold > cost, companion limit, pay → clan → party).
- Harmony: mod companions kept out of the vanilla wanderer count; vanilla hire option hidden for them; voice-over
  postfix that plays `audio/<line id>.ogg` from the companion folder.

### Added after the in-game test (not in the original plan)
- Name fix: Special templates got a random culture name ("Orachos"); the JSON name is now set after creation and
  re-applied on every load.
- Voice-over without a build step: static `language_data.xml`; the mod rewrites
  `ModuleData/Languages/VoicedLines/albocarius_voiced_lines_en.xml` in `OnSubModuleLoad` (the game registers the path
  before submodules load and parses it after). Adding a companion = dropping a folder with JSON + `audio/*.ogg`
  (+ optional Rhubarb `.xml` of the same name).
- Log shows town names next to settlement ids.

## In-game results (user, Windows, 2026-10-24)
| Check | Result |
|---|---|
| Mod loads with Harmony | OK |
| Companion spawned in a tavern (Myzea, later Jalmarys in another run) | OK |
| Intro / backstory / hire dialogs | OK |
| Hire: gold deducted, joins clan and party, fixed equipment | OK |
| Name from JSON; changing it in companion.json renames the hero after load | OK |
| Voice-over from `audio/<line id>.ogg` | OK |
| Lipsync from Rhubarb `.xml` | OK |

## Not verified / known gaps
- Dismissed companion: should keep roaming taverns like a vanilla wanderer (same base) — untested.
- Vanilla wanderer count unchanged with the mod — not checked in game.
- Companion death: the hero stays dead and is never respawned (by design for now; the user has a later plan).
- Removing the mod from a running campaign: not supported or tested (a mod that adds characters is not expected to be
  removed mid-campaign).
- The unhired companion likely stays in its spawn town (it is kept out of the vanilla wanderer swap). Acceptable.
- In JSON but without effect yet: `likes`, `dislikes`, `relations`, optional slots (`leave_threat`, `relation_complaint`,
  `like_reaction`, `dislike_reaction`, `idle`); only the first line per slot is used.
- Translations: `tr` texts in dialogs.json are validated but not shown (no language XML is generated yet); player prompts
  ("Tell me about yourself." …) are fixed English strings in code.
- A non-`.ogg` `audio` path is skipped silently; validation should report it.
- `nord` culture fallback semantics (DLC-present check vs. id existence) still open; currently id existence.
- `hireCost: 0` cannot be hired with 0 gold (vanilla strict `>` check).

## GAME_MAP UNCERTAINs
Resolved by the in-game test: template registration at load (Q5/Q11), hero creation and tavern placement (Q3/Q4),
Harmony prefixes intercept on the shipped runtime (Q7/Q10), hire flow and conversation option listing (Q6/Q7),
save/load round-trip of the companion map (Q8/Q9), module/DLL layout and Harmony id (Q16), writable module folder on the
user's Steam install (Q15), missing-language-file tolerance and voiced-lines format (Q12), lipsync files (Q12).

Still open (none block the foundation):
- Q1/Q3: whether other vanilla paths create heroes from wanderer templates; tavern spawn-point capacity; siege/sack effects.
- Q4: valid hair/beard/tattoo index ranges (face keys are passed through, not generated).
- Q6: starting clan tier at a new game (companion limit = tier + 3).
- Q8/Q9: free SaveableTypeDefiner id ranges (not needed: only a registered container type is saved); game behaviour when
  the save's module set differs from the active one.
- Q12: FMOD format requirements beyond "OGG works" (sample rate/channels).
- Q13: NavalDLC XML content (not in the local copy).
- Q15: install folder writability on GOG/Epic/Game Pass (Steam verified); native handling of an uncaught exception.
- Q16: launcher ordering details for `DependedModules` metadata.

## BLOCKERS
None open. `docs/BLOCKERS.md` has one resolved item (distro dotnet 10.0.113 crashes → Microsoft SDK 10.0.402).

## Morning test checklist
Install: copy `dist/AlbocariusCompanions/` to `<game>\Modules\`, enable **Bannerlord.Harmony (≥ 2.4.2)** above
**Albocarius's Companions** in the launcher. Mod log: `Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\AlbocariusCompanions.log`.
1. [x] Mod appears in the launcher and loads without errors.
2. [x] Log shows the companion loaded and validated (`Companion loaded: test_companion`, `1 companion(s) ready`).
3. [x] New campaign: companion exists and appears in a tavern (log: `Companion spawned: ... in <town>`).
4. [x] Hire dialog works, cost is deducted, companion joins the clan.
5. [ ] Vanilla wanderer count looks unchanged (Encyclopedia → Heroes → wanderers, compare with a run without the mod).
6. [ ] Save → load: no duplicate companion (log: `already present`, `0 spawned`).
7. [ ] Existing save made without the mod + mod enabled: companion appears (log: `spawned` with reason `game load`).
8. [x] Voice-over and lipsync play for voiced lines.
9. [ ] Dismiss the hired companion: it should return to tavern roaming.
