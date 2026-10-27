using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q38 triggers (TriggerRegistry.Q38.cs); field names and sources as in the registry.</summary>
    private static void Q38(Dictionary<string, TriggerAdapter> d)
    {
        // ---- party lifecycle & command ----

        d["partyJoinedArmy"] = (owner, fire) => CampaignEvents.OnPartyJoinedArmyEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyJoinedArmy", () => Of(("party", Party(party)), ("clan", Clan(party.ActualClan)))));
        d["partyLeftArmy"] = (owner, fire) => CampaignEvents.PartyRemovedFromArmyEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyLeftArmy", () => Of(("party", Party(party)), ("clan", Clan(party.ActualClan)))));
        d["partyLeaderChanged"] = (owner, fire) => CampaignEvents.OnPartyLeaderChangedEvent.AddNonSerializedListener(owner,
            (party, oldLeader) => Emit(fire, "partyLeaderChanged", () => Of(("party", Party(party)), ("newLeader", Hero(party.LeaderHero)), ("oldLeader", Hero(oldLeader)), ("clan", Clan(party.ActualClan)))));
        d["partyDisbandStarted"] = (owner, fire) => CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyDisbandStarted", () => Of(("party", Party(party)), ("clan", Clan(party.ActualClan)))));
        d["partyDisbanded"] = (owner, fire) => CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(owner,
            (party, settlement) => Emit(fire, "partyDisbanded", () => Of(("party", Party(party)), ("settlement", Settlement(settlement)), ("clan", Clan(party.ActualClan)), ("leader", Hero(party.LeaderHero)))));
        d["partyVisibilityChanged"] = (owner, fire) => CampaignEvents.PartyVisibilityChangedEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyVisibilityChanged", () => Of(("party", Party(party)), ("visible", Bool(party.IsVisible)))));

        // ---- troops: recruiting, upgrading, garrison gifts ----

        d["playerRecruitedTroops"] = (owner, fire) => CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(owner,
            (troop, number) => Emit(fire, "playerRecruitedTroops", () => Of(("troop", Text(troop?.StringId)), ("number", Int(number)))));
        d["heroRecruitedTroops"] = (owner, fire) => CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(owner,
            (recruiter, settlement, source, troop, number) => Emit(fire, "heroRecruitedTroops", () => Of(("recruiter", Hero(recruiter)), ("settlement", Settlement(settlement)), ("source", Hero(source)), ("troop", Text(troop?.StringId)), ("number", Int(number)))));
        d["troopsUpgraded"] = (owner, fire) => CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(owner,
            (oldTroop, newTroop, number) => Emit(fire, "troopsUpgraded", () => Of(("oldTroop", Text(oldTroop?.StringId)), ("newTroop", Text(newTroop?.StringId)), ("number", Int(number)))));
        d["troopsGivenToSettlement"] = (owner, fire) => CampaignEvents.OnTroopGivenToSettlementEvent.AddNonSerializedListener(owner,
            (giver, settlement, _) => Emit(fire, "troopsGivenToSettlement", () => Of(("giver", Hero(giver)), ("settlement", Settlement(settlement)))));
        d["playerPartyStarving"] = (owner, fire) => CampaignEvents.OnMainPartyStarvingEvent.AddNonSerializedListener(owner,
            () => Emit(fire, "playerPartyStarving", () => Of()));

        // ---- player menu actions ----

        d["playerStartedTalkFromMenu"] = (owner, fire) => CampaignEvents.PlayerStartTalkFromMenu.AddNonSerializedListener(owner,
            hero => Emit(fire, "playerStartedTalkFromMenu", () => Of(("hero", Hero(hero)))));
        d["playerJoinedTournament"] = (owner, fire) => CampaignEvents.OnPlayerJoinedTournamentEvent.AddNonSerializedListener(owner,
            (town, isParticipant) => Emit(fire, "playerJoinedTournament", () => Of(("town", Settlement(town)), ("isParticipant", Bool(isParticipant)))));
    }
}
