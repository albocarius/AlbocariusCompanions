using System;
using System.Collections.Generic;
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
