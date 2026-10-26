# Run report — milestone M7 "behavior foundation"

Date: 2026-10-26. Branch `orchestra/foundation`. Source of truth: `orchestra/SPEC_BEHAVIOR_FOUNDATION.md`, with the plan-gate
changes recorded in `orchestra/PLAN.md` (M7, decisions D1–D8; D2 as changed by the user). Build + tests:
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` → **Core.Tests 264/264,
Acceptance 620/620**, 0 build errors. Nothing was tested in game (no Windows machine in this run).

## Track R — research (GAME_MAP "Behavior system", all VERIFIED by spot checks in decomp)

| R | GAME_MAP | Answer in one line |
|---|---|---|
| R1 relations | Q21 | One symmetric `int` per pair of *any* two heroes (`CharacterRelationManager`, saved with the campaign). `Hero.GetRelation` reads an *effective* value (clan leaders substituted, personality ±), so a companion↔lord relation is really the player's; companion↔companion keeps its own row. Write: `ChangeRelationAction.ApplyRelationChangeBetweenHeroes` (events, notifications) or `Hero.SetPersonalRelation` (silent). |
| R2 morale | Q22 | Party-level only, no per-hero mood. `MobileParty.Morale` = 50 + recent events + leadership + food variety − starvation/wages/over-size, clamped 0..100. Mods add to `RecentEventsMorale` (decays 10 %/day, shown as "Recent Events") or override `PartyMoraleModel` for a labelled permanent factor. |
| R3 events | Q23 + Q32 | Table of `CampaignEvents` members with args, player check and timing. No dedicated events for battle won/lost (use `OnPlayerBattleEndEvent` + `MapEventEnded`), retreat (`PlayerDesertedBattleEvent` only for "leave soldiers behind"), unpaid wages or morale change (poll daily ticks). Listeners run in **reverse** registration order. |
| R4 traits | Q24 | One funnel for the player: `TraitLevelingHelper.AddPlayerTraitXPAndLogEntry` → `PlayerTraitChangedEvent(trait, previousLevel)` on a level change; read with `Hero.GetTraitLevel`. Action → trait table included. |
| R5 companion talks first | Q25 | Yes: `CampaignMapConversation.OpenConversation(player, partner)` while `GameStateManager.Current.ActiveState is MapState` (vanilla grievances also skip at sea, during a map event or an encounter). Time is not paused by the call. |
| R6 pop-ups | Q26 | `InformationManager.ShowInquiry(InquiryData, pauseGameActiveState, prioritize)` (max two buttons, callbacks) and `MBInformationManager.ShowMultiSelectionInquiry` for N options. |
| R7 time | Q27 | `CampaignTime.Now`, `ElapsedDaysUntilNow`, `CampaignTime.Days(n)`; `DailyTickEvent`/`HourlyTickEvent`; `CampaignTime` is saveable directly in `SyncData` (vanilla does it). |
| R8 location | Q28 | `MobileParty.MainParty.CurrentSettlement` (null outside), `BesiegerCamp`, `IsTown`/`IsCastle`/`IsVillage`, `Settlement.Culture.StringId`, `SettlementHelper.FindNearest*`, map distance model, `Village.VillageType.PrimaryProduction`. |
| R9 items/gold | Q29 | Items by id via `MBObjectManager.GetObject<ItemObject>` (null if unknown); inventory = party `ItemRoster` (companions have none of their own); gold via `GiveGoldAction` (capped at the giver, null side creates/destroys). |
| R10 console | Q30 | `[CommandLineFunctionality.CommandLineArgumentFunction("name", "group")]` on a `static string M(List<string>)`, found by the game's assembly scan; first registration wins; args split on spaces. |
| R11 misc | Q31 | `Clan.PlayerClan.Tier`; `FactionManager.IsAtWarAgainstFaction` / `IFaction.IsAtWarWith` (a clan without a kingdom is its own map faction); `hero.PartyBelongedTo == MobileParty.MainParty` vs `IsPlayerCompanion` (clan). |

**Finding that matters today (not new code):** a conversation `relation` effect that drops a *hired* companion's
relation below −10 makes vanilla remove that companion (`CompanionRolesCampaignBehavior.OnHeroRelationChanged` →
`KillCharacterAction.ApplyByRemove`, Q21/Q18). Authors should keep negative relation effects small until leaving is
designed.

### UNCERTAIN (each needs one in-game check; details in GAME_MAP)
- Q30: which key opens the console in a retail build, whether it needs `cheat_mode`/`development_mode` in
  `engine_config.txt`, and whether mod commands are found by the first scan. **Affects checklist item 3.**
- Q25: clock and safety of opening a map conversation during a map event; a second conversation while one is open.
- Q23/Q32: naval paths, `HeroWounded` for the player hero, `MapEventEnded` per siege round, daily-tick listener order.
- Q29: `GiveItemAction` hero↔hero double subtraction (looks like a vanilla bug — the mod does not use it), whether
  `Debug.FailedAssert` is visible in retail (the mod clamps item removal anyway).
- Q21/Q22/Q24: DLC listeners/triggers not audited (DLC sources excluded by rule); Harmony on the morale setter.
- Q26/Q27/Q28/Q31: queued pop-ups across save/load, frame `dt` caps, village types as XML, map-distance units, stale
  stance links, companions in caravans/garrisons.

## Track I — implementation

| Task | Owner | Done |
|---|---|---|
| I1+I2 variables and conditions (69) | self | `variables` in dialogs.json (bool/int, local/shared, defaults, built-in `mood`), `set`/`add` effects (D1), `flags`/`notFlags`/`setFlags`/`clearFlags` as sugar on declared bools, `all`/`any`/`not` and `var` + `min`/`max`/`eq` (D3). Validation per companion + catalog-level cross-companion pass with the D2 rule: loaded companion with an undeclared / local / wrongly typed variable → error, companion skipped; companion **not loaded** (missing, skipped for errors, required module missing) → warning, the condition is false as a whole, a write is skipped (tested: Ahmet loads without Jeremus and the conversation is not offered). `requiredModules` filtering moved into `CompanionCatalog` for this. |
| I1 save (70) | self | `VariableStore`; saved under `_variables` as `owner.name=value` for written values only; unknown names ignored, wrong type → default, new variables start at their default; M6 flags in old saves become the declared bool of the same name (D4). Renaming a variable loses its value (SCHEMA.md). |
| I3 transfers Core (71) | worker (+ review fix) | `transfer` effect (`gold` or `item`+`count`, `from`/`to` = player, self, world, companion id), `hasGold`/`hasItem`; items only player↔world (Q29); companion actors that are not loaded → warning. Implied check kept (D5): a node whose source cannot pay is not offered. |
| I3 transfers game (72) | self | Gold via `GiveGoldAction.ApplyBetweenCharacters` (null = world), items via the player party `ItemRoster` (removal clamped, unknown item id skipped + warning). |
| I3 `gold` key removed (D6) | self | after the sample moved to `transfer`; a leftover `gold` key is now a load error. |
| I4 voiced nodes (76) | self | already existed since M6 (D7); added the missing-audio test for nodes. |
| I5 graph (73) | worker | nodes that can never reach `hub`/`end` → one error per conversation (dangling `next` and unreachable nodes existed since M6). |
| I5 authoring warnings (74) | worker | "variable never changes" (never written by any loaded companion and starts at the type default); transfer from ≠ world without a matching `hasGold`/`minGold`/`hasItem` on the node, the conversation or an earlier node of the path. |
| I5 repeatable reward (75) | self | non-`once` conversation path that rewards the player (relation up, gold/items to the player) without a guard the same path makes false → warning. The "shadowing" rule was not added (selection rules). |
| I6 Mermaid (77) | worker | `MermaidExport` in Core (player/npc shapes, conditions on edges, effects in labels). |
| I6 CLI (78) | worker (+ review fix) | `tools/AlbocariusCompanions.Cli` (net8.0, RollForward Major): `validate <companion folder or Companions dir>`, `mermaid <companion folder> [conversation]`; exit codes 0/1/2. Review fix: the CLI ships its own Newtonsoft 13.0.1 (Core keeps it private), found by the worker. |
| I7 console (79) | self | `albocarius.list`, `albocarius.get <id>.<var>`, `albocarius.set <id>.<var> <value>`, `albocarius.reset <id>`; text/parsing in Core (unit-tested), each wrapped in `Safe.Run`, needs a running campaign. **No `albocarius.reload`**: dialogs are registered once per campaign start, swapping the JSON underneath is not safe. |
| I8 sample (80) | self | `spare_coin` fixed: both paths set `got_coin`, the payment has `hasGold` and gold moves via `transfer`; variables declared; Turkish lang file completed (it was partial on purpose in M5, but the spec requires zero warnings); `validate module/ModuleData/Companions` → 0 errors, 0 warnings (acceptance test). The scaffold template has no conversations, so it needed no change. |

Other: Mermaid ids are `<conversation>__<node>`, so a node literally named `entry` would collide with the entry box
(cosmetic, noted). Ideas: none new for `docs/IDEAS.md`.

## Decisions to confirm
- D1–D8 as approved at the plan gate; D2 implemented exactly as you changed it.
- `gold` effect removed (spec I3) — authored content must use `transfer`. Old saves are unaffected (effects are not saved).
- Old `gold: -n` paid the companion; spec says gold = the player's gold change, so the short-lived sugar went to `world`.
  The sample now pays the companion explicitly (`from: player, to: self`).
- Scope of a variable defaults to `local` when `scope` is omitted.

## BLOCKERS
None open.

## Test checklist (human)
Before testing: build, copy `dist/AlbocariusCompanions/` to `<game>\Modules\` (keep your `audio/`). Your own companion
folders must declare every flag they use under `"variables"` and replace `"gold"` effects with `"transfer"` (run
`dotnet run --project tools/AlbocariusCompanions.Cli -- validate <your Companions folder>` first).
1. [ ] Existing companions still load, talk, hire, voice + lipsync (no regression); no red/yellow message at start.
2. [ ] test_companion: hire, ask about the scar ("That must have been hard…" → relation +2, `told_scar`), then
   `spare_coin`: pay 100 (needs 100 gold; the companion's gold goes up) **or** ask at relation ≥ 21 for 50 — either way
   the offer is gone afterwards and never comes back.
3. [ ] Console (if it opens, Q30): `albocarius.list`, `albocarius.get test_companion.got_coin`,
   `albocarius.set test_companion.got_coin false` → the coin offer is back; `albocarius.reset test_companion`.
4. [ ] CLI: `validate module/ModuleData/Companions` → `0 error(s), 0 warning(s).`; `mermaid
   module/ModuleData/Companions/test_companion` output renders (e.g. mermaid.live).
5. [ ] Save → load keeps variable values (e.g. after `spare_coin` the offer stays gone; log line "Conversation variables
   loaded: N"). Loading an M6 save converts its flags ("imported from old flags").
