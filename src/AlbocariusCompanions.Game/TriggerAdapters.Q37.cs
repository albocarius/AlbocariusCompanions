using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q37 triggers (TriggerRegistry.Q37.cs); field names and sources as in the registry.</summary>
    private static void Q37(Dictionary<string, TriggerAdapter> d)
    {
        // ---- armies and hosted parties ----

        d["armyCreated"] = (owner, fire) => CampaignEvents.ArmyCreated.AddNonSerializedListener(owner,
            army => Emit(fire, "armyCreated", () => Of(("leader", Party(army.LeaderParty)), ("kingdom", Kingdom(army.Kingdom)), ("playerAttached", Bool(army.Parties.Contains(MobileParty.MainParty))))));
        d["armyDispersed"] = (owner, fire) => CampaignEvents.ArmyDispersed.AddNonSerializedListener(owner,
            (army, reason, isPlayersArmy) => Emit(fire, "armyDispersed", () => Of(("leader", Party(army.LeaderParty)), ("kingdom", Kingdom(army.Kingdom)), ("reason", Text(reason)), ("playerAttached", Bool(isPlayersArmy)))));
        d["partyAttached"] = (owner, fire) => CampaignEvents.PartyAttachedAnotherParty.AddNonSerializedListener(owner,
            attachedParty => Emit(fire, "partyAttached", () => Of(("party", Party(attachedParty)), ("host", Party(attachedParty.AttachedTo)))));

        // ---- party lifecycle on the map ----

        d["partyCreated"] = (owner, fire) => CampaignEvents.MobilePartyCreated.AddNonSerializedListener(owner,
            mobileParty => Emit(fire, "partyCreated", () => Of(("party", Party(mobileParty)), ("clan", Clan(mobileParty.Party.Owner?.Clan)))));
        d["partyDestroyed"] = (owner, fire) => CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(owner,
            (destroyedParty, destroyerParty) => Emit(fire, "partyDestroyed", () => Of(("party", Party(destroyedParty)), ("destroyer", Party(destroyerParty)), ("clan", Clan(destroyedParty.Party.Owner?.Clan)))));

        // ---- settlement visits, village raid states, town construction ----

        d["settlementEntered"] = (owner, fire) => CampaignEvents.SettlementEntered.AddNonSerializedListener(owner,
            (mobileParty, settlement, hero) => Emit(fire, "settlementEntered", () => Of(("settlement", Settlement(settlement)), ("party", Party(mobileParty)), ("hero", Hero(hero)))));
        d["settlementLeft"] = (owner, fire) => CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(owner,
            (mobileParty, settlement) => Emit(fire, "settlementLeft", () => Of(("settlement", Settlement(settlement)), ("party", Party(mobileParty)))));
        d["villageStateChanged"] = (owner, fire) => CampaignEvents.VillageStateChanged.AddNonSerializedListener(owner,
            (village, oldState, newState, raiderParty) => Emit(fire, "villageStateChanged", () => Of(("village", Settlement(village)), ("oldState", Text(oldState)), ("newState", Text(newState)), ("raider", Party(raiderParty)))));
        d["buildingLevelChanged"] = (owner, fire) => CampaignEvents.OnBuildingLevelChangedEvent.AddNonSerializedListener(owner,
            (town, building, levelChange) => Emit(fire, "buildingLevelChanged", () => Of(("town", Settlement(town)), ("building", Text(building.BuildingType.StringId)), ("level", Int(building.CurrentLevel)), ("delta", Int(levelChange)))));

        // ---- crime: town alley ownership and the two alley-fight outcomes ----
        // The registry's "alley.AreaState" is the Alley.AreaState enum; the member that reads it is Alley.State.

        d["alleyOwnerChanged"] = (owner, fire) => CampaignEvents.AlleyOwnerChanged.AddNonSerializedListener(owner,
            (alley, newOwner, oldOwner) => Emit(fire, "alleyOwnerChanged", () => Of(("newOwner", Hero(newOwner)), ("oldOwner", Hero(oldOwner)), ("areaState", Text(alley.State)))));
        d["alleyTakenOver"] = (owner, fire) => CampaignEvents.AlleyOccupiedByPlayer.AddNonSerializedListener(owner,
            (_, _) => Emit(fire, "alleyTakenOver", () => Of()));
        d["alleyCleared"] = (owner, fire) => CampaignEvents.AlleyClearedByPlayer.AddNonSerializedListener(owner,
            _ => Emit(fire, "alleyCleared", () => Of()));

        // ---- tournaments ----

        d["tournamentStarted"] = (owner, fire) => CampaignEvents.TournamentStarted.AddNonSerializedListener(owner,
            town => Emit(fire, "tournamentStarted", () => Of(("town", Settlement(town)))));
        d["tournamentFinished"] = (owner, fire) => CampaignEvents.TournamentFinished.AddNonSerializedListener(owner,
            (winner, _, town, prize) => Emit(fire, "tournamentFinished", () => Of(("winner", Hero(winner)), ("town", Settlement(town)), ("prize", Item(prize)))));
        d["tournamentCancelled"] = (owner, fire) => CampaignEvents.TournamentCancelled.AddNonSerializedListener(owner,
            town => Emit(fire, "tournamentCancelled", () => Of(("town", Settlement(town)))));
        d["playerEliminatedFromTournament"] = (owner, fire) => CampaignEvents.PlayerEliminatedFromTournament.AddNonSerializedListener(owner,
            (round, town) => Emit(fire, "playerEliminatedFromTournament", () => Of(("round", Int(round)), ("town", Settlement(town)))));

        // ---- mission scenes and conversations: neither argument names a payload member, so no fields ----

        d["sceneEntered"] = (owner, fire) => CampaignEvents.AfterMissionStarted.AddNonSerializedListener(owner,
            _ => Emit(fire, "sceneEntered", () => Of()));
        d["conversationEnded"] = (owner, fire) => CampaignEvents.ConversationEnded.AddNonSerializedListener(owner,
            _ => Emit(fire, "conversationEnded", () => Of()));

        // ---- economy: gold and item transfers between heroes and parties ----

        d["goldGiven"] = (owner, fire) => CampaignEvents.HeroOrPartyTradedGold.AddNonSerializedListener(owner,
            (giver, receiver, amount, _) => Emit(fire, "goldGiven", () => Of(("giver", Hero(giver.Item1)), ("giverParty", Party(giver.Item2)), ("receiver", Hero(receiver.Item1)), ("receiverParty", Party(receiver.Item2)), ("amount", Int(amount.Item1)), ("transactionId", Text(amount.Item2)))));
        d["itemGiven"] = (owner, fire) => CampaignEvents.HeroOrPartyGaveItem.AddNonSerializedListener(owner,
            (giver, receiver, item, _) => Emit(fire, "itemGiven", () => Of(("giver", Hero(giver.Item1)), ("giverParty", Party(giver.Item2)), ("receiver", Hero(receiver.Item1)), ("receiverParty", Party(receiver.Item2)), ("item", Item(item.EquipmentElement.Item)), ("amount", Int(item.Amount)))));
    }
}
