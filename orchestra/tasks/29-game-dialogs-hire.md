# Hard rules (everyone, every task)
- decomp/ is read-only reference. Never copy decompiled TaleWorlds code into the repo.
- Only use game APIs documented in docs/GAME_MAP.md. If it is not there, stop and report.
- Game DLLs: <Reference> with HintPath into ~/bannerlord/dlls, Private=false. Never reference DLC assemblies.
- Core (netstandard2.0) must not reference TaleWorlds.
- JSON is the single source of truth; generated XML only in dist/, never committed.
- Done = `dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build` passes.

# Task 29-game-dialogs-hire: generic companion dialogs and hiring

## Shared rules for this task
- Game layer: `src/AlbocariusCompanions.Game` (net472, compiled against the real game DLLs). There is no way to run the game here:
  the check is the build plus the acceptance tests (incl. `ApiIndexTests`). The orchestrator reviews every API call.
- **Every TaleWorlds type/member you use must have a row in the `## API index` table at the end of `docs/GAME_MAP.md`**
  (format: `| `Type.Member` | `decomp/<path>.cs` | Q<n> | used for |`, the Q must be a VERIFIED section). The APIs listed below are
  already verified in the decomp; add the missing rows yourself. Do not use any API that is not listed here or already in the index.
- Every delegate/handler the game calls must be wrapped with `Safe.Run(ModRuntime.Log, "<context>", ...)` (GAME_MAP Q15: the game
  catches nothing). Conditions use the `Safe.Run<bool>(..., fallback: false)` overload.
- Log meaningful events with `ModRuntime.Log.Info/Warning/Error`. Read `ModRuntime.cs`, `SubModule.cs`, `CompanionHeroFactory.cs` first.
- Keep the existing style (file-scoped namespaces, `internal`, XML doc summaries on types).

## Goal
Implement `CompanionDialogs.Register(CampaignGameStarter starter)` in `src/AlbocariusCompanions.Game/CompanionDialogs.cs` (keep the signature).
No companion-specific code: everything comes from `ModRuntime.Registry` (dialogs.json) and only companions present in `ModRuntime.Templates`.
For each such companion (id = companion id), take the **first** line of each slot. Companion text is always registered as
`"{=" + line.Id + "}" + line.Text["en"]` (localization/voice key = line id, GAME_MAP Q12/Q14). Player prompts are fixed strings in code with
their own ids: `{=albocarius_player_backstory}Tell me about yourself.`, `{=albocarius_player_hire}I could use someone like you in my company.`,
`{=albocarius_player_hire_pay}Very well. Here is {ALBOCARIUS_HIRE_COST} denars.`, `{=albocarius_player_hire_full}I already have more companions than I can manage.`,
`{=albocarius_player_hire_later}Not now.`. Line/token ids are prefixed `albocarius_<id>_` so they never collide with vanilla.
"This companion" = `ModRuntime.IsModHero(Hero.OneToOneConversationHero) && Hero.OneToOneConversationHero.Template.StringId == id`.
Flow (GAME_MAP Q6/Q7: NPC lines = first passing match wins, player lines = all passing options listed by priority):
1. intro: `AddDialogLine(albocarius_<id>_intro, "start", "hero_main_options", <intro>, cond: this companion && !hero.HasMet, cons: hero.SetHasMet(), priority 120)`
   (beats vanilla `start_wanderer_unmet` at 110).
2. backstory: player line on `"hero_main_options"` → `albocarius_<id>_backstory` (cond: this companion), then NPC line on that token → `"hero_main_options"`.
3. hire offer: player line on `"hero_main_options"` → `albocarius_<id>_hire_offer`, cond: this companion && !hero.IsPlayerCompanion && hero.PartyBelongedTo == null;
   NPC hire_offer line on that token → `albocarius_<id>_hire_response`; its condition also sets `MBTextManager.SetTextVariable("ALBOCARIUS_HIRE_COST", hireCost)`.
4. on `albocarius_<id>_hire_response` three player lines:
   - pay: cond `Hero.MainHero.Gold > hireCost && !TooManyCompanions()` (vanilla uses strict >, Q6); cons: `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, hireCost)`,
     `AddCompanionAction.Apply(Clan.PlayerClan, hero)`, `AddHeroToPartyAction.Apply(hero, MobileParty.MainParty)` (vanilla order, Q6), log the hire; → `albocarius_<id>_hire_accept`.
   - full: cond `TooManyCompanions()` → `albocarius_<id>_hire_decline`.
   - later: cond `!TooManyCompanions()` → `albocarius_<id>_hire_decline`.
   `TooManyCompanions()` = `Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit` (Q6).
5. NPC hire_accept line on `albocarius_<id>_hire_accept` → `"close_window"`; NPC hire_decline line on `albocarius_<id>_hire_decline` → `"hero_main_options"`.
hireCost = `package.Companion.HireCost ?? 0`. Log how many lines were registered per companion. The vanilla hire option is already hidden
for mod companions by a Harmony patch (do not touch HarmonyPatches.cs).

## Verified APIs you may use (add missing API-index rows)
`CampaignGameStarter.AddDialogLine`, `CampaignGameStarter.AddPlayerLine` (Q7; decomp/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignGameStarter.cs),
`ConversationSentence.OnConditionDelegate` / `OnConsequenceDelegate` (Q7), `Hero.OneToOneConversationHero`, `Hero.HasMet`, `Hero.SetHasMet`,
`Hero.IsPlayerCompanion`, `Hero.PartyBelongedTo`, `Hero.Gold`, `Hero.MainHero` (Q6; .../TaleWorlds.CampaignSystem/Hero.cs),
`Clan.PlayerClan`, `Clan.Companions`, `Clan.CompanionLimit` (Q6; .../TaleWorlds.CampaignSystem/Clan.cs),
`GiveGoldAction.ApplyBetweenCharacters`, `AddCompanionAction.Apply`, `AddHeroToPartyAction.Apply` (Q6; .../TaleWorlds.CampaignSystem.Actions/),
`MobileParty.MainParty` (Q6; .../TaleWorlds.CampaignSystem.Party/MobileParty.cs), `MBTextManager.SetTextVariable` (Q14; decomp/TaleWorlds.Localization/TaleWorlds.Localization/MBTextManager.cs).
Files allowed to edit: `src/AlbocariusCompanions.Game/CompanionDialogs.cs`, `docs/GAME_MAP.md` (API index rows only).

## Check command
`dotnet build AlbocariusCompanions.sln && dotnet test AlbocariusCompanions.sln --no-build`
