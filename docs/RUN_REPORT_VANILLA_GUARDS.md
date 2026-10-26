# Run report — milestone M8 "vanilla guards"

Date: 2026-10-26. Branch `orchestra/foundation`. Source of truth: `orchestra/SPEC_VANILLA_GUARDS.md` plus the plan-gate
notes in `orchestra/PLAN.md` (M8: E1–E3, one raw read API, orchestrator-verified exclude rows). Version **0.2.1**
(SubModule.xml and the Game assembly). Build + tests → **Core.Tests 273/273, Acceptance 631/631**. Not tested in game.

Every guard applies to mod companions only (`ModRuntime.IsModHero`: hero template id in the registry); vanilla heroes keep
vanilla behavior. Every prevented action writes one log line (companion id + what was prevented).

## Per task

| Task | Done |
|---|---|
| V1 relation removal (82) | Prefix on `CompanionRolesCampaignBehavior.OnHeroRelationChanged` (E1): skipped for a pair with a mod companion. The relation change itself has already been written. Log: `Companion '<id>': relation with the player is now <n> (change <d>); vanilla removal prevented.` (only when vanilla would really have removed it). `KillCharacterAction` stays vanilla for deaths etc. GAME_MAP Q21 "Mod guard". |
| V2 grievances (83 → 84) | GAME_MAP Q33 (worker, verified): one behavior, four grievance types (starvation, unpaid wages, player raided a village, player abandoned a battle), trait-gated (Generosity / Mercy / Valor > 0), opened as a map conversation, consequences are relation changes only (+10 / −2 / −5 / −15) — removal happens indirectly through the V1 path. Exclusion: no new grievance (per-hero gate), saved rows of mod companions dropped hourly, both openers false for mod companions → no grievance line, no consequence. Log: `vanilla grievance prevented` / `saved vanilla grievance removed`. |
| V3 audit (85 → 86) | GAME_MAP Q34 (worker, 106 rows, verified). Every *exclude* row re-checked in decomp by me (table below). New guard: the over-limit removal. |
| V4 relation helper (87) | GAME_MAP Q21: **one read API** `CharacterRelationManager.GetHeroRelation` (the stored value; `Hero.GetBaseHeroRelation` goes through a replaceable diplomacy model, `Hero.GetRelation` is the effective value = stored + personality modifiers for two companions of the player clan). Write `Hero.SetPersonalRelation` — **verified: fires no relation-changed event** (no notification, no Charm XP, no V1 check). Game helper `CompanionRelations`; console `albocarius.relation <a> <b> [value]` (text side in Core, unit-tested). No content uses it yet; the mod had no companion ↔ companion access to migrate. |
| V5 dismissal (88) | GAME_MAP Q19: the dismissal path (`ApplyByFire` + tavern placement) changes no relation, calls no grievance member and never reaches `KillCharacterAction.ApplyByRemove`; none of the guards calls into it. No change needed. |
| Version + report (89) | 0.2.1, this file. |

## V3 — verdict on every *exclude* row (my check, one-line reason)

| Vanilla member | Verdict | Reason |
|---|---|---|
| `CompanionsCampaignBehavior.TryKillCompanion` | exclude — already covered | picks only from `_aliveCompanionTemplates`; the existing `AddToAliveCompanions` prefix keeps mod templates out |
| `CompanionsCampaignBehavior.OnHeroCreated` / `OnHeroOccupationChanged` | exclude — already covered | both register through `AddToAliveCompanions` |
| `PerkResetCampaignBehavior.RemoveACompanionFromPlayerParty` (+ `DailyTick`) | **exclude — new guard** | over the companion limit for 6 days a random companion is removed for good; now a mod companion is skipped (vanilla retries another day) |
| `CompanionRolesCampaignBehavior.OnHeroRelationChanged` | exclude — V1 | relation < −10 → permanent removal |
| `CompanionGrievanceBehavior` | exclude — V2 | complaints in vanilla text + relation penalties |
| `companion_fire_on_consequence` | exclude — already covered | the T4 dismissal prefix |
| `LordConversationsCampaignBehavior` hire option / consequence | exclude — already covered | the Q7 prefix |
| `CompanionsCampaignBehavior.OnHeroKilled` | → keep | bookkeeping after a death; kills nothing itself |
| `PerkResetCampaignBehavior.WarnPlayerAboutCompanionLimit` | → keep | generic limit warning, names no hero |
| `companion_agrees_to_leave_on_condition` | → keep (slot) | it is the `fire_farewell` slot: own line replaces it; empty slot → vanilla (your earlier decision) |
| `conversation_wanderer_player_owned_on_condition` | → keep (slot) | its only line is the `job_status` slot |
| `DefaultNotificationsCampaignBehavior.OnCompanionRemoved` | → keep (lord case: decide) | system toast *about* the hero ("left your clan"), accurate after dismissal; not the hero's voice |
| `PlayerTrackCompanionBehavior` (scattered companions) | → keep | helps find companions scattered after a battle; system popup |
| `IssueManager.CheckIfTroopsCanReturnToMainParty` | → keep | escorts returning; system popup |
| `TavernEmployeesCampaignBehavior.FindCompanionWithType` | → keep | the tavernkeeper tells where a skilled unhired wanderer is |
| `IssueBase.StartIssueWithAlternativeSolution` / `DisableHeroAction` | → **decide** | the player chooses to send the companion; it returns later |

Totals in Q34: 63 keep, 19 decide, 20 exclude rows as written by the research (several are the same mechanism; after my
check: 1 new guard, 6 already covered, 2 covered by slots, 6 keep, 1 decide).

## ⚠ *Decide* list — your call (nothing changed)

1. **Companion becomes a lord** (`turn_companion_to_lord_on_condition`, `ClanNameSelectionIsDone`, king conversation): the hero leaves the companion system; its JSON dialogs would no longer apply. The "is now a lord" toast belongs here too.
2. **Companion limit counts mod companions** (`DefaultClanTierModel.GetCompanionLimit` is the lever): blocks vanilla wanderer hiring (`too_many_companions`), cancels/blocks the tutor and family-feud quests and issues with `CompanionLimitReached`, and triggers the over-limit check (removal now guarded).
3. **Governor / party leader / alley leader** (`ClanFiefsVM`, `ClanPartiesVM`, `DefaultAlleyModel`, `Hero.CanBeGovernorOrHavePartyRole`): a mod companion can be assigned and leaves the main party.
4. **Caravans** (`companion_is_caravan_leader_on_condition`, `companion_is_leading_caravan_condition`): vanilla caravan dialog for a mod companion leading one.
5. **Rescue / prison break** (`companion_rescue_start_condition`, `prison_break_end_with_success_clan_member`): vanilla dialog lines about a captured mod companion (not spoken by it).
6. **Tournaments** (`CanNpcJoinTournament`, `GetTroopPriorityPointForTournament`): an unhired mod wanderer in a town can be entered by the AI; companions get priority.
7. **Issue alternative solutions** (`IssueBase.StartIssueWithAlternativeSolution`): sending a mod companion away on a quest.
8. **Quest companions** (`FamilyFeudIssueBehavior.OnFinalize`, `LordsNeedsTutorIssueBehavior.OnFinalize`): only quest-created heroes, never a mod companion — listed for completeness.
9. **Kingdom expels the player clan** (`ExpelClanDecisionItemVM`): companions processed with the clan.

## UNCERTAIN
- Q33: what the player sees if a grievance conversation opens with every line suppressed — prevented by dropping mod rows before the hourly tick, but an old in-flight grievance from a save made before 0.2.1 is the edge case (cleared on the first hourly tick).
- Q34: `Hero.Clan == null` for mod companions is read from code, not observed; rows gated on `Clan` assume it.
- V3 guard: with only mod companions above the limit, vanilla keeps trying daily (one log line per day) and the companion-limit warning stays.
- Everything in game (checklist below).

## BLOCKERS
None.

## Test checklist (human)
Deploy 0.2.1 (DLLs + SubModule.xml; companion data unchanged in this milestone).
1. [ ] No regression: load, talk, hire, voice + lipsync, conversations (`the_scar`, `spare_coin`), transfers, console.
2. [ ] Hire test_companion; in the console (cheat mode on) `campaign.add_hero_relation <its name> | -60` — the vanilla
   cheat goes through `ChangeRelationAction`, i.e. the real removal path. It stays in the party; log `vanilla removal
   prevented`. (Same with a vanilla companion → it disappears, as in vanilla.)
3. [ ] Grievance: have test_companion (Valor 1) and a vanilla companion with Valor > 0 in the party, start a fight and
   pick **"Try to get away"** (leave soldiers behind). Within an in-game hour the vanilla companion complains on the map;
   test_companion stays silent; log `vanilla grievance prevented`.
4. [ ] With two mod companions hired: `albocarius.relation <a> <b>` → 0; `albocarius.relation <a> <b> -40` → −40; change
   your own relation with either (conversation) → `albocarius.relation <a> <b>` still −40.
5. [ ] Dismiss test_companion → it roams taverns again; rehire works.
