using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Rules;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Keeps mod companions out of the vanilla wanderer count and away from the daily wanderer removal (GAME_MAP Q10).
/// With the Special-occupation template the hero is already rejected by IsTemplateKnown; this prefix is the explicit guard.
/// </summary>
[HarmonyPatch(typeof(CompanionsCampaignBehavior), "AddToAliveCompanions")]
internal static class ExcludeModCompanionsFromWandererCount
{
    // Original: private void AddToAliveCompanions(Hero companion, bool isTemplateControlled = false)
    private static bool Prefix(Hero companion)
    {
        try
        {
            return !ModRuntime.IsModHero(companion);
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("AddToAliveCompanions prefix failed; running vanilla code", ex);
            return true;
        }
    }
}

/// <summary>
/// Hides the vanilla "I can use someone like you" hire option for mod companions; the mod registers its own hire line
/// with the JSON hireCost (GAME_MAP Q6, Q7).
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_hero_hire_on_condition")]
internal static class HideVanillaHireOptionForModCompanions
{
    // Original: private bool conversation_hero_hire_on_condition()
    private static bool Prefix(ref bool __result)
    {
        try
        {
            if (!ModRuntime.IsModHero(Hero.OneToOneConversationHero))
                return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("hire-condition prefix failed; running vanilla code", ex);
            return true;
        }
    }
}

/// <summary>
/// Plays mod voice lines from the companion folder (audio/&lt;line id&gt;.ogg). The vanilla model picks a path by
/// accent/gender/persona file-name tokens and the hero's generated character id (GAME_MAP Q12); none of these fit
/// author-named files, so for paths written by VoicedLinesFile the result is replaced with the real file.
/// </summary>
[HarmonyPatch(typeof(DefaultVoiceOverModel), nameof(DefaultVoiceOverModel.GetSoundPathForCharacter))]
internal static class PlayModVoiceLinesFromCompanionFolder
{
    private static void Postfix(VoiceObject voiceObject, ref string __result)
    {
        try
        {
            if (voiceObject == null)
                return;
            foreach (var voicePath in voiceObject.VoicePaths)
            {
                var resolved = VoicedLinesXml.ResolveSoundPath(voicePath, ModRuntime.ModuleId);
                if (resolved == null)
                    continue;
                __result = resolved;
                return;
            }
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("voice-over postfix failed; keeping the vanilla result", ex);
        }
    }
}

/// <summary>
/// Player dismissal of a mod companion ("I no longer have need of your services."): vanilla fires the companion and then
/// removes the hero from the game (KillCharacterAction.ApplyByRemove), which is why it vanished from the map. For mod
/// companions the fire step runs, the removal is skipped and the hero goes back to a tavern as an unhired wanderer
/// (GAME_MAP Q19). This dialog consequence is the only player-dismissal path; deaths, captivity, quests and clan moves
/// use other RemoveCompanionAction entry points and are not touched.
/// </summary>
[HarmonyPatch(typeof(CompanionRolesCampaignBehavior), "companion_fire_on_consequence")]
internal static class ReturnDismissedModCompanionToTavern
{
    // Original: private void companion_fire_on_consequence()
    private static bool Prefix()
    {
        var isModHero = false;
        try
        {
            var hero = Hero.OneToOneConversationHero;
            isModHero = ModRuntime.IsModHero(hero);
            if (!isModHero)
                return true;

            RemoveCompanionAction.ApplyByFire(hero.CompanionOf, hero);
            ModRuntime.Log.Info($"Companion '{hero.Template.StringId}' (hero {hero.StringId}) dismissed by the player.");

            var settlement = CompanionPlacement.ReturnToTavern(hero);
            if (settlement != null)
                ModRuntime.Log.Info($"Companion '{hero.Template.StringId}' returns to the tavern of {settlement.Name} ('{settlement.StringId}').");
            else
                ModRuntime.Log.Warning($"Companion '{hero.Template.StringId}' was dismissed but no town is available; it stays a fugitive.");

            // Same ending as the vanilla consequence.
            if (PlayerEncounter.Current != null)
                PlayerEncounter.LeaveEncounter = true;
            return false;
        }
        catch (Exception ex)
        {
            // Vanilla would remove a mod companion for good: for mod heroes never fall back to it.
            ModRuntime.Log.Error("dismissal prefix failed" + (isModHero ? "; vanilla removal skipped for the mod companion" : ""), ex);
            return !isModHero;
        }
    }
}

/// <summary>
/// SPEC_VANILLA_GUARDS V1 (GAME_MAP Q21 "Mod guard"): vanilla removes a hired companion whose relation with the player drops
/// below −10 after a negative change (KillCharacterAction.ApplyByRemove — the hero vanishes, no "left" message). For a pair
/// where the companion is a mod hero the check is skipped; the relation change itself has already happened. The method does
/// nothing else, so skipping it for mod heroes only removes the removal. Vanilla companions keep the vanilla behavior.
/// </summary>
[HarmonyPatch(typeof(CompanionRolesCampaignBehavior), "OnHeroRelationChanged")]
internal static class KeepModCompanionsOnLowRelation
{
    // Original: private void OnHeroRelationChanged(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange,
    //   bool showNotification, ChangeRelationAction.ChangeRelationDetail detail, Hero originalHero, Hero originalGainedRelationWith)
    private static bool Prefix(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange)
    {
        try
        {
            var companion = ModRuntime.IsModHero(effectiveHero) ? effectiveHero
                : ModRuntime.IsModHero(effectiveHeroGainedRelationWith) ? effectiveHeroGainedRelationWith : null;
            if (companion == null)
                return true;

            var other = companion == effectiveHero ? effectiveHeroGainedRelationWith : effectiveHero;
            // Same condition as vanilla's removal: logged only when vanilla would actually have removed the companion.
            if (other == Hero.MainHero && companion.IsPlayerCompanion && relationChange < 0)
            {
                var relation = effectiveHero.GetRelation(effectiveHeroGainedRelationWith);
                if (relation < VanillaCompanionRelationLimit)
                    ModRuntime.Log.Info($"Companion '{companion.Template.StringId}': relation with the player is now {relation} " +
                        $"(change {relationChange}); vanilla removal prevented.");
            }
            return false;
        }
        catch (Exception ex)
        {
            // Falling back to vanilla could remove a mod companion for good; the check has no other effect, so skip it.
            ModRuntime.Log.Error("low-relation guard failed; vanilla removal check skipped", ex);
            return false;
        }
    }

    /// <summary>CompanionRolesCampaignBehavior.CompanionRelationLimit (−10, Q21); private in vanilla.</summary>
    private const int VanillaCompanionRelationLimit = -10;
}

/// <summary>
/// SPEC_VANILLA_GUARDS V2 (GAME_MAP Q33): mod companions never get vanilla grievances. Three layers, all scoped to mod heroes:
/// no new grievance (the per-hero gate returns its default, GrievanceType.Invalid); rows that already exist (older saves)
/// are dropped before the hourly tick can open a grievance conversation; and the two grievance openers never hold for a
/// mod hero, so the consequences (relation changes) cannot be reached. Vanilla companions keep their grievances.
/// </summary>
[HarmonyPatch(typeof(CompanionGrievanceBehavior), "GetGrievanceTypeForCompanion")]
internal static class NoGrievanceForModCompanions
{
    // Original: private GrievanceType GetGrievanceTypeForCompanion(Hero companionHero, GrievanceType type)
    private static bool Prefix(Hero companionHero)
    {
        try
        {
            if (!ModRuntime.IsModHero(companionHero))
                return true;
            // Skipping leaves the enum result at its default, 0 = GrievanceType.Invalid ("no grievance").
            ModRuntime.Log.Info($"Companion '{companionHero.Template.StringId}': vanilla grievance prevented.");
            return false;
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("grievance guard failed; vanilla grievance check runs", ex);
            return true;
        }
    }
}

[HarmonyPatch(typeof(CompanionGrievanceBehavior), "OnHourlyTick")]
internal static class DropModCompanionGrievances
{
    // Original: private void OnHourlyTick() — opens the first pending grievance conversation of a hero in the main party.
    private static void Prefix(CompanionGrievanceBehavior __instance, IDictionary ____heroGrievances)
    {
        try
        {
            var modHeroes = ____heroGrievances?.Keys.OfType<Hero>().Where(ModRuntime.IsModHero).ToList() ?? new List<Hero>();
            foreach (var hero in modHeroes)
            {
                ____heroGrievances!.Remove(hero);
                ModRuntime.Log.Info($"Companion '{hero.Template.StringId}': saved vanilla grievance removed.");
            }

            // A grievance already in flight for a mod hero (older save) would keep the priority-120 opener alive.
            var current = CurrentGrievance.GetValue(__instance);
            if (current != null && ModRuntime.IsModHero(GrievingHero(current)))
                CurrentGrievance.SetValue(__instance, null);
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("grievance cleanup failed", ex);
        }
    }

    private static readonly FieldInfo CurrentGrievance = AccessTools.Field(typeof(CompanionGrievanceBehavior), "_currentGrievance");

    private static Hero? GrievingHero(object grievance) =>
        AccessTools.Field(grievance.GetType(), "GrievingHero")?.GetValue(grievance) as Hero;
}

[HarmonyPatch(typeof(CompanionGrievanceBehavior))]
internal static class SilenceGrievanceOpenersForModCompanions
{
    // Originals: private bool companion_start_grievance_condition(), private bool companion_repeat_grievance_condition()
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CompanionGrievanceBehavior), "companion_start_grievance_condition");
        yield return AccessTools.Method(typeof(CompanionGrievanceBehavior), "companion_repeat_grievance_condition");
    }

    private static bool Prefix(ref bool __result)
    {
        try
        {
            if (!ModRuntime.IsModHero(Hero.OneToOneConversationHero))
                return true;
            __result = false;
            return false;
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("grievance opener guard failed; vanilla condition runs", ex);
            return true;
        }
    }
}

/// <summary>
/// SPEC_VANILLA_GUARDS V3 (GAME_MAP Q34): over the companion limit for six days, vanilla removes a random clan companion
/// for good (PerkResetCampaignBehavior.RemoveACompanionFromPlayerParty → KillCharacterAction.ApplyByRemove). While that
/// method runs, a removal of a mod companion is skipped; vanilla then retries on a later day and may pick a vanilla
/// companion. KillCharacterAction is only guarded inside this window: deaths and every other removal stay vanilla.
/// </summary>
[HarmonyPatch(typeof(PerkResetCampaignBehavior), "RemoveACompanionFromPlayerParty")]
internal static class MarkOverLimitRemoval
{
    [ThreadStatic] internal static bool Active;

    // Original: private void RemoveACompanionFromPlayerParty()
    private static void Prefix() => Active = true;

    private static void Finalizer() => Active = false;
}

[HarmonyPatch(typeof(KillCharacterAction), nameof(KillCharacterAction.ApplyByRemove))]
internal static class KeepModCompanionsOverTheLimit
{
    // Original: public static void ApplyByRemove(Hero victim, bool showNotification = false, bool isForced = true)
    private static bool Prefix(Hero victim)
    {
        try
        {
            if (!MarkOverLimitRemoval.Active || !ModRuntime.IsModHero(victim))
                return true;
            ModRuntime.Log.Info($"Companion '{victim.Template.StringId}': vanilla over-limit removal prevented " +
                $"({Clan.PlayerClan.Companions.Count} companions, limit {Clan.PlayerClan.CompanionLimit}).");
            return false;
        }
        catch (Exception ex)
        {
            // Inside the over-limit window a failure must not remove a mod companion for good.
            ModRuntime.Log.Error("over-limit guard failed", ex);
            return !MarkOverLimitRemoval.Active;
        }
    }
}

/// <summary>
/// Vanilla dialog points as companion slots (SPEC_CORE_GAPS T4.0, GAME_MAP Q18 note): when a mod companion speaks a vanilla
/// NPC sentence whose slot it has lines for, the text shown and voiced is one of its own lines. The vanilla condition already
/// ran (its variables are set) and the consequence and tokens are unchanged; an empty slot keeps the vanilla text.
/// </summary>
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.UpdateCurrentSentenceText))]
internal static class ReplaceVanillaTextForModCompanions
{
    private static void Postfix(ConversationManager __instance, List<ConversationSentence> ____sentences, int ____currentSentence,
        ref TextObject ____currentSentenceText)
    {
        try
        {
            if (____sentences == null || ____currentSentence < 0 || ____currentSentence >= ____sentences.Count)
                return;
            var sentence = ____sentences[____currentSentence];
            if (sentence == null || sentence.IsPlayer)
                return;

            var point = VanillaDialogPoints.ForSentence(sentence.Id);
            if (point == null)
                return;

            var hero = __instance.OneToOneConversationHero;
            if (!ModRuntime.IsModHero(hero) || !ModRuntime.Registry.TryGet(hero.Template.StringId, out var package) || package == null)
                return;

            var text = DialogText.PickText(package, point.Slot);
            if (text != null)
                ____currentSentenceText = text;
        }
        catch (Exception ex)
        {
            ModRuntime.Log.Error("vanilla text replacement failed; showing the vanilla text", ex);
        }
    }
}
