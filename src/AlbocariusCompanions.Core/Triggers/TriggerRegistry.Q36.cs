using System.Collections.Generic;
using System.Linq;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q36 triggers (orchestra/triggers.tsv rows with q = Q36).</summary>
    private static IEnumerable<TriggerDefinition> Q36() => new TriggerDefinition[]
    {
        // Every raise site in Q36 is campaign/map code (SiegeEvent.FinalizeSiegeEvent, MapEvent.FinalizeEventAux,
        // HideoutCampaignBehavior, DesertionCampaignBehavior, party screens), so none of these is inMission.

        // ---- siege: the whole siege, one of its battles, its aftermath and the naval blockade ----

        new TriggerDefinition("siegeEnded", "siege",
            "A siege is over and its outcome is decided: the last assault or sally failed, the besiegers broke off, and who holds the settlement is settled.",
            "OnSiegeEventEndedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement that was besieged", "siegeEvent.BesiegedSettlement"),
                F("attacker", FieldKind.Party, "leading party of the besieger camp", "siegeEvent.BesiegerCamp.LeaderParty"),
            },
            subjectFields: S("settlement", "attacker"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("siegeBattleEnded", "siege",
            "One battle of a siege ended with a decided winner — an assault, a sally-out or a blockade battle; the siege itself may go on.",
            "SiegeCompletedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the besieged settlement this battle was fought at", "settlement"),
                // The leader argument changes side with the battle type (Q36 note): besieger camp for an assault, garrison for a sally-out.
                F("leader", FieldKind.Party, "the acting side's leading party: the besieger camp for an assault, the sallying garrison for a sally-out", "leaderParty"),
                F("leaderWon", FieldKind.Bool, "true when that leader party's side won this battle", "isWin"),
                F("battleType", FieldKind.Text, "the game's MapEvent.BattleTypes for this battle: Siege, SiegeOutside, SallyOut, BlockadeBattle, …", "battleType"),
            },
            subjectFields: S("settlement", "leader"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("siegeAftermathApplied", "siege",
            "The victors chose what to do with a town or castle they had just taken; the prosperity, loyalty, building and gold losses happen right after this.",
            "OnSiegeAftermathAppliedEvent",
            fields: new[]
            {
                F("attacker", FieldKind.Party, "the party that took it", "attackerParty"),
                F("settlement", FieldKind.Settlement, "the town or castle that was taken", "settlement"),
                F("aftermath", FieldKind.Text, "the game's SiegeAftermathAction.SiegeAftermath: Devastate, Pillage or ShowMercy", "aftermathType"),
                F("previousOwner", FieldKind.Clan, "the clan that held it until now; this is the one that lost the fight", "previousSettlementOwner"),
            },
            subjectFields: S("settlement", "attacker", "previousOwner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("blockadeStarted", "siege",
            "A port was sealed off by a naval blockade: its garrison is cut off from the sea and can only be resupplied by a sally.",
            "OnBlockadeActivatedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the blockaded port settlement", "siegeEvent.BesiegedSettlement"),
                F("attacker", FieldKind.Party, "leading party of the blockading besieger camp", "siegeEvent.BesiegerCamp.LeaderParty"),
            },
            subjectFields: S("settlement", "attacker"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("blockadeEnded", "siege",
            "The naval blockade of a port was lifted — the besiegers lost their ships or a blockade battle was decided; the siege itself may continue on land.",
            "OnBlockadeDeactivatedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the port whose blockade ended", "siegeEvent.BesiegedSettlement"),
                F("attacker", FieldKind.Party, "leading party of the besieger camp", "siegeEvent.BesiegerCamp.LeaderParty"),
            },
            subjectFields: S("settlement", "attacker"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyJoinedSiege", "siege",
            "A party joined a siege camp: the player or one of their clan's parties arriving at the siege works, or any AI besieger reinforcing one.",
            "OnMobilePartyJoinedToSiegeEventEvent",
            fields: new[] { F("party", FieldKind.Party, "the party that joined the siege camp", "party") },
            subjectFields: S("party"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyLeftSiege", "siege",
            "A party left the siege camp it was besieging: the player riding off, or the whole camp breaking up at once when the siege is lost.",
            "OnMobilePartyLeftSiegeEventEvent",
            fields: new[] { F("party", FieldKind.Party, "the party that left the siege camp", "party") },
            subjectFields: S("party"), defaultSubject: Subjects.PlayerClan),

        // ---- raids and forced levies: the completion hook carries the winner side ----

        new TriggerDefinition("villageRaidEnded", "raids",
            "A raid on a village ended with a decided winner; the village is looted when the attackers won, and the loot and casualties are already committed.",
            "RaidCompletedEvent",
            // raidEvent is the raid component; its MapEvent carries the battle (Q23), the direct winnerSide arg is added on top.
            fields: MapEventFields("raidEvent.MapEvent").Concat(new[]
            {
                F("winnerSide", FieldKind.Text, "the game's BattleSideEnum winner of the raid battle: Attacker, Defender or None", "winnerSide"),
            }).ToArray(),
            subjectFields: S("settlement", "attacker", "defender"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("volunteersForced", "raids",
            "A forced levying of men at a settlement ended; when the attackers won the men are already taken, otherwise the levy was driven off.",
            "ForceVolunteersCompletedEvent",
            fields: MapEventFields("forceVolunteersEvent.MapEvent").Concat(new[]
            {
                F("winnerSide", FieldKind.Text, "the game's BattleSideEnum winner of the fight over the levy: Attacker, Defender or None", "winnerSide"),
            }).ToArray(),
            subjectFields: S("settlement", "attacker", "defender"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("suppliesForced", "raids",
            "A forced collection of supplies at a settlement ended; when the attackers won the supplies are already taken.",
            "ForceSuppliesCompletedEvent",
            fields: MapEventFields("forceSuppliesEvent.MapEvent").Concat(new[]
            {
                F("winnerSide", FieldKind.Text, "the game's BattleSideEnum winner of the fight over the supplies: Attacker, Defender or None", "winnerSide"),
            }).ToArray(),
            subjectFields: S("settlement", "attacker", "defender"), defaultSubject: Subjects.PlayerClan),

        // ---- prisoners: the roster arguments are batch data with no party on them (Q36 rows), so they are not exposed ----

        new TriggerDefinition("prisonersTaken", "prisoners",
            "The player's party took a batch of common captives and the party screen was closed (hero captives are reported by heroCaptured).",
            "OnPrisonerTakenEvent",
            defaultSubject: Subjects.Any),
        new TriggerDefinition("prisonersReleased", "prisoners",
            "The player deliberately released a batch of common captives; releasing for a ransom or a peace deal is not reported here.",
            "OnPrisonerReleasedEvent",
            defaultSubject: Subjects.Any),
        new TriggerDefinition("prisonersRecruited", "prisoners",
            "The player's party filled its own ranks with captives (they were prisoners before this moment).",
            "OnMainPartyPrisonerRecruitedEvent",
            defaultSubject: Subjects.Any),
        new TriggerDefinition("prisonersSold", "prisoners",
            "A party sold its captives to another party; selling to a tavern or a market has no buyer party.",
            "OnPrisonerSoldEvent",
            fields: new[]
            {
                F("seller", FieldKind.Party, "the party that had the captives", "sellerParty"),
                F("buyer", FieldKind.Party, "the party that bought them; empty when they went to a tavern or a market", "buyerParty"),
            },
            subjectFields: S("seller"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("prisonersDonated", "prisoners",
            "Captives were handed over to a settlement's dungeon: the player donating prisoners, captives sold to same-faction lords, or the captives of a party that disbanded.",
            "OnPrisonerDonatedToSettlementEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement whose dungeon took the captives", "settlement"),
                F("party", FieldKind.Party, "the party the transfer is tied to: the receiving party for a player donate, the seller for a sell, the disbanding party", "party"),
            },
            subjectFields: S("settlement", "party"), defaultSubject: Subjects.PlayerClan),

        // ---- loot after a field battle ----

        new TriggerDefinition("lootDistributed", "battle",
            "A winning party received its share of the defeated party's loot; the player's own share is booked here, before the loot screen opens.",
            "OnLootDistributedToPartyEvent",
            fields: new[]
            {
                F("winner", FieldKind.Party, "the party that received the loot", "winnerParty"),
                F("defeated", FieldKind.Party, "the defeated party the loot came from", "defeatedParty"),
            },
            subjectFields: S("winner", "defeated"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("lootCollected", "battle",
            "A winner party's whole loot pile from the defeated party was collected; for the player this fires just before the loot screen hands the items over.",
            "OnCollectLootsItemsEvent",
            fields: new[] { F("winner", FieldKind.Party, "the party that collected the loot", "winnerParty") },
            subjectFields: S("winner"), defaultSubject: Subjects.PlayerClan),

        // ---- hideouts and desertion ----

        new TriggerDefinition("hideoutSpotted", "crime",
            "A hideout appeared on the campaign map because a party discovered it; in the vanilla raisers that party is always the player's.",
            "OnHideoutSpottedEvent",
            fields: new[]
            {
                F("spotter", FieldKind.Party, "the party that found it (the player's main party in the vanilla raisers)", "spotterParty"),
                F("hideout", FieldKind.Settlement, "the hideout settlement that is now visible", "hideoutParty.Settlement"),
            },
            subjectFields: S("spotter", "hideout"), defaultSubject: Subjects.PlayerParty),
        new TriggerDefinition("hideoutEmptied", "crime",
            "A hideout went empty and was removed from the map: its last band left or was destroyed. The event does not say who cleared it.",
            "OnHideoutDeactivatedEvent",
            fields: new[] { F("settlement", FieldKind.Settlement, "the hideout settlement that went empty", "hideoutSettlement") },
            subjectFields: S("settlement"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("troopsDeserted", "party",
            "Men deserted a party overnight (low morale or unpaid wages); they are already gone from its roster.",
            "OnTroopsDesertedEvent",
            fields: new[] { F("party", FieldKind.Party, "the party that lost men", "party") },
            subjectFields: S("party"), defaultSubject: Subjects.PlayerClan),
    };
}
