# Run report — milestone M6 "per-companion player lines + conversations"

Date: 2026-10-25. Branch `orchestra/foundation`. No spec file: user request of 2026-10-25 and the answers recorded in
`orchestra/PLAN.md` (M6). Build + tests: `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
→ Core.Tests 215/215, Acceptance 359/359. Not tested in game yet.

## Per task

| Task | Done | Notes |
|---|---|---|
| 54 player lines per companion | player slots `player_ask_about`, `player_ask_hire`, `player_hire_pay`, `player_hire_full`, `player_hire_later` are required slots of each companion's dialogs.json; texts in its own lang files; never voiced (`voiced_player_line` error); `_shared` removed from the repo; a leftover `_shared` folder is skipped with a warning (`obsolete_shared_folder`); template + test_companion + fixtures migrated | the scaffold template has the 5 player lines pre-filled |
| 55 GAME_MAP Q20 | entry tokens and priorities (menu option 105 on `hero_main_options`, greeting 119 on `start`), return tokens (`lord_pretalk` after a player line, `hero_main_options` after an NPC line, `close_window`), relation/gold/join actions, `List<string>` save container; API rows | self-verified in decomp |
| 56 conversations | `conversations` in dialogs.json (SCHEMA.md "Conversations"): entry `hub`/`greeting`, `once`, `when`, `start`, nodes with speaker, `next`, `when`, `effects`, voice. Core: models, `ConversationChecks` (structure, links, reachability, stall warning, condition/effect ranges), `ConversationGraph` (graph → game sentences), `ConversationConditions` (pure, tested with a fake context), `ConversationFlags` (saved keys). Game: registration in `CompanionDialogs`, effects, flags in the behavior's `SyncData`. test_companion has three example conversations | voiced nodes get voice-over entries like lines |
| 57 report | this file | |

## Decisions to confirm
- **Player lines are copied into every companion.** No shared default any more (as you chose): a companion without its 5
  player lines fails validation. The template and test_companion carry the old texts.
- **Greeting conversations need a met companion** and use priority 119: above vanilla greetings, below the intro (which
  needs a companion not met yet) and below story-quest lines.
- **Menu options of conversations** are listed above vanilla options (priority 105) and below quests.
- **"once" counts from the start**: the conversation is used up when its first line is said, even if the player leaves early.
- **Flags are per companion** (`told_scar` of one companion is unrelated to another's) and per campaign.
- **Relation changes** go through the game's own relation action: positive changes are scaled by the game's model
  (e.g. Charm) and rounded randomly, so +2 may show as +1..+3.

## UNCERTAIN
- Everything in-game: conversations, flags across save/load, greeting priority, effects, notifications of relation/gold.
- Q20: greeting priority against other mods' lines on `start` between 111 and 125.

## BLOCKERS
None.

## Test checklist (human)
Before testing: copy `dist/AlbocariusCompanions/` to `<game>\Modules\` (keep your `audio/`), delete the old
`ModuleData\Companions\_shared` folder (otherwise a yellow warning says it is no longer read), and migrate
`albocompanions_jeremos` to the new format (schemaVersion, no id/cultureFallback/fallbackTemplateId, texts in lang/en.json,
5 player lines) — see test_companion. Audio file renames from the previous milestone still apply.
1. [ ] Unhired test_companion: the menu shows the mod's ask-about / hire options with the texts from test_companion's
   lang files (Turkish under Türkçe); no conversation option yet (all three need a hired companion).
2. [ ] Hire it. The menu shows "That scar on your arm…" (the_scar). Choose it → story → two answers.
3. [ ] "That must have been hard to carry." → relation +2 notification, "It is. Thank you…", back to "Is there anything
   else?" and the menu. The scar option is gone (once). Log: `Conversation effects of 'test_companion' node 'p_scar_sorry'`.
4. [ ] Now "Here, take a hundred denars…" (spare_coin) shows only with ≥ 100 gold; picking it pays 100, ends the
   conversation, and the option never returns (flag `got_coin`).
5. [ ] With relation ≥ 10 "Could you spare some coin…" appears: at relation ≤ 20 the companion refuses (back to the menu),
   above 20 it gives 50 gold and the conversation ends.
6. [ ] Talk to it again while it is in your party: it opens with "Captain. I keep thinking about our talk…" (after_scar
   greeting, once); "I'm glad you told me." → menu, "Not now, I'm busy." → conversation closes. Next talk: normal greeting.
7. [ ] Save after step 3, reload: the scar option stays gone, spare_coin is still offered (log `Conversation flags loaded: N`).
8. [ ] Start a new campaign: flags are empty again (scar option back after hiring).
9. [ ] Previous milestone checklist (`docs/RUN_REPORT_CORE_GAPS.md`) items 1–10 still pass.
