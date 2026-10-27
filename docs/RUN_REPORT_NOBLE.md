# Run report — noble mechanic (M10)

Version **0.3.1**. Branch `orchestra/foundation`. Not pushed. Not tested in game.

## What was built
When the player makes a mod companion a lord (the grant-a-fief conversation, GAME_MAP Q44) and that companion has
`"noble": false` (the default), the player loses **4** relation with every clan leader of the player's kingdom.

- Hook: `CampaignEvents.OnClanCreatedEvent` with `isCompanion == true` (only raiser: the grant-a-fief conversion).
- Only mod companions (template id in the registry). `noble: true` → logged, nothing changed.
- Affected: clans of `Clan.PlayerClan.Kingdom`, except the player's clan, the new companion clan, eliminated clans,
  mercenary clans and clans without a living leader.
- Write: `ChangeRelationAction.ApplyPlayerRelation(leader, -4, showQuickNotification: false)`. The new lord's relations
  are not changed.
- Feedback: one message "The lords of the realm frown upon raising a commoner to nobility." (only if at least one
  relation changed); each change logged with the relation before and after, plus a count.
- Code: `src/AlbocariusCompanions.Core/Rules/NobleRules.cs`, `src/AlbocariusCompanions.Game/NoblePenalty.cs`, one listener
  in `AlbocariusCompanionsCampaignBehavior.RegisterEvents`. Docs: GAME_MAP Q44 addendum + API index, SCHEMA.md `noble`.

## Tests
Core.Tests 281/281, Acceptance 709/709 (+4 in `NobleTests`: rule, constant + message, clan filter, game wiring).

## Decisions taken here (change if wrong)
- Mercenary clans are not counted as "lords of the realm".
- The vanilla per-lord relation toast is off; the single summary message replaces it.

## In-game checklist
1. Be king of your own kingdom with at least one fief and one other vassal clan. Hire a mod companion with `noble: false`
   (test_companion), give it a fief → the message appears; the mod log lists one line per vassal clan leader with −4
   (before → after); your relation with those lords dropped by 4 in the encyclopedia.
2. The new lord's relation with you is the vanilla +50; its relations with the other lords are unchanged (0 + personality).
3. Same with `"noble": true` → no message, log says "noble, so no relation change".
4. A vanilla companion made a lord → nothing from the mod.

Deploy 0.3.1: both DLLs and `SubModule.xml`.
