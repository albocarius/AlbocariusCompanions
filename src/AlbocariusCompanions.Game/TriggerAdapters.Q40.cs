using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q40 triggers (TriggerRegistry.Q40.cs); field names and sources as in the registry.</summary>
    private static void Q40(Dictionary<string, TriggerAdapter> d)
    {
        // ---- kingdoms, war and peace ----
        d["kingdomCreated"] = (owner, fire) => CampaignEvents.KingdomCreatedEvent.AddNonSerializedListener(owner,
            kingdom => Emit(fire, "kingdomCreated", () => Of(("kingdom", Kingdom(kingdom)))));
        d["tradeAgreementSigned"] = (owner, fire) => CampaignEvents.OnTradeAgreementSignedEvent.AddNonSerializedListener(owner,
            (kingdom1, kingdom2) => Emit(fire, "tradeAgreementSigned", () => Of(("firstKingdom", Kingdom(kingdom1)), ("secondKingdom", Kingdom(kingdom2)))));
        d["allianceStarted"] = (owner, fire) => CampaignEvents.OnAllianceStartedEvent.AddNonSerializedListener(owner,
            (proposer, receiver) => Emit(fire, "allianceStarted", () => Of(("proposerKingdom", Kingdom(proposer)), ("receiverKingdom", Kingdom(receiver)))));
        d["allianceEnded"] = (owner, fire) => CampaignEvents.OnAllianceEndedEvent.AddNonSerializedListener(owner,
            (kingdom1, kingdom2) => Emit(fire, "allianceEnded", () => Of(("firstKingdom", Kingdom(kingdom1)), ("secondKingdom", Kingdom(kingdom2)))));
        d["callToWarStarted"] = (owner, fire) => CampaignEvents.OnCallToWarAgreementStartedEvent.AddNonSerializedListener(owner,
            (calling, called, target) => Emit(fire, "callToWarStarted", () => Of(("callingKingdom", Kingdom(calling)), ("calledKingdom", Kingdom(called)), ("targetKingdom", Kingdom(target)))));
        d["callToWarEnded"] = (owner, fire) => CampaignEvents.OnCallToWarAgreementEndedEvent.AddNonSerializedListener(owner,
            (calling, called, target) => Emit(fire, "callToWarEnded", () => Of(("callingKingdom", Kingdom(calling)), ("calledKingdom", Kingdom(called)), ("targetKingdom", Kingdom(target)))));
        d["tributeEarned"] = (owner, fire) => CampaignEvents.OnClanEarnedGoldFromTributeEvent.AddNonSerializedListener(owner,
            (clan, payingFaction) => Emit(fire, "tributeEarned", () => Of(("clan", Clan(clan)), ("payer", Faction(payingFaction)))));

        // ---- settlements ----
        d["settlementOwnerChanged"] = (owner, fire) => CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(owner,
            (settlement, openToClaim, newOwner, oldOwner, capturer, detail) => Emit(fire, "settlementOwnerChanged", () => Of(
                ("settlement", Settlement(settlement)), ("openToClaim", Bool(openToClaim)), ("newOwner", Hero(newOwner)),
                ("oldOwner", Hero(oldOwner)), ("capturer", Hero(capturer)), ("detail", Text(detail)))));
        d["governorChanged"] = (owner, fire) => CampaignEvents.OnGovernorChangedEvent.AddNonSerializedListener(owner,
            (town, oldGovernor, newGovernor) => Emit(fire, "governorChanged", () => Of(("settlement", Settlement(town)), ("oldGovernor", Hero(oldGovernor)), ("newGovernor", Hero(newGovernor)))));

        // ---- service offers and mercenaries ----
        d["serviceOfferCancelled"] = (owner, fire) => CampaignEvents.OnVassalOrMercenaryServiceOfferCanceledEvent.AddNonSerializedListener(owner,
            kingdom => Emit(fire, "serviceOfferCancelled", () => Of(("kingdom", Kingdom(kingdom)))));
        d["mercenaryServiceStarted"] = (owner, fire) => CampaignEvents.OnMercenaryServiceStartedEvent.AddNonSerializedListener(owner,
            (clan, details) => Emit(fire, "mercenaryServiceStarted", () => Of(("clan", Clan(clan)), ("detail", Text(details)))));
        d["mercenaryServiceEnded"] = (owner, fire) => CampaignEvents.OnMercenaryServiceEndedEvent.AddNonSerializedListener(owner,
            (clan, details) => Emit(fire, "mercenaryServiceEnded", () => Of(("clan", Clan(clan)), ("detail", Text(details)))));

        // ---- marriage, children, succession ----
        d["marriageOfferCancelled"] = (owner, fire) => CampaignEvents.OnMarriageOfferCanceledEvent.AddNonSerializedListener(owner,
            (suitor, maiden) => Emit(fire, "marriageOfferCancelled", () => Of(("suitor", Hero(suitor)), ("maiden", Hero(maiden)))));
        d["childConceived"] = (owner, fire) => CampaignEvents.OnChildConceivedEvent.AddNonSerializedListener(owner,
            mother => Emit(fire, "childConceived", () => Of(("mother", Hero(mother)))));
        d["childBorn"] = (owner, fire) => CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(owner,
            (mother, _, stillbornCount) => Emit(fire, "childBorn", () => Of(("mother", Hero(mother)), ("stillbornCount", Int(stillbornCount)))));
        d["heirSelectionRequested"] = (owner, fire) => CampaignEvents.OnHeirSelectionRequestedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "heirSelectionRequested", () => Of()));
        d["heirSelected"] = (owner, fire) => CampaignEvents.OnHeirSelectionOverEvent.AddNonSerializedListener(owner,
            heir => Emit(fire, "heirSelected", () => Of(("heir", Hero(heir)))));
        d["playerCharacterAboutToChange"] = (owner, fire) => CampaignEvents.OnBeforePlayerCharacterChangedEvent.AddNonSerializedListener(owner,
            (oldPlayer, newPlayer) => Emit(fire, "playerCharacterAboutToChange", () => Of(("oldPlayer", Hero(oldPlayer)), ("newPlayer", Hero(newPlayer)))));
        d["playerCharacterChanged"] = (owner, fire) => CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(owner,
            (oldPlayer, newPlayer, newParty, mainPartyChanged) => Emit(fire, "playerCharacterChanged", () => Of(("oldPlayer", Hero(oldPlayer)),
                ("newPlayer", Hero(newPlayer)), ("party", Party(newParty)), ("mainPartyChanged", Bool(mainPartyChanged)))));
        d["clanLeaderChanged"] = (owner, fire) => CampaignEvents.OnClanLeaderChangedEvent.AddNonSerializedListener(owner,
            (oldLeader, newLeader) => Emit(fire, "clanLeaderChanged", () => Of(("oldLeader", Hero(oldLeader)), ("newLeader", Hero(newLeader)))));

        // ---- clans ----
        d["clanDestroyed"] = (owner, fire) => CampaignEvents.OnClanDestroyedEvent.AddNonSerializedListener(owner,
            clan => Emit(fire, "clanDestroyed", () => Of(("clan", Clan(clan)))));
        d["heroChangedClan"] = (owner, fire) => CampaignEvents.OnHeroChangedClanEvent.AddNonSerializedListener(owner,
            (hero, oldClan) => Emit(fire, "heroChangedClan", () => Of(("hero", Hero(hero)), ("oldClan", Clan(oldClan)), ("newClan", Clan(hero?.Clan)))));
        d["clanInfluenceChanged"] = (owner, fire) => CampaignEvents.OnClanInfluenceChangedEvent.AddNonSerializedListener(owner,
            (clan, amount) => Emit(fire, "clanInfluenceChanged", () => Of(("clan", Clan(clan)), ("change", Number(amount)))));
        d["companionRemoved"] = (owner, fire) => CampaignEvents.CompanionRemoved.AddNonSerializedListener(owner,
            (companion, detail) => Emit(fire, "companionRemoved", () => Of(("companion", Hero(companion)), ("detail", Text(detail)))));

        // ---- hero roles and movement ----
        d["heroGotBusy"] = (owner, fire) => CampaignEvents.OnHeroGetsBusyEvent.AddNonSerializedListener(owner,
            (hero, reason) => Emit(fire, "heroGotBusy", () => Of(("hero", Hero(hero)), ("reason", Text(reason)))));
        d["heroSharedFood"] = (owner, fire) => CampaignEvents.OnHeroSharedFoodWithAnotherHeroEvent.AddNonSerializedListener(owner,
            (supporter, supported, influence) => Emit(fire, "heroSharedFood", () => Of(("supporter", Hero(supporter)), ("supported", Hero(supported)), ("influence", Number(influence)))));
        d["heroTeleportRequested"] = (owner, fire) => CampaignEvents.OnHeroTeleportationRequestedEvent.AddNonSerializedListener(owner,
            (hero, targetSettlement, targetParty, detail) => Emit(fire, "heroTeleportRequested", () => Of(("hero", Hero(hero)),
                ("settlement", Settlement(targetSettlement)), ("party", Party(targetParty)), ("detail", Text(detail)))));
    }
}
