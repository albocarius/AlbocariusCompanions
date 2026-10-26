# SPEC — AlbocariusCompanions — Milestone: Vanilla guards

> Follows the previous specs; their hard rules and `orchestra/HARD_RULES.md` still apply.
> Goal: stop vanilla systems from acting on mod companions in ways that bypass our design (silent removal, vanilla dialog text). **No new features.** Leaving, morale/mood mechanics and selection rules are designed later.

## 0. Principle

Mod companions are not vanilla wanderers. A vanilla system may act on them only if it is mechanical and neutral (movement, combat, party roles, equipment, skills). A vanilla system must **not**:
- remove, kill, disable or make a mod companion leave the clan/party on its own logic, or
- make a mod companion speak vanilla text (grievances, comments, requests, notifications in its name).

Every exclusion is scoped to mod companions only (registry ids). Vanilla heroes keep vanilla behavior. Every exclusion logs once per occurrence what it prevented.

## 1. Tasks

### V1 — Relation-based removal
GAME_MAP Q21/Q18: a hired companion whose relation with the player drops below the vanilla threshold is removed (`CompanionRolesCampaignBehavior.OnHeroRelationChanged` → `KillCharacterAction.ApplyByRemove`).
- Prevent this for mod companions (Harmony, per GAME_MAP). The relation change itself still happens.
- Log: companion id, new relation, "vanilla removal prevented".
- Done: build + tests; add to checklist: set a hired mod companion's relation to −50 (conversation effect or console) → companion stays in the party.

### V2 — Vanilla grievances
- GAME_MAP: map the vanilla grievance system (behavior class, what triggers each grievance, how it opens the conversation, its texts, and its consequences — relation change, leaving, removal).
- Exclude mod companions from it entirely (no grievance conversations, no grievance consequences).
- Done: GAME_MAP section, exclusion implemented, checklist item describing how to provoke a vanilla grievance (from GAME_MAP) and confirm the mod companion stays silent.

### V3 — Audit of vanilla behaviors touching companions
- GAME_MAP: list every vanilla campaign behavior / action / model that targets player companions or wanderers (e.g. checks `IsPlayerCompanion`, `Occupation.Wanderer`, clan membership of companions). For each: what it does, trigger, consequence, and a classification:
  - **keep** — mechanical and neutral (§0),
  - **exclude** — clearly violates §0 (removal or vanilla text),
  - **decide** — unclear or a gameplay choice (e.g. marriage, becoming a lord/governor, being given a fief, leaving at low party morale, caravans).
- Implement exclusions **only** for items classified *exclude*. Do **not** change anything classified *decide* — list them in the report for the user.
- Include DLC-agnostic notes only (DLC sources stay excluded by rule).

### V4 — Companion ↔ companion relation helper
GAME_MAP Q21: `Hero.GetRelation` returns an *effective* value (clan leader substitution, personality modifiers). For two companions in the player's clan this may not be their personal relation.
- Add one game-layer helper to read and write the **raw personal** relation between two heroes (per Q21: silent `SetPersonalRelation` for writes unless a notification is wanted), and use it for every companion ↔ companion read/write the mod does now or later.
- GAME_MAP: confirm which API returns the raw value and what `GetRelation` returns for two heroes in the same (player) clan.
- No content uses companion ↔ companion relations yet; add a console command `albocarius.relation <a> <b> [value]` to get/set it for testing.

### V5 — Dismissal path check
- Confirm in GAME_MAP and code that the dismissal fix from SPEC_CORE_GAPS T4 does not route through a removal path that V1/V3 exclusions could interfere with (or vice versa). If an interaction exists, document it and fix so that: player dismissal → companion roams taverns again (unchanged behavior).

## 2. Report

`docs/RUN_REPORT_VANILLA_GUARDS.md`: per task done, the V3 table (keep / exclude / decide) with the *decide* list highlighted for the user, UNCERTAINs, BLOCKERS, and test checklist:
1. No regression: load, talk, hire, voice + lipsync, conversations, transfers, console.
2. Relation −50 on a hired mod companion → stays in the party (V1).
3. Provoke a vanilla grievance (method from V2) → mod companion silent, vanilla companions still complain.
4. `albocarius.relation` get/set works between two mod companions; value unaffected by the player's own relations (V4).
5. Dismiss → companion roams taverns again, rehire works (V5).
