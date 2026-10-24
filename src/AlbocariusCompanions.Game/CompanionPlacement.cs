using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace AlbocariusCompanions.Game;

/// <summary>Where a partyless mod companion waits in a tavern: initial spawn and return after dismissal use the same rule.</summary>
internal static class CompanionPlacement
{
    /// <summary>
    /// Town choice like vanilla (GAME_MAP Q1/Q3): every town except the one the player is standing in, preferring a town
    /// of the template's culture, then any candidate; one of the pool at random. Null when no town is available.
    /// </summary>
    public static Town? PickTown(CharacterObject template)
    {
        var playerSettlement = Hero.MainHero?.CurrentSettlement;
        var candidates = Town.AllTowns
            .Where(town => town.Settlement != null && town.Settlement != playerSettlement)
            .ToList();

        var cultureMatched = template.Culture == null
            ? new List<Town>()
            : candidates.Where(town => town.Culture == template.Culture).ToList();

        var pool = cultureMatched.Count > 0 ? cultureMatched : candidates;
        if (pool.Count == 0)
            return null;

        return pool[MBRandom.RandomInt(pool.Count)];
    }

    /// <summary>
    /// Back to an unhired wanderer in a tavern, as vanilla spawns wanderers (GAME_MAP Q19): active state, then the only
    /// writer of StayingInSettlement. Returns the settlement, or null when no town is available (hero left as it is).
    /// </summary>
    public static Settlement? ReturnToTavern(Hero hero)
    {
        var town = PickTown(hero.Template ?? hero.CharacterObject);
        if (town == null)
            return null;

        hero.ChangeState(Hero.CharacterStates.Active);
        EnterSettlementAction.ApplyForCharacterOnly(hero, town.Settlement);
        return town.Settlement;
    }
}
