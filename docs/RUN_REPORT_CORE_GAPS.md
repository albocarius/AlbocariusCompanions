# Run report — AlbocariusCompanions milestone "core gaps"

Date: 2026-10-24. Branch `orchestra/foundation`. Spec: `orchestra/SPEC_CORE_GAPS.md` (+ the user's T6 decisions in
`orchestra/PLAN.md`). Build + tests: `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
→ Core.Tests 201/201, Acceptance 319/319. Nothing was deployed to Windows (laptop closed); `dist/` is up to date.

## Per task

| Task | Done | Notes |
|---|---|---|
| T1 close previous round | RUN_REPORT known-gaps table, WORKER_LOG rows for the out-of-plan work, stale PLAN boxes fixed, `docs/SCHEMA.md` + `docs/IDEAS.md` created | |
| T2.1 OGG validation | `invalid_audio_extension` error for any non-`.ogg` audio (case-insensitive) | worker (32) |
| T2.2 hire cost | `gold >= hireCost`; `hireCost: 0` hireable with 0 gold | |
| T2.3 requiredModules | `cultureFallback` / `equipment.fallbackTemplateId` removed everywhere; optional `requiredModules`; at load a companion with a module that is not active is skipped with `Companion 'x' skipped: required module(s) not loaded: …`; a saved hero of a companion that is not loaded is left untouched with one warning | Core worker (35), game self; detection = `ModuleHelper.IsModuleActive` (GAME_MAP Q13), no reference to other assemblies |
| T2.4 schemaVersion | required `1` in companion.json, dialogs.json, player_lines.json and every lang file; one check point `SchemaVersions.Check`; missing / not integer / other value → loader error, folder skipped | worker (32); a missing version is reported after a JSON error so a broken file still shows its real error first |
| T2.5 in-game notifications | warnings yellow, errors red via `InformationManager.DisplayMessage` (GAME_MAP Q17); queued until the first campaign start/load, then shown as they come; each text once per session; one summary line `N companions loaded, M skipped — see log`; info is file-only | the only listener (chat log) exists from the main menu on, but messages wait for a campaign as specified; missing translations are expected, so they are file-only and counted per language |
| T3.1 folder = id | `id` removed from companion.json; folder name must match `[a-z0-9_]+`; `_shared` reserved | |
| T3.2 short line ids | local ids in dialogs.json (`[a-z0-9_]+`, unique per companion); full id `<companion>_<local>` for `{=id}`, voice-over and logs, unique across companions and player lines; default audio `audio/<local id>.ogg` | **the user's audio files on Windows must be renamed** (`test_companion_intro_01.ogg` → `intro_01.ogg`, same for the Rhubarb `.xml`) |
| T3.3 scaffold | `tools/new-companion.sh <id> [root]` and `tools\new-companion.bat <id> [root]` copy `tools/companion-template/`; refuse an existing folder, an invalid id and `_shared`; the template passes validation (test) | the `.bat` is not run by the tests (Linux CI); `findstr` ranges avoided because `[a-z]` matches uppercase there |
| T4.0 vanilla dialog points | GAME_MAP Q18 (worker, verified): every wanderer/companion dialog point in 8 tables; 46 NPC-side points are optional companion slots (`VanillaDialogPoints`); a postfix on `ConversationManager.UpdateCurrentSentenceText` shows (and voices) the companion's line instead of the vanilla text when the slot has lines; the template has one line per slot with the vanilla English text, our ids | see "Deviations" for player-side points |
| T4 dismissal | GAME_MAP Q19 (self, verified): vanilla dismissal = `RemoveCompanionAction.ApplyByFire` **then `KillCharacterAction.ApplyByRemove`** — that removal is why the companion vanished. A prefix on `CompanionRolesCampaignBehavior.companion_fire_on_consequence` (the only player-dismissal path; the clan screen has none) runs the fire step for mod companions, skips the removal, makes the hero active and places it in a tavern with the initial-spawn rule; logs both steps. Other removals are untouched. | the prefix never falls back to vanilla for a mod hero (that would delete it) |
| T5 slot variation | `LinePicker` + `IRandomSource` in Core (tested with a fake source); game uses `MBRandom`; every slot, ours and vanilla points, picks a random line each time it is shown | |
| T6 player lines + languages | text moved out of dialogs.json into `lang/<code>.json` (`{ schemaVersion, lines: { id: text } }`); `_shared/player_lines.json` + `_shared/lang/`; no dialog text in C# (test); dialog lines registered once as `{=!}{VAR}`, the condition picks the text in the active language with en fallback (GAME_MAP Q14 note 2, vanilla `VOICED_LINE` pattern) — no build step, no language XML; only the voiced-lines XML is still written at start-up | language match: ISO code → game language via `LocalizedTextManager.GetLocalizationCodeOfISOLanguageCode` (cached); a language change in the options applies to the next line shown |
| T7 report | this file | |

## Deviations and decisions to confirm
- **Player-side vanilla points are not slots.** Q18 lists them, but player text lives in `_shared` (global, not per
  companion) and per-companion player overrides are out of scope (T6.3); replacing a vanilla player option would change
  it for every mod companion. Only the mod's own player lines (`ask_about`, `ask_hire`, `hire_pay`, `hire_full`,
  `hire_later`) are in JSON.
- **The template overrides every vanilla point.** By the user's decision only an absent/empty slot falls back, so a
  scaffolded companion that keeps the template text shows that English text instead of the vanilla line — also under
  a non-English game language, where vanilla would have been translated. Authors delete lines they do not want to own.
- **The 5 mod slots stay required** (`intro`, `backstory`, `hire_offer`, `hire_accept`, `hire_decline`): they drive the
  mod's own meeting/hire flow, which replaces the vanilla wanderer flow for mod companions.
- **Relation break-up** (`CompanionRelationLimit = -10`) removes a companion with the same `ApplyByRemove` as vanilla
  dismissal; it is not a player dismissal, so per T4.2 the mod does not intervene. A mod companion can therefore still be
  lost that way.
- **Caravan-leader talk** (Table 7, `CaravansCampaignBehavior`) has no slots; the NPC sentence ids were not extracted.
- 40, 41, 47, 49, 50 were done as one change (commit 9253894) because they rewrite the same fixtures.

## UNCERTAIN
- Everything in-game in this milestone is untested: notifications, Turkish texts, random pick, vanilla-point replacement,
  dismissal return, requiredModules skip. The APIs are verified in the decompiled source only.
- Q19: dismissing a captive mod companion (`EndCaptivityAction.ApplyByEscape` branch) not tested.
- Q18: `HeroHelper.UsesLordConversations` body is not in the decomp extract (affects when vanilla greetings fire).
- Q14: languages without an ISO 639-1 code (`zh-HANS`, `zh-HANT`) cannot get a lang file; they fall back to English.
- Q17: the chat log drops messages when the player turned the singleplayer chat box off; the log file still has them.
- Older items from the foundation report (Q1/Q3/Q4/Q6/Q8/Q9/Q12/Q13/Q15/Q16) are unchanged.

## BLOCKERS
None. One worker task (42, Q18) hit the 60-minute timeout after writing a complete section; it passed the check and was
verified.

## Test checklist (human)
Before testing: rebuild/copy `dist/AlbocariusCompanions/` to `<game>\Modules\` (replace the mod folder but keep your
`audio/`), and **rename the audio files** in `ModuleData\Companions\test_companion\audio\` to local ids:
`test_companion_intro_01.ogg` → `intro_01.ogg` (and the `.xml` lipsync files likewise). Mod log:
`Documents\Mount and Blade II Bannerlord\AlbocariusCompanions\AlbocariusCompanions.log`.
1. [ ] Mod loads; log shows `Companion loaded: test_companion`, `1 companion(s) ready`; in game a summary line
   `Albocarius's Companions: 1 companion loaded.` appears on campaign start/load.
2. [ ] Add `"requiredModules": ["EOE_Armory"]` to a companion (module not installed): log `Companion 'x' skipped: required
   module(s) not loaded: EOE_Armory`, in-game summary says `1 skipped — see log`.
3. [ ] Hire with exactly `hireCost` gold works (500 for test_companion).
4. [ ] Dismiss the companion ("About your position in the clan…" → "I no longer have need of your services." on the map):
   it says goodbye, log shows `dismissed by the player` and `returns to the tavern of <town>`; find it there and rehire.
5. [ ] Save after the dismissal, reload: no duplicate, no respawn (log `already present`, `0 spawned`).
6. [ ] Slot with several lines: add `intro_02`/`backstory_02` lines (+ en texts) and talk several times — different lines.
7. [ ] Switch the game language to Türkçe: `intro_01` and the player lines show in Turkish, untranslated lines in English.
8. [ ] Voiced lines still play with lipsync after the rename (no regression from the id change).
9. [ ] Vanilla point override: give test_companion a `farewell` line ("I'll be waiting, Captain.") — leaving the
   conversation shows it instead of "Until next time, then."; remove it — vanilla text is back.
10. [ ] An invalid file (e.g. `"schemaVersion": 2`) shows a red in-game message once and the log names the file.
