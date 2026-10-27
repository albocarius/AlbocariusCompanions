using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Triggers;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q36 triggers (TriggerRegistry.Q36.cs); field names and sources as in the registry.</summary>
    private static void Q36(Dictionary<string, TriggerAdapter> d)
    {
        // ---- siege: the whole siege, one of its battles, its aftermath and the naval blockade ----

        d["siegeEnded"] = (owner, fire) => CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(owner,
            siegeEvent => Emit(fire, "siegeEnded", () => Of(("settlement", Settlement(siegeEvent.BesiegedSettlement)), ("attacker", Party(siegeEvent.BesiegerCamp.LeaderParty)))));
        d["siegeBattleEnded"] = (owner, fire) => CampaignEvents.SiegeCompletedEvent.AddNonSerializedListener(owner,
            (settlement, leaderParty, isWin, battleType) => Emit(fire, "siegeBattleEnded", () => Of(("settlement", Settlement(settlement)), ("leader", Party(leaderParty)), ("leaderWon", Bool(isWin)), ("battleType", Text(battleType)))));
        d["siegeAftermathApplied"] = (owner, fire) => CampaignEvents.OnSiegeAftermathAppliedEvent.AddNonSerializedListener(owner,
            (attackerParty, settlement, aftermathType, previousSettlementOwner, _) => Emit(fire, "siegeAftermathApplied", () => Of(("attacker", Party(attackerParty)), ("settlement", Settlement(settlement)), ("aftermath", Text(aftermathType)), ("previousOwner", Clan(previousSettlementOwner)))));
        d["blockadeStarted"] = (owner, fire) => CampaignEvents.OnBlockadeActivatedEvent.AddNonSerializedListener(owner,
            siegeEvent => Emit(fire, "blockadeStarted", () => Of(("settlement", Settlement(siegeEvent.BesiegedSettlement)), ("attacker", Party(siegeEvent.BesiegerCamp.LeaderParty)))));
        d["blockadeEnded"] = (owner, fire) => CampaignEvents.OnBlockadeDeactivatedEvent.AddNonSerializedListener(owner,
            siegeEvent => Emit(fire, "blockadeEnded", () => Of(("settlement", Settlement(siegeEvent.BesiegedSettlement)), ("attacker", Party(siegeEvent.BesiegerCamp.LeaderParty)))));
        d["partyJoinedSiege"] = (owner, fire) => CampaignEvents.OnMobilePartyJoinedToSiegeEventEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyJoinedSiege", () => Of(("party", Party(party)))));
        d["partyLeftSiege"] = (owner, fire) => CampaignEvents.OnMobilePartyLeftSiegeEventEvent.AddNonSerializedListener(owner,
            party => Emit(fire, "partyLeftSiege", () => Of(("party", Party(party)))));

        // ---- raids and forced levies: the MapEvent of the component plus the direct winnerSide argument ----

        d["villageRaidEnded"] = (owner, fire) => CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(owner,
            (winnerSide, raidEvent) => Emit(fire, "villageRaidEnded", () => Of(MapEvent(raidEvent.MapEvent).Concat(new (string, PayloadValue?)[] { ("winnerSide", Text(winnerSide)) }).ToArray())));
        d["volunteersForced"] = (owner, fire) => CampaignEvents.ForceVolunteersCompletedEvent.AddNonSerializedListener(owner,
            (winnerSide, forceVolunteersEvent) => Emit(fire, "volunteersForced", () => Of(MapEvent(forceVolunteersEvent.MapEvent).Concat(new (string, PayloadValue?)[] { ("winnerSide", Text(winnerSide)) }).ToArray())));
        d["suppliesForced"] = (owner, fire) => CampaignEvents.ForceSuppliesCompletedEvent.AddNonSerializedListener(owner,
            (winnerSide, forceSuppliesEvent) => Emit(fire, "suppliesForced", () => Of(MapEvent(forceSuppliesEvent.MapEvent).Concat(new (string, PayloadValue?)[] { ("winnerSide", Text(winnerSide)) }).ToArray())));

        // ---- prisoners: the roster arguments carry no party or hero, so these three have no payload fields ----

        d["prisonersTaken"] = (owner, fire) => CampaignEvents.OnPrisonerTakenEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "prisonersTaken", () => Of()));
        d["prisonersReleased"] = (owner, fire) => CampaignEvents.OnPrisonerReleasedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "prisonersReleased", () => Of()));
        d["prisonersRecruited"] = (owner, fire) => CampaignEvents.OnMainPartyPrisonerRecruitedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "prisonersRecruited", () => Of()));
        d["prisonersSold"] = (owner, fire) => CampaignEvents.OnPrisonerSoldEvent.AddNonSerializedListener(owner,
            (sellerParty, buyerParty, _) => Emit(fire, "prisonersSold", () => Of(("seller", Party(sellerParty)), ("buyer", Party(buyerParty)))));
        d["prisonersDonated"] = (owner, fire) => CampaignEvents.OnPrisonerDonatedToSettlementEvent.AddNonSerializedListener(owner,
            (party, _, settlement) => Emit(fire, "prisonersDonated", () => Of(("settlement", Settlement(settlement)), ("party", Party(party)))));

        // ---- loot after a field battle ----

        d["lootDistributed"] = (owner, fire) => CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(owner,
            (winnerParty, defeatedParty, _) => Emit(fire, "lootDistributed", () => Of(("winner", Party(winnerParty)), ("defeated", Party(defeatedParty)))));
        d["lootCollected"] = (owner, fire) => CampaignEvents.OnCollectLootsItemsEvent.AddNonSerializedListener(owner,
            (winnerParty, _) => Emit(fire, "lootCollected", () => Of(("winner", Party(winnerParty)))));

        // ---- hideouts and desertion ----

        d["hideoutSpotted"] = (owner, fire) => CampaignEvents.OnHideoutSpottedEvent.AddNonSerializedListener(owner,
            (spotterParty, hideoutParty) => Emit(fire, "hideoutSpotted", () => Of(("spotter", Party(spotterParty)), ("hideout", Settlement(hideoutParty.Settlement)))));
        d["hideoutEmptied"] = (owner, fire) => CampaignEvents.OnHideoutDeactivatedEvent.AddNonSerializedListener(owner,
            hideoutSettlement => Emit(fire, "hideoutEmptied", () => Of(("settlement", Settlement(hideoutSettlement)))));
        d["troopsDeserted"] = (owner, fire) => CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(owner,
            (party, _) => Emit(fire, "troopsDeserted", () => Of(("party", Party(party)))));
    }
}
