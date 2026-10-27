using AlbocariusCompanions.Core.Models;

namespace AlbocariusCompanions.Core.Rules;

/// <summary>
/// The commoner-to-lord relation cost (M10, GAME_MAP Q44): when the player makes a mod companion with <c>noble: false</c> a
/// lord, the player loses relation with the clan leaders of the player's kingdom. The new lord's relations are untouched.
/// </summary>
public static class NobleRules
{
    /// <summary>Change to the player's relation with each affected clan leader. Mod-wide, never per companion.</summary>
    public const int CommonerRaisedRelationChange = -4;

    /// <summary>Shown once in the message log when at least one relation changed.</summary>
    public const string CommonerRaisedMessage = "The lords of the realm frown upon raising a commoner to nobility.";

    /// <summary>Only a non-noble companion costs relation.</summary>
    public static bool CostsRelation(CompanionDefinition companion) => !companion.Noble;

    /// <summary>
    /// Whether a clan of the player's kingdom is affected: not the player's clan, not the clan just created for the
    /// companion, not eliminated, not a mercenary (mercenaries serve the realm but are not its lords), and led by a living hero.
    /// </summary>
    public static bool IsAffectedClan(bool isPlayerClan, bool isNewClan, bool isEliminated, bool isMercenary, bool hasLivingLeader) =>
        !isPlayerClan && !isNewClan && !isEliminated && !isMercenary && hasLivingLeader;
}
