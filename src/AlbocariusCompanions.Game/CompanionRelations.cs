using TaleWorlds.CampaignSystem;

namespace AlbocariusCompanions.Game;

/// <summary>
/// The raw personal relation between two heroes (SPEC_VANILLA_GUARDS V4, GAME_MAP Q21). Every companion ↔ companion read or
/// write of the mod goes through here: Hero.GetRelation is the effective value (clan-leader substitution, personality
/// modifiers), not the personal one. Writes are silent: no relation-changed event, no notification, no vanilla removal check.
/// </summary>
internal static class CompanionRelations
{
    public static int Get(Hero a, Hero b) => CharacterRelationManager.GetHeroRelation(a, b);

    /// <summary>Clamped to the game's relation limits by the game itself.</summary>
    public static void Set(Hero a, Hero b, int value) => a.SetPersonalRelation(b, value);
}
