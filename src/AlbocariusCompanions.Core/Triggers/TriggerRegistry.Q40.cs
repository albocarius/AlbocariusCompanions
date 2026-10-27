using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q40 triggers (orchestra/triggers.tsv rows with q = Q40).</summary>
    private static IEnumerable<TriggerDefinition> Q40() => new TriggerDefinition[]
    {
        // Every Q40 raise site is campaign code — KingdomManager / AllianceCampaignBehavior / the game Actions /
        // Clan / Hero / CharacterCreationManager — never a Mission* or Agent* class, so no trigger in this batch
        // is inMission. None of these events carries a pure UI flag.

        // ---- kingdom, war and peace ----

        new TriggerDefinition("kingdomCreated", "kingdom",
            "A new kingdom was founded somewhere in the world — a clan raised its banner; the player clan founding its own realm is the special case, and the outcome (ruling clan, its clans) is already set when this fires.",
            "KingdomCreatedEvent",
            fields: new[] { F("kingdom", FieldKind.Kingdom, "the new kingdom", "kingdom") },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("tradeAgreementSigned", "kingdom",
            "Two kingdoms signed a trade agreement with each other — kingdom news bark.",
            "OnTradeAgreementSignedEvent",
            fields: new[]
            {
                F("firstKingdom", FieldKind.Kingdom, "one kingdom of the pair", "kingdom1"),
                F("secondKingdom", FieldKind.Kingdom, "the other kingdom of the pair", "kingdom2"),
            },
            subjectFields: S("firstKingdom", "secondKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("allianceStarted", "kingdom",
            "Two kingdoms became allies — the receiving kingdom has already accepted, and wars against their enemies follow right after.",
            "OnAllianceStartedEvent",
            fields: new[]
            {
                F("proposerKingdom", FieldKind.Kingdom, "the kingdom that proposed the alliance", "proposerKingdom"),
                F("receiverKingdom", FieldKind.Kingdom, "the kingdom that accepted it", "receiverKingdom"),
            },
            subjectFields: S("proposerKingdom", "receiverKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("allianceEnded", "kingdom",
            "The alliance between two kingdoms ended — it expired, the two allies went to war with each other, or one of the kingdoms was destroyed.",
            "OnAllianceEndedEvent",
            fields: new[]
            {
                F("firstKingdom", FieldKind.Kingdom, "one kingdom of the pair", "kingdom1"),
                F("secondKingdom", FieldKind.Kingdom, "the other kingdom of the pair", "kingdom2"),
            },
            subjectFields: S("firstKingdom", "secondKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("callToWarStarted", "kingdom",
            "One kingdom asked its ally to join a war against `targetKingdom` — the agreement is made but the war is not declared yet; when `calledKingdom` is the player's realm it is being dragged in.",
            "OnCallToWarAgreementStartedEvent",
            fields: new[]
            {
                F("callingKingdom", FieldKind.Kingdom, "the kingdom asking its ally to join the war", "callingKingdom"),
                F("calledKingdom", FieldKind.Kingdom, "the ally being asked", "calledKingdom"),
                F("targetKingdom", FieldKind.Kingdom, "the kingdom the war will be declared on", "kingdomToCallToWarAgainst"),
            },
            subjectFields: S("callingKingdom", "calledKingdom", "targetKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("callToWarEnded", "kingdom",
            "A call-to-war agreement ended — usually straight after the war was declared, later by expiry, by peace, or when the alliance behind it ended.",
            "OnCallToWarAgreementEndedEvent",
            fields: new[]
            {
                F("callingKingdom", FieldKind.Kingdom, "the kingdom that had asked", "callingKingdom"),
                F("calledKingdom", FieldKind.Kingdom, "the ally that had been asked", "calledKingdom"),
                F("targetKingdom", FieldKind.Kingdom, "the kingdom the call to war was against", "kingdomToCallToWarAgainst"),
            },
            subjectFields: S("callingKingdom", "calledKingdom", "targetKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("tributeEarned", "kingdom",
            "A clan received gold tribute from a faction it is at war with — fires for every clan in the daily income pass, so gate on the player clan for a purse bark.",
            "OnClanEarnedGoldFromTributeEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the clan that got the gold", "clan"),
                F("payer", FieldKind.Faction, "the clan or kingdom that paid the tribute", "payingFaction"),
            },
            subjectFields: S("clan", "payer"), defaultSubject: Subjects.Player),

        // ---- settlements and fiefs ----

        new TriggerDefinition("settlementOwnerChanged", "settlements",
            "A settlement changed hands — taken by siege, or handed over by barter, gift, a king's decision, rebellion, or the old owner's clan being destroyed or leaving its faction.",
            "OnSettlementOwnerChangedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the settlement that changed owner", "settlement"),
                F("openToClaim", FieldKind.Bool, "true when the fortification is now open to claim — the game sets it for changes by siege, clan destruction or the old clan leaving its faction", "openToClaim"),
                F("newOwner", FieldKind.Hero, "the hero of the clan that now owns it; empty when the fief went unassigned", "newOwner"),
                F("oldOwner", FieldKind.Hero, "the hero of the clan that lost it", "oldOwner"),
                F("capturer", FieldKind.Hero, "the hero who took it by force; empty for peaceful transfers", "capturerHero"),
                F("detail", FieldKind.Text, "the game's ChangeOwnerOfSettlementDetail: Default, BySiege, ByBarter, ByLeaveFaction, ByKingDecision, ByGift, ByRebellion, ByClanDestruction", "detail"),
            },
            subjectFields: S("settlement", "newOwner", "oldOwner", "capturer"), defaultSubject: Subjects.PlayerClan,
            common: true),
        new TriggerDefinition("governorChanged", "settlements",
            "A town's governor changed — appointed, given up or replaced; on a removal `newGovernor` is empty, and an appointment is followed immediately by `heroGotBusy` with reason BecomeGovernor.",
            "OnGovernorChangedEvent",
            fields: new[]
            {
                F("settlement", FieldKind.Settlement, "the town or castle whose governor changed", "town.Settlement"),
                F("oldGovernor", FieldKind.Hero, "the governor before; empty on a first appointment", "oldGovernor"),
                F("newGovernor", FieldKind.Hero, "the new governor; empty when the governor was only removed", "newGovernor"),
            },
            subjectFields: S("settlement", "oldGovernor", "newGovernor"), defaultSubject: Subjects.PlayerClan),

        // ---- clan service (mercenary / vassal offers) ----

        new TriggerDefinition("serviceOfferCancelled", "kingdom",
            "A kingdom's offer to take the player's clan into mercenary or vassal service disappeared before the player decided — it expired or died from war, captivity or a changed allegiance; clicking Decline fires nothing.",
            "OnVassalOrMercenaryServiceOfferCanceledEvent",
            fields: new[] { F("kingdom", FieldKind.Kingdom, "the kingdom that withdrew the offer", "offeredKingdom") },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("mercenaryServiceStarted", "kingdom",
            "A clan went into mercenary service under a kingdom — for the player clan, right after accepting a hire offer; the new kingdom is readable live from the clan.",
            "OnMercenaryServiceStartedEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the clan that entered mercenary service", "clan"),
                F("detail", FieldKind.Text, "the game's StartMercenaryServiceActionDetail — only ApplyByDefault in 1.4.8", "details"),
            },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),
        new TriggerDefinition("mercenaryServiceEnded", "kingdom",
            "A clan's mercenary service ended — it left the kingdom or became a full vassal; for the player clan the hired-sword arc is over.",
            "OnMercenaryServiceEndedEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the clan that left mercenary service", "clan"),
                F("detail", FieldKind.Text, "the game's EndMercenaryServiceActionDetail: ApplyByDefault, ApplyByLeavingKingdom, ApplyByBecomingVassal", "details"),
            },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),

        // ---- family (marriage offers, children, heirs) ----

        new TriggerDefinition("marriageOfferCancelled", "family",
            "A marriage offer to the player's kin was withdrawn — refused, expired, the clans made war, or one of the two was captured or killed; the offer banner vanishes when the game removes it.",
            "OnMarriageOfferCanceledEvent",
            fields: new[]
            {
                F("suitor", FieldKind.Hero, "the hero who had offered marriage", "suitor"),
                F("maiden", FieldKind.Hero, "the hero who had been offered marriage", "maiden"),
            },
            subjectFields: S("suitor", "maiden"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("childConceived", "family",
            "A hero conceived a child — fires for every couple in the world, so gate on the player clan; `childBorn` follows roughly nine in-game months later.",
            "OnChildConceivedEvent",
            fields: new[] { F("mother", FieldKind.Hero, "the mother of the unborn child", "mother") },
            subjectFields: S("mother"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("childBorn", "family",
            "A hero gave birth — the in-game birth of newborn children, the player's own family and companions included; the mother can be killed in childbirth right afterwards.",
            "OnGivenBirthEvent",
            // aliveChildren (List<Hero>, the newborn heroes) is left out: no GAME_MAP member names a simple value of it.
            fields: new[]
            {
                F("mother", FieldKind.Hero, "the hero who gave birth", "mother"),
                F("stillbornCount", FieldKind.Int, "how many of the litter were stillborn; usually 0", "stillbornCount"),
            },
            subjectFields: S("mother"), defaultSubject: Subjects.PlayerClan,
            common: true),
        new TriggerDefinition("heirSelectionRequested", "clan",
            "The player character just died and the game is asking which clan member takes over — the heir is not chosen yet; the candidate list is not exposed, so pair this with `heirSelected`.",
            "OnHeirSelectionRequestedEvent",
            // candidateHeirs (Dictionary<Hero,int>) is left out: no GAME_MAP member names a simple value of it.
            // No usable entity field: only "any" can match.
            defaultSubject: Subjects.Any),
        new TriggerDefinition("heirSelected", "clan",
            "The new heir of the player clan was chosen after a death — the player character switches right afterwards.",
            "OnHeirSelectionOverEvent",
            fields: new[] { F("heir", FieldKind.Hero, "the hero picked as the new heir", "selectedHeir") },
            subjectFields: S("heir"), defaultSubject: Subjects.PlayerClan),

        // ---- player character changes ----

        new TriggerDefinition("playerCharacterAboutToChange", "player",
            "The hero the player controls is about to change — fires before the old hero hands over and while they can still be read.",
            "OnBeforePlayerCharacterChangedEvent",
            fields: new[]
            {
                F("oldPlayer", FieldKind.Hero, "the hero played until now", "oldPlayer"),
                F("newPlayer", FieldKind.Hero, "the hero played from now on", "newPlayer"),
            },
            subjectFields: S("oldPlayer", "newPlayer"), defaultSubject: Subjects.Player),
        new TriggerDefinition("playerCharacterChanged", "player",
            "The player now controls a different hero — `heirSelected` and `clanLeaderChanged` came before; greetings for the new protagonist belong here.",
            "OnPlayerCharacterChangedEvent",
            fields: new[]
            {
                F("oldPlayer", FieldKind.Hero, "the hero played until now", "oldPlayer"),
                F("newPlayer", FieldKind.Hero, "the hero played from now on", "newPlayer"),
                F("party", FieldKind.Party, "the party the new player hero commands", "newPlayerParty"),
                F("mainPartyChanged", FieldKind.Bool, "whether the main party itself changed hands", "isMainPartyChanged"),
            },
            subjectFields: S("newPlayer", "oldPlayer", "party"), defaultSubject: Subjects.Player),
        new TriggerDefinition("clanLeaderChanged", "clan",
            "A clan got a new leader — succession after a death, the player-chosen heir, or a new clan lord; `newLeader` is already the leader when this fires.",
            "OnClanLeaderChangedEvent",
            fields: new[]
            {
                F("oldLeader", FieldKind.Hero, "the leader before the change; empty on a brand-new clan", "oldLeader"),
                F("newLeader", FieldKind.Hero, "the new clan leader", "newLeader"),
            },
            subjectFields: S("oldLeader", "newLeader"), defaultSubject: Subjects.PlayerClan),

        // ---- clan (destruction, membership, influence, companions) ----

        new TriggerDefinition("clanDestroyed", "clan",
            "A clan was destroyed — its parties, heroes and fiefs are about to be wiped; when the player's clan is destroyed the campaign is effectively over.",
            "OnClanDestroyedEvent",
            fields: new[] { F("clan", FieldKind.Clan, "the clan being destroyed; fully readable while this fires", "clan") },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),
        new TriggerDefinition("heroChangedClan", "clan",
            "A lord-type hero joined or left a clan — compare `oldClan` and `newClan` for the direction; companions use `companionAdded` / `companionRemoved` instead.",
            "OnHeroChangedClanEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who moved", "hero"),
                F("oldClan", FieldKind.Clan, "the clan the hero left", "oldClan"),
                F("newClan", FieldKind.Clan, "the clan the hero joined, read live from the hero", "hero.Clan"),
            },
            subjectFields: S("hero", "newClan", "oldClan"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("clanInfluenceChanged", "clan",
            "A clan's influence moved — fires for every clan and every trickle, so gate on the player clan and put a `where` threshold on `change`.",
            "OnClanInfluenceChangedEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the clan whose influence moved", "clan"),
                F("change", FieldKind.Number, "influence gained (positive) or lost (negative)", "amount"),
            },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),
        new TriggerDefinition("companionRemoved", "clan",
            "A companion left the player clan — dismissed, killed, finished by a quest, or turned into a lord; the hero is already off the roster when this fires, so say the goodbye lines here.",
            "CompanionRemoved",
            fields: new[]
            {
                F("companion", FieldKind.Hero, "the companion who left", "companion"),
                F("detail", FieldKind.Text, "the game's RemoveCompanionDetail: Fire, Death, AfterQuest, ByTurningToLord", "detail"),
            },
            subjectFields: S("companion"), defaultSubject: Subjects.PlayerClan),

        // ---- hero (busy roles, food, teleport) ----

        new TriggerDefinition("heroGotBusy", "heroes",
            "A hero took up a task or role — becoming a governor, emissary, caravan or alley leader, being handed an issue, or starting to travel; the Traveling value fires very often, so filter on `reason`.",
            "OnHeroGetsBusyEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who became busy", "hero"),
                F("reason", FieldKind.Text, "the game's HeroGetsBusyReasons: BecomeGovernor, BecomeEmissary, BecomeCaravanLeader, BecomeAlleyLeader, SolvesIssue, Traveling", "reason"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroSharedFood", "heroes",
            "A better-fed hero gave rations to a starving hero of the same army and gained influence for it — gratitude or indignation lines when the player's army is involved.",
            "OnHeroSharedFoodWithAnotherHeroEvent",
            fields: new[]
            {
                F("supporter", FieldKind.Hero, "the hero who gave the food", "supporter"),
                F("supported", FieldKind.Hero, "the hungry hero who got the food", "supported"),
                F("influence", FieldKind.Number, "the influence the giver earned", "influence"),
            },
            subjectFields: S("supporter", "supported"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroTeleportRequested", "heroes",
            "A hero is about to be teleported on the campaign map — it fires before the move and can still be refused by the game; the typical case is a companion re-placed after being garrisoned or released.",
            "OnHeroTeleportationRequestedEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who will be teleported", "hero"),
                F("settlement", FieldKind.Settlement, "the settlement teleported to; empty when the target is a party", "targetSettlement"),
                F("party", FieldKind.Party, "the party teleported to; empty when the target is a settlement", "targetParty"),
                F("detail", FieldKind.Text, "the game's TeleportationDetail — immediate or delayed, to a settlement or a party (as governor / party leader)", "detail"),
            },
            subjectFields: S("hero", "settlement", "party"), defaultSubject: Subjects.PlayerClan),
    };
}
