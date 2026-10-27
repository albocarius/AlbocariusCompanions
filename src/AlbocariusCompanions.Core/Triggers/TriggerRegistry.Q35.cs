using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q35 triggers (orchestra/triggers.tsv rows with q = Q35).</summary>
    private static IEnumerable<TriggerDefinition> Q35() => new TriggerDefinition[]
    {
        new TriggerDefinition("playerBattleEnded", "battle",
            "A battle the player fought in ended (winner known; casualties and loot not yet counted).",
            "OnPlayerBattleEndEvent",
            fields: MapEventFields("mapEvent"),
            // Always the player's battle: "any" is the natural default and matches every time.
            defaultSubject: Subjects.Any, common: true),
        new TriggerDefinition("heroKilled", "heroes", "A hero died (battle, execution, old age, …).", "HeroKilledEvent",
            fields: new[]
            {
                F("victim", FieldKind.Hero, "the hero who died", "victim"),
                F("killer", FieldKind.Hero, "who killed them; empty for natural deaths", "killer"),
                F("detail", FieldKind.Text, "the game's KillCharacterActionDetail: DiedInBattle, Executed, DiedOfOldAge, …", "detail"),
            },
            subjectFields: S("victim"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("villageLooted", "raids", "A village raid finished and the village is now looted.", "VillageLooted",
            fields: new[] { F("village", FieldKind.Settlement, "the looted village", "village.Settlement") },
            subjectFields: S("village"), defaultSubject: Subjects.PlayerClan, common: true),

        // ---- rest of the Q35 batch, in GAME_MAP row order (the three above were added first) ----

        new TriggerDefinition("heroWounded", "battle",
            "A hero's health dropped into the wounded state (companion knocked out in a fight; there is no heal event).", "HeroWounded",
            fields: new[] { F("hero", FieldKind.Hero, "the hero who became wounded", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan, common: true,
            inMission: true), // knock-out happens inside the battle: Q23 raise path PartyAgentOrigin.SetWounded
        new TriggerDefinition("battleStarted", "battle",
            "A single battle on an engagement started on the map; sides and place known, no outcome yet.", "BattleStarted",
            fields: new[]
            {
                F("attacker", FieldKind.Party, "the attacking party", "attacker"),
                F("defender", FieldKind.Party, "the defending party", "defender"),
                F("settlement", FieldKind.Settlement, "the settlement the battle is fought for; empty in the open field", "subject"),
            },
            subjectFields: S("attacker", "defender", "settlement"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("mapEventStarted", "battle",
            "A battle, raid or siege started anywhere in the world, before any fighting.", "MapEventStarted",
            fields: MapEventFields("mapEvent"), // the direct attacker/defender args are the MapEvent side leader parties = same fields
            subjectFields: S("settlement", "attacker", "defender"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("mapEventEnded", "battle",
            "A battle, raid or siege anywhere in the world ended, after its casualties, prisoners and loot were committed.", "MapEventEnded",
            fields: MapEventFields("mapEvent"),
            subjectFields: S("settlement", "attacker", "defender"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("playerDesertedBattle", "battle",
            "The player abandoned a battle in progress, leaving men behind.", "PlayerDesertedBattleEvent",
            fields: new[] { F("menLeft", FieldKind.Int, "the number of men left behind to escape", "sacrificedMenCount") },
            // player-only event with no entity field to match; "any" matches every firing.
            defaultSubject: Subjects.Any),
        new TriggerDefinition("heroHitInCombat", "battle",
            "A hero landed one blow during a battle — fires for every hero blow in every battle (very chatty; gate on `fatal`).", "OnHeroCombatHitEvent",
            fields: new[]
            {
                F("attacker", FieldKind.Hero, "the hero who landed the blow", "attacker.HeroObject"),
                F("victim", FieldKind.Hero, "the hero hit; empty when a common troop took the blow", "victim.HeroObject"),
                F("attackerParty", FieldKind.Party, "the side the attacker fights on", "attackerParty"),
                F("fatal", FieldKind.Bool, "the blow was lethal", "isFatal"),
                F("xp", FieldKind.Int, "the experience the game awarded for the blow", "xp"),
            },
            subjectFields: S("victim", "attacker", "attackerParty"), defaultSubject: Subjects.PlayerClan,
            inMission: true), // live blows run through the battle's agent origins (PartyGroupAgentOrigin)
        new TriggerDefinition("siegeStarted", "battle",
            "A siege began on the map, before it runs.", "OnSiegeEventStartedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement being besieged", "siegeEvent.BesiegedSettlement"),
                F("attacker", FieldKind.Party, "leading party of the besiegers", "siegeEvent.BesiegerCamp.LeaderParty"),
            },
            subjectFields: S("settlement", "attacker"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("playerJoinedSiege", "battle",
            "The player entered a siege battle, simulated or real.", "OnPlayerSiegeStartedEvent",
            // no arguments and always the player: "any" matches every firing.
            defaultSubject: Subjects.Any),
        new TriggerDefinition("crimeRatingChanged", "crime",
            "The player's crime rating with a faction changed (`delta` > 0 = a new crime; < 0 = daily decay, a fine or peace).", "CrimeRatingChanged",
            fields: new[]
            {
                F("faction", FieldKind.Faction, "the kingdom or clan whose crime rating with the player changed", "faction"),
                F("delta", FieldKind.Number, "the change in the rating", "deltaCrimeAmount"),
            },
            subjectFields: S("faction"), defaultSubject: Subjects.Player),
        new TriggerDefinition("heroBecameFugitive", "crime",
            "A hero became a fugitive (a dismissed or released companion now wanders on its own).", "CharacterBecameFugitiveEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the hero who went fugitive", "fugitive") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("banditPartyRecruited", "crime",
            "The player's party absorbed a defeated bandit party just before it is destroyed.", "BanditPartyRecruited",
            fields: new[] { F("party", FieldKind.Party, "the bandit party whose men joined the player", "banditParty") },
            // always about the player, but the bandit party belongs to no player subject — only "any" can match.
            defaultSubject: Subjects.Any),
        new TriggerDefinition("trackSpotted", "party",
            "The player's party spotted a fresh track on the map.", "TrackDetectedEvent",
            fields: new[]
            {
                F("partyName", FieldKind.Text, "display name of the party that left the track", "track.PartyName"),
                F("partyType", FieldKind.Text, "the game's PartyTypeEnum of the tracked party (Bandit, Caravan, …)", "track.PartyTypeEnum"),
                F("men", FieldKind.Int, "the tracked party's total strength", "track.NumberOfAllMembers"),
                F("isEnemy", FieldKind.Bool, "the track belongs to a hostile party", "track.IsEnemy"),
            },
            // a Track is a snapshot with no reference to the tracked party — no entity field, so only "any".
            defaultSubject: Subjects.Any),
        new TriggerDefinition("trackLost", "party",
            "A track the player had spotted expired and faded from the map.", "TrackLostEvent",
            fields: new[]
            {
                F("partyName", FieldKind.Text, "display name of the party that left the track", "track.PartyName"),
                F("partyType", FieldKind.Text, "the game's PartyTypeEnum of the tracked party (Bandit, Caravan, …)", "track.PartyTypeEnum"),
                F("men", FieldKind.Int, "the tracked party's total strength", "track.NumberOfAllMembers"),
                F("isEnemy", FieldKind.Bool, "the track belongs to a hostile party", "track.IsEnemy"),
            },
            defaultSubject: Subjects.Any),
        new TriggerDefinition("heroCaptured", "prisoners",
            "A hero was taken prisoner anywhere in the world (by the player or by anyone else).", "HeroPrisonerTaken",
            fields: new[]
            {
                F("prisoner", FieldKind.Hero, "the hero taken captive", "prisoner"),
                F("captor", FieldKind.Party, "the party that took them", "capturerParty"),
            },
            subjectFields: S("prisoner", "captor"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("heroReleased", "prisoners",
            "A captive hero was set free (`detail` says how); a freed player companion goes fugitive at the same moment.", "HeroPrisonerReleased",
            fields: new[]
            {
                F("prisoner", FieldKind.Hero, "the released hero", "prisoner"),
                F("formerCaptor", FieldKind.Party, "the party that was holding them", "formerCaptorParty"),
                F("formerCaptorFaction", FieldKind.Faction, "the faction that was holding them", "capturerFaction"),
                F("detail", FieldKind.Text, "the game's EndCaptivityDetail: Ransom, ReleasedAfterPeace, ReleasedAfterBattle, " +
                    "ReleasedAfterEscape, ReleasedByChoice, Death, ReleasedByCompensation", "detail"),
            },
            subjectFields: S("prisoner", "formerCaptor", "formerCaptorFaction"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("prisonersChangedInSettlement", "prisoners",
            "The prisoners of a settlement's dungeon changed; the event does not say who moved them.", "PrisonersChangeInSettlement",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement whose dungeon changed", "settlement"),
                F("prisoner", FieldKind.Hero, "the single hero moved; empty when a whole prison roster moved", "prisonerHero"),
                F("takenFromDungeon", FieldKind.Bool, "true: prisoners left the dungeon; false: prisoners were added", "takenFromDungeon"),
            },
            subjectFields: S("settlement", "prisoner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("ransomOfferCancelled", "prisoners",
            "A pending ransom offer fell through (2-day expiry or the notification was ignored; killed/released captives have their own triggers).", "OnRansomOfferCancelledEvent",
            fields: new[] { F("captive", FieldKind.Hero, "the captive the ransom offer concerned", "captiveHero") },
            subjectFields: S("captive"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("villageRaidStarted", "raids",
            "A raid on a village started; it is being burned and looted now.", "VillageBeingRaided",
            fields: new[] { F("village", FieldKind.Settlement, "the village being raided", "village.Settlement") },
            subjectFields: S("village"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("itemsLooted", "raids",
            "A raiding party hauled one batch of goods out of a village; fires many times while the raid loots.", "ItemsLooted",
            fields: new[] { F("looter", FieldKind.Party, "the raiding party", "lootingParty") },
            subjectFields: S("looter"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("townRebellionStateChanged", "raids",
            "A town entered or left its rebellious state — open rebellion only follows if loyalty keeps falling.", "TownRebelliosStateChanged",
            fields: new[]
            {
                F("town", FieldKind.Settlement, "the town whose rebellious state changed", "town.Settlement"),
                F("rebellious", FieldKind.Bool, "true: the town became rebellious; false: it calmed down", "inRebelliousState"),
            },
            subjectFields: S("town"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("rebellionSucceeded", "raids",
            "A rebellion won: the settlement is already owned by the rebels when this fires.", "RebellionFinished",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement that broke away", "settlement"),
                F("oldOwner", FieldKind.Clan, "the clan that lost it", "oldOwnerClan"),
            },
            subjectFields: S("settlement", "oldOwner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroAboutToBeKilled", "heroes",
            "A hero is about to be killed; fires before heroKilled, while their live party and prison state can still be read.", "BeforeHeroKilledEvent",
            fields: new[]
            {
                F("victim", FieldKind.Hero, "the hero about to die", "victim"),
                F("killer", FieldKind.Hero, "who is killing them; empty for natural deaths", "killer"),
                F("detail", FieldKind.Text, "the game's KillCharacterActionDetail (same values as in heroKilled)", "detail"),
            },
            subjectFields: S("victim"), defaultSubject: Subjects.PlayerClan),
    };
}
