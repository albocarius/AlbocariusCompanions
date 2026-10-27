using System.Linq;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Library;

namespace AlbocariusCompanions.Game;

/// <summary>
/// M10 noble mechanic (GAME_MAP Q44): a mod companion with <c>noble: false</c> made a lord costs the player
/// <see cref="NobleRules.CommonerRaisedRelationChange"/> with every affected clan leader of the player's kingdom
/// (<see cref="NobleRules.IsAffectedClan"/>). Hooked on OnClanCreatedEvent with isCompanion true, whose only raiser is the
/// grant-a-fief conversion; at that point the companion already leads the new clan.
/// </summary>
internal static class NoblePenalty
{
    public static void OnClanCreated(Clan clan, bool isCompanion)
    {
        if (!isCompanion)
            return;

        var lord = clan?.Leader;
        if (lord == null || !ModRuntime.IsModHero(lord))
            return;

        var log = ModRuntime.Log;
        var id = lord.Template.StringId;
        if (!ModRuntime.Registry.TryGet(id, out var package) || package == null)
            return;

        if (!NobleRules.CostsRelation(package.Companion))
        {
            log.Info($"Companion '{id}' made a lord; noble, so no relation change.");
            return;
        }

        var kingdom = Clan.PlayerClan?.Kingdom;
        if (kingdom == null)
        {
            log.Warning($"Companion '{id}' made a lord, but the player has no kingdom; no relation change.");
            return;
        }

        var changed = 0;
        // A copy: relation listeners run inside the loop and must not see a list we are iterating.
        foreach (var other in kingdom.Clans.ToList())
        {
            var leader = other.Leader;
            if (!NobleRules.IsAffectedClan(other == Clan.PlayerClan, other == clan, other.IsEliminated, other.IsUnderMercenaryService,
                    leader != null && leader.IsAlive))
                continue;

            var before = Hero.MainHero.GetRelation(leader);
            // Negative changes are not charm-scaled (Q21). Silent per lord; one summary message follows.
            ChangeRelationAction.ApplyPlayerRelation(leader, NobleRules.CommonerRaisedRelationChange, showQuickNotification: false);
            var after = Hero.MainHero.GetRelation(leader);
            log.Info($"Companion '{id}' made a lord (commoner): relation player ↔ {leader!.Name} ({other.Name}) {before} → {after} " +
                $"(change {NobleRules.CommonerRaisedRelationChange}).");
            changed++;
        }

        log.Info($"Companion '{id}' made a lord (commoner): {changed} clan leader relation(s) changed.");
        if (changed > 0)
            InformationManager.DisplayMessage(new InformationMessage(NobleRules.CommonerRaisedMessage));
    }
}
