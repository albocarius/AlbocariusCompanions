using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Creates the hero for one companion from its registered template (GAME_MAP Q4, Q11). Placement in a settlement is
/// the caller's job (Q3). The template has occupation Special, so the passed age is honoured; the hero is then switched
/// to Wanderer so vanilla tavern/hiring mechanics apply (Q2, Q3, Q6).
/// </summary>
internal static class CompanionHeroFactory
{
    public static Hero Create(CharacterObject template, CompanionDefinition definition, Settlement bornSettlement)
    {
        var hero = HeroCreator.CreateSpecialHero(template, bornSettlement, null, null, definition.Age ?? -1);
        hero.SetNewOccupation(Occupation.Wanderer);

        // Creation adds random noise to template skills and auto-distributes attribute points (Q4): pin JSON values.
        if (definition.Skills != null)
        {
            foreach (var entry in definition.Skills)
            {
                var skill = MBObjectManager.Instance.GetObject<SkillObject>(entry.Key);
                if (skill != null)
                    hero.SetSkillValue(skill, entry.Value);
            }
        }

        if (definition.Attributes != null)
        {
            foreach (var name in CompanionRules.AttributeNames)
            {
                var attribute = MBObjectManager.Instance.GetObject<CharacterAttribute>(name);
                if (attribute == null || !definition.Attributes.TryGetValue(name, out var target))
                    continue;
                hero.HeroDeveloper.AddAttribute(attribute, target - hero.GetAttributeValue(attribute), checkUnspentPoints: false);
            }
        }

        hero.ChangeState(Hero.CharacterStates.Active);
        return hero;
    }
}
