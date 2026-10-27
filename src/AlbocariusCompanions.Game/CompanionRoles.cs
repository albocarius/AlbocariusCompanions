using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace AlbocariusCompanions.Game;

/// <summary>
/// A companion's current role for the <c>role</c> condition and the roleStarted/roleEnded polling (M11 T4; GAME_MAP Q45
/// governor / party leader / caravan leader, Q47 alley leader / quest alternative solution). One role at a time, in this
/// order; null when it has none.
/// </summary>
internal static class CompanionRoles
{
    public static string? Of(Hero? hero)
    {
        if (hero == null || !hero.IsAlive)
            return null;
        if (hero.GovernorOf != null)
            return "governor";
        var party = hero.PartyBelongedTo;
        if (party != null && party != MobileParty.MainParty && party.LeaderHero == hero)
            return party.IsCaravan ? "caravanLeader" : "partyLeader";
        var campaign = Campaign.Current;
        if (campaign?.GetCampaignBehavior<IAlleyCampaignBehavior>()?.IsHeroAlleyLeaderOfAnyPlayerAlley(hero) == true)
            return "alleyLeader";
        if (campaign?.IssueManager != null)
            foreach (var solver in campaign.IssueManager.IssueSolvingCompanionList)
                if (solver == hero)
                    return "questAlternative";
        return null;
    }
}
