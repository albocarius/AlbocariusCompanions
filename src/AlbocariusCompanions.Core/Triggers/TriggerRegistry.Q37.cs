using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q37 triggers (orchestra/triggers.tsv rows with q = Q37).</summary>
    private static IEnumerable<TriggerDefinition> Q37() => new TriggerDefinition[]
    {
        // Raise sites are campaign/map code (Kingdom.CreateArmy, MobileParty, settlement/village/building actions,
        // TournamentManager) except where a mission raises the event: the alley pair (SandBox MissionAlleyHandler
        // party screen after an alley fight), the tournament-scene raisers (SandBox TournamentBehavior: the last
        // match of tournamentFinished, playerEliminatedFromTournament) and AfterMissionStarted
        // (CampaignMissionComponent) — only those are inMission.

        // ---- armies and hosted parties ----

        new TriggerDefinition("armyCreated", "army",
            "A new army formed somewhere in the world: it has already gathered its parties under a leader when this fires.",
            "ArmyCreated",
            fields: new[]
            {
                F("leader", FieldKind.Party, "the party leading the army", "army.LeaderParty"),
                F("kingdom", FieldKind.Kingdom, "the kingdom that raised the army", "army.Kingdom"),
                F("playerAttached", FieldKind.Bool, "the player's party is part of the army", "army.Parties.Contains(MobileParty.MainParty)"),
            },
            subjectFields: S("leader", "kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("armyDispersed", "army",
            "An army broke up — `reason` says why (cohesion spent, objective finished, leader gone, player captured, …); the member parties can still be read here.",
            "ArmyDispersed",
            fields: new[]
            {
                F("leader", FieldKind.Party, "the party that led the army", "army.LeaderParty"),
                F("kingdom", FieldKind.Kingdom, "the kingdom the army belonged to", "army.Kingdom"),
                F("reason", FieldKind.Text, "the game's ArmyDispersionReason: CohesionDepleted, ObjectiveFinished, PlayerTakenPrisoner, NotEnoughParty, …", "reason"),
                F("playerAttached", FieldKind.Bool, "the player's party was part of the army", "isPlayersArmy"),
            },
            subjectFields: S("leader", "kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("partyAttached", "army",
            "A party merged into another party on the map: the player joined a lord's host, or a party joined the player's host; it also fires again on save-load re-linking.",
            "PartyAttachedAnotherParty",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party that merged", "attachedParty"),
                F("host", FieldKind.Party, "the party it merged into", "attachedParty.AttachedTo"),
            },
            subjectFields: S("party", "host"), defaultSubject: Subjects.PlayerClan),

        // ---- party lifecycle on the map ----

        new TriggerDefinition("partyCreated", "party",
            "A new party appeared on the campaign map — most are vanilla caravan, quest or spawn parties, so gate on the owning clan.",
            "MobilePartyCreated",
            fields: new[]
            {
                F("party", FieldKind.Party, "the newly created party, fully readable", "mobileParty"),
                F("clan", FieldKind.Clan, "the clan owning the party; empty for parties without a clan owner", "mobileParty.Party.Owner?.Clan"),
            },
            subjectFields: S("party", "clan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyDestroyed", "party",
            "A party was destroyed or disbanded — the last moment its rosters, prisoners and leader are still readable, before the party is wiped.",
            "MobilePartyDestroyed",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party being destroyed", "destroyedParty"),
                F("destroyer", FieldKind.Party, "the party that destroyed it; empty when the party disbanded", "destroyerParty"),
                F("clan", FieldKind.Clan, "the clan that owned the destroyed party", "destroyedParty.Party.Owner?.Clan"),
            },
            subjectFields: S("party", "destroyer", "clan"), defaultSubject: Subjects.PlayerClan),

        // ---- settlement visits, village raid states, town construction ----

        new TriggerDefinition("settlementEntered", "settlements",
            "The player entered a settlement (town, castle, village or hideout) with the party; walking in alone as a character fires too, then `party` is empty and `hero` is the player hero.",
            "SettlementEntered",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement entered", "settlement"),
                F("party", FieldKind.Party, "the party that entered", "mobileParty"),
                F("hero", FieldKind.Hero, "the hero on whose behalf the entry happened", "hero"),
            },
            subjectFields: S("settlement", "party", "hero"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("settlementLeft", "settlements",
            "The player's party left a settlement it was inside; leaving alone as a character fires nothing.",
            "OnSettlementLeftEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement being left", "settlement"),
                F("party", FieldKind.Party, "the party leaving", "mobileParty"),
            },
            subjectFields: S("settlement", "party"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("villageStateChanged", "settlements",
            "A village's raid state changed somewhere in the world — gate on `newState`: BeingRaided for a raid starting, ForcedForVolunteers/ForcedForSupplies for a levy, Looted when the village was burned out.",
            "VillageStateChanged",
            fields: new[]
            {
                F("village", FieldKind.Settlement, "the village whose state changed", "village.Settlement"),
                F("oldState", FieldKind.Text, "previous state: Normal, BeingRaided, ForcedForVolunteers, ForcedForSupplies, Looted", "oldState"),
                F("newState", FieldKind.Text, "new state, same values", "newState"),
                F("raider", FieldKind.Party, "the raiding or levying party behind the state change", "raiderParty"),
            },
            subjectFields: S("village", "raider"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("buildingLevelChanged", "settlements",
            "A building in a town went up or down a level — normally construction finishing (+1) in a player-owned fief, or an incident/cheat removing a level.",
            "OnBuildingLevelChangedEvent",
            fields: new[]
            {
                F("town", FieldKind.Settlement, "the town or castle whose building changed level", "town.Settlement"),
                F("building", FieldKind.Text, "the game's StringId of the building type", "building.BuildingType.StringId"),
                F("level", FieldKind.Int, "the building's level after the change", "building.CurrentLevel"),
                F("delta", FieldKind.Int, "the level change: usually +1 or -1, incident effects move it by more", "levelDelta"),
            },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan),

        // ---- crime: town alley ownership and the two alley-fight outcomes ----
        // The Alley argument names no member giving its settlement or an id (Q37 rows), and the alley pair's
        // TroopRoster argument has an unsettled meaning (Q37 Uncertain), so those arguments are left out.

        new TriggerDefinition("alleyOwnerChanged", "crime",
            "The owner of a town alley (hideout) changed: the player took or lost one, or a gang leader got it; the alley itself is not identifiable in the payload.",
            "AlleyOwnerChanged",
            fields: new[]
            {
                F("newOwner", FieldKind.Hero, "the hero who owns the alley now; empty when the alley went empty", "newOwner"),
                F("oldOwner", FieldKind.Hero, "the previous owner; empty when the alley was empty", "oldOwner"),
                F("areaState", FieldKind.Text, "the alley's occupancy now: OccupiedByPlayer, OccupiedByGangLeader, Empty", "alley.AreaState"),
            },
            subjectFields: S("newOwner", "oldOwner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("alleyTakenOver", "crime",
            "The player took over a hideout at the end of an alley fight; the takeover and the roster moves are already applied.",
            "AlleyOccupiedByPlayer",
            // always the player, but nothing in the payload names an entity — only "any" can match.
            defaultSubject: Subjects.Any,
            inMission: true), // SandBox MissionAlleyHandler: the after-battle party screen of the alley mission
        new TriggerDefinition("alleyCleared", "crime",
            "After winning an alley fight the player chose to leave the hideout empty instead of taking it over.",
            "AlleyClearedByPlayer",
            // always the player, but nothing in the payload names an entity — only "any" can match.
            defaultSubject: Subjects.Any,
            inMission: true), // SandBox MissionAlleyHandler: the "leave it empty" choice of the alley mission

        // ---- tournaments ----

        new TriggerDefinition("tournamentStarted", "tournaments",
            "A tournament was scheduled at a town somewhere on the map and is already active; it fires even when the player is nowhere near.",
            "TournamentStarted",
            fields: new[] { F("town", FieldKind.Settlement, "the town holding the tournament", "town.Settlement") },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("tournamentFinished", "tournaments",
            "A town's tournament ended and the prize was paid: the player won only if they attended (`winner.isPlayer`), otherwise the winner is one of the simulated participants.",
            "TournamentFinished",
            // participants is a list argument; the row names no member that gives it a simple payload value, so it is left out.
            fields: new[]
            {
                F("winner", FieldKind.Hero, "the hero who won the tournament", "winner.HeroObject"),
                F("town", FieldKind.Settlement, "the town that held the tournament", "town.Settlement"),
                F("prize", FieldKind.Item, "the prize item given to the winner", "prize"),
            },
            subjectFields: S("winner", "town"), defaultSubject: Subjects.PlayerClan, common: true,
            inMission: true), // one of the two raisers is SandBox TournamentBehavior's last match inside the tournament scene
        new TriggerDefinition("tournamentCancelled", "tournaments",
            "A town's scheduled tournament was cancelled because the town was under siege when its end-time came; it fires instead of tournamentFinished.",
            "TournamentCancelled",
            fields: new[] { F("town", FieldKind.Settlement, "the town whose tournament was cancelled", "town.Settlement") },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("playerEliminatedFromTournament", "tournaments",
            "The player lost their match and is out of the town's tournament for good; the lost bet is already paid.",
            "PlayerEliminatedFromTournament",
            fields: new[]
            {
                F("round", FieldKind.Int, "the tournament round the player lost", "currentRoundIndex"),
                F("town", FieldKind.Settlement, "the town holding the tournament", "town.Settlement"),
            },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan,
            inMission: true), // SandBox TournamentBehavior runs the elimination inside the tournament scene

        // ---- mission scenes and conversations ----

        new TriggerDefinition("sceneEntered", "scenes",
            "A mission scene the player is in started and is fully live — combat, siege, town, tavern or hideout visit, conversation — and it fires for every one of them.",
            "AfterMissionStarted",
            // mission names no exposed kind (mission.MainAgent is always the player's agent, scene kind is not a
            // payload member), so there is no field and only "any" can match.
            defaultSubject: Subjects.Any,
            inMission: true), // SandBox CampaignMissionComponent: the scene is live when this fires
        new TriggerDefinition("conversationEnded", "scenes",
            "A vanilla game conversation ended and its characters are about to be released; it fires for every conversation in the game.",
            "ConversationEnded",
            // conversationCharacters is a list argument; the row names no member that gives it a simple payload
            // value, so it is left out — and only "any" can match then.
            defaultSubject: Subjects.Any),

        // ---- economy: gold and item transfers between heroes and parties ----
        // showQuickInformation / showNotification are pure UI flags and are left out of both entries.

        new TriggerDefinition("goldGiven", "economy",
            "Gold moved between a hero or party and another hero or party — loot, ransoms, bribes, quest rewards and more; it fires for every payment in the world, so gate strictly on the player side and `transactionId`.",
            "HeroOrPartyTradedGold",
            fields: new[]
            {
                F("giver", FieldKind.Hero, "hero side of the payer; empty for party-only transactions", "giver.Item1"),
                F("giverParty", FieldKind.Party, "party side of the payer; empty for hero-only transactions", "giver.Item2"),
                F("receiver", FieldKind.Hero, "hero side of the payee", "receiver.Item1"),
                F("receiverParty", FieldKind.Party, "party side of the payee", "receiver.Item2"),
                F("amount", FieldKind.Int, "the gold that moved, already clamped to what the payer could pay", "amount"),
                F("transactionId", FieldKind.Text, "the game's id of what was paid for (loot, ransom, bribe, quest reward, …)", "transactionStringId"),
            },
            subjectFields: S("giver", "giverParty", "receiver", "receiverParty"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("itemGiven", "economy",
            "A hero or party handed an item stack to another hero or party (quest trades, gifts); only transfers that ran through a party roster fire.",
            "HeroOrPartyGaveItem",
            fields: new[]
            {
                F("giver", FieldKind.Hero, "hero side of the giver; empty for party-only transfers", "giver.Item1"),
                F("giverParty", FieldKind.Party, "party side of the giver", "giver.Item2"),
                F("receiver", FieldKind.Hero, "hero side of the receiver", "receiver.Item1"),
                F("receiverParty", FieldKind.Party, "party side of the receiver", "receiver.Item2"),
                F("item", FieldKind.Item, "the item that was handed over", "item.EquipmentElement.Item"),
                F("amount", FieldKind.Int, "how many of the item moved", "item.Amount"),
            },
            subjectFields: S("giver", "giverParty", "receiver", "receiverParty"), defaultSubject: Subjects.PlayerClan),
    };
}
