using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
