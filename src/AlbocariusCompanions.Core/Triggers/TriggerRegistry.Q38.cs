using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q38 triggers (orchestra/triggers.tsv rows with q = Q38).</summary>
    private static IEnumerable<TriggerDefinition> Q38() => new TriggerDefinition[]
    {
        // Every raise site in Q38 is campaign/map/menu code (Army and MobileParty internals, the settlement garrison
        // actions, the daily starvation tick, the game-menu overlay, TournamentCampaignBehavior's tournament menu),
        // so none of these is inMission — the tournament and talk triggers fire from menus, not inside a mission.

        // ---- party lifecycle & command ----

        new TriggerDefinition("partyJoinedArmy", "party",
            "A party joined an army somewhere in the world — the player's party joined a host, or a clan's war party went off to war; it is already a member when this fires.",
            "OnPartyJoinedArmyEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party that joined the army", "party"),
                F("clan", FieldKind.Clan, "the clan owning that party", "party.ActualClan"),
            },
            subjectFields: S("party", "clan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyLeftArmy", "party",
            "A party stopped being part of an army — it left on its own, the leader dismissed it, or it was destroyed; it is already gone from the army when this fires.",
            "PartyRemovedFromArmyEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party that left the army", "party"),
                F("clan", FieldKind.Clan, "the clan owning that party", "party.ActualClan"),
            },
            subjectFields: S("party", "clan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyLeaderChanged", "party",
            "A party got a new leader hero — a companion took over a party or lost one; `oldLeader` is the hero who gave it up. On load, every re-assigned party leader fires at once, so skip that first wave.",
            "OnPartyLeaderChangedEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party that changed leader", "party"),
                F("newLeader", FieldKind.Hero, "the hero leading it now", "party.LeaderHero"),
                F("oldLeader", FieldKind.Hero, "the hero who just stepped down; empty on first assignment", "oldLeader"),
                F("clan", FieldKind.Clan, "the clan owning the party", "party.ActualClan"),
            },
            subjectFields: S("party", "newLeader", "oldLeader", "clan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyDisbandStarted", "party",
            "A party was told to disband: over the following days it walks to the nearest friendly fortification and melts away; its roster is still intact here.",
            "OnPartyDisbandStartedEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party that began disbanding", "party"),
                F("clan", FieldKind.Clan, "the clan owning that party", "party.ActualClan"),
            },
            subjectFields: S("party", "clan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyDisbanded", "party",
            "The disbanding party reached a friendly fortification and is about to be removed from the map — its full roster and leader hero can still be read right here, the last reaction slot before the end.",
            "OnPartyDisbandedEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party being disbanded, roster still readable", "party"),
                F("settlement", FieldKind.Settlement, "the fortification where it melts away", "settlement"),
                F("clan", FieldKind.Clan, "the clan that owned the party; empty for clanless parties", "party.ActualClan"),
                F("leader", FieldKind.Hero, "the party's leader hero — the companion whose party just ended, if it was a companion's", "party.LeaderHero"),
            },
            subjectFields: S("party", "settlement", "clan", "leader"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("partyVisibilityChanged", "party",
            "A party became visible or invisible on the campaign map (spotted, fog of war, going to ground). At game start every party fires once — ignore that wave.",
            "PartyVisibilityChangedEvent",
            fields: new[]
            {
                F("party", FieldKind.Party, "the party whose visibility changed; a settlement's garrison party can come through here too", "party"),
                F("visible", FieldKind.Bool, "whether the party can be seen on the map now", "party.IsVisible"),
            },
            subjectFields: S("party"), defaultSubject: Subjects.PlayerClan),

        // ---- troops: recruiting, upgrading, garrison gifts ----

        new TriggerDefinition("playerRecruitedTroops", "party",
            "The player recruited `number` men of `troop` into their party; fires once per troop type per batch, so one hiring spree fires many times.",
            "OnUnitRecruitedEvent",
            fields: new[]
            {
                F("troop", FieldKind.Text, "string id of the recruited troop type", "troop.StringId"),
                F("number", FieldKind.Int, "how many of that troop type came in this unit batch", "number"),
            },
            defaultSubject: Subjects.Any),
        new TriggerDefinition("heroRecruitedTroops", "party",
            "A hero recruited troops for their party somewhere in the world — keep the subject filter (player, companion) or this fires for every AI lord's hiring too.",
            "OnTroopRecruitedEvent",
            fields: new[]
            {
                F("recruiter", FieldKind.Hero, "the hero who recruited", "recruiter"),
                F("settlement", FieldKind.Settlement, "the settlement of the recruitment; empty for on-map mercenary recruiting", "settlement"),
                F("source", FieldKind.Hero, "the hero the men came from (volunteers out of a lord's retinue); empty when none", "recruitmentSource"),
                F("troop", FieldKind.Text, "string id of the recruited troop type", "troop.StringId"),
                F("number", FieldKind.Int, "how many of that troop type in this batch", "number"),
            },
            subjectFields: S("recruiter", "settlement", "source"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("troopsUpgraded", "party",
            "The player upgraded `number` men from `oldTroop` to `newTroop` on the party screen; the whole batch lands when Done is clicked, nothing fires for AI upgrades.",
            "PlayerUpgradedTroopsEvent",
            fields: new[]
            {
                F("oldTroop", FieldKind.Text, "string id of the troop type the men came from", "oldTroop.StringId"),
                F("newTroop", FieldKind.Text, "string id of the troop type they became", "newTroop.StringId"),
                F("number", FieldKind.Int, "how many men were upgraded", "number"),
            },
            defaultSubject: Subjects.Any),
        new TriggerDefinition("troopsGivenToSettlement", "party",
            "A party left men behind as garrison when it entered a friendly fortification outside the player clan — typically a companion's garrison contribution; the lone main party never fires this.",
            "OnTroopGivenToSettlementEvent",
            fields: new[]
            {
                F("giver", FieldKind.Hero, "the hero who gave the troops; the event only carries the hero side of the giving party", "giver"),
                F("settlement", FieldKind.Settlement, "the settlement that received the garrison", "settlement"),
            },
            subjectFields: S("giver", "settlement"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("playerPartyStarving", "party",
            "The player's party is still without food another day — fires once per day from the second day of a starvation streak on, right after the daily starvation morale hit.",
            "OnMainPartyStarvingEvent",
            defaultSubject: Subjects.Any),

        // ---- player menu actions ----

        new TriggerDefinition("playerStartedTalkFromMenu", "scenes",
            "The player started a conversation with `hero` from the map or a location menu — hook for a companion to react when addressed directly or when the player talks to someone nearby.",
            "PlayerStartTalkFromMenu",
            fields: new[] { F("hero", FieldKind.Hero, "the hero the player is about to talk to", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("playerJoinedTournament", "tournaments",
            "The player entered the tournament in `town` from its menu; `isParticipant` is true when they compete themselves, false when they only start spectating — no bracket has run yet.",
            "OnPlayerJoinedTournamentEvent",
            fields: new[]
            {
                F("town", FieldKind.Settlement, "the town holding the tournament", "town.Settlement"),
                F("isParticipant", FieldKind.Bool, "true = the player signed up to compete; false = the player began watching", "isParticipant"),
            },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan),
    };
}
