using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q39 triggers (orchestra/triggers.tsv rows with q = Q39).</summary>
    private static IEnumerable<TriggerDefinition> Q39() => new TriggerDefinition[]
    {
        // Every Q39 raise site is campaign code — HeroCreator / Hero / HeroDeveloper / the game Actions / campaign
        // behaviors / KingdomElection — never a Mission* or Agent* class, so no trigger in this batch is inMission.
        // Pure UI flags (shouldNotify, showNotification, doNotNotify) are left out of every payload.

        // ---- hero lifecycle (creation, state, leveling, parties) ----

        new TriggerDefinition("heroCreated", "heroes",
            "A brand-new hero entered the world somewhere — a wanderer appearing in a tavern, a newborn, a notable; it also fires for heroes other mods (and this one) spawn, so filter by who was created.",
            "HeroCreated",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the newly created hero", "hero"),
                F("bornNaturally", FieldKind.Bool, "true only for naturally born newborns; false for wanderers and every factory-created hero", "isBornNaturally"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroActivated", "heroes",
            "A hero woke up into the active world; `previousState` says where they came from — `Fugitive` marks a dismissed companion reappearing in a tavern.",
            "OnHeroActivatedEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who became active", "hero"),
                F("previousState", FieldKind.Text, "the game's Hero.CharacterStates they were in before, e.g. Fugitive", "previousState"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroOccupationChanged", "heroes",
            "A hero's occupation changed — the classic case is a companion granted a fief turning into a lord; the payload carries the old value, the new one is on the hero (`hero.occupation`).",
            "HeroOccupationChangedEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who changed occupation", "hero"),
                F("oldOccupation", FieldKind.Text, "the game's Occupation before the change, e.g. Wanderer", "oldOccupation"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroLeveledUp", "heroes",
            "A hero gained a level, after the game handed out the focus points (and one attribute point every fourth level); fires for the player, companions and AI heroes alike.",
            "HeroLevelledUp",
            fields: new[] { F("hero", FieldKind.Hero, "the hero who gained the level", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("heroGainedSkill", "heroes",
            "A hero's skill ticked up one or more whole skill levels — companion skill-up remarks.",
            "HeroGainedSkill",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero whose skill improved", "hero"),
                F("skill", FieldKind.Text, "the game's skill id", "skill.StringId"),
                F("change", FieldKind.Int, "how much the skill rose", "changeAmount"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroJoinedParty", "heroes",
            "A hero joined a party on the map — a just-hired wanderer riding out to their lord, a notable joining a caravan or a garrison turning over; it fires everywhere, not only for the player.",
            "OnHeroJoinedPartyEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who joined", "hero"),
                F("party", FieldKind.Party, "the party they joined", "newParty"),
            },
            subjectFields: S("hero", "party"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("companionAdded", "heroes",
            "The player hired a new companion — when this fires the hero is already in the player's clan and party, so greeting lines can reference them directly.",
            "NewCompanionAdded",
            fields: new[] { F("hero", FieldKind.Hero, "the new companion", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("playerMetHero", "heroes",
            "The player met a hero for the first time — a conversation happened or the encyclopedia revealed them; first-meeting chatter.",
            "OnPlayerMetHeroEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the hero the player met", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),

        // ---- player development (traits, perks) ----

        new TriggerDefinition("playerTraitChanged", "player",
            "One of the player's personality traits crossed a level boundary (only at levels, never for trait XP alone); the five that move at runtime are Mercy, Honor, Valor, Generosity and Calculating.",
            "PlayerTraitChangedEvent",
            fields: new[]
            {
                F("trait", FieldKind.Text, "the game's trait id, e.g. Honor", "trait.StringId"),
                F("previousLevel", FieldKind.Int, "the trait level before (a jump of two levels still fires once)", "previousLevel"),
                F("newLevel", FieldKind.Int, "the trait level now, read live from the player hero", "Hero.MainHero.GetTraitLevel(trait)"),
            },
            // No entity argument at all: only "any" can match.
            defaultSubject: Subjects.Any),
        new TriggerDefinition("perkUnlocked", "player",
            "A hero unlocked a perk — usually the player picking one at a level-up; AI heroes open perks too, so check who it was.",
            "PerkOpenedEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who opened the perk", "hero"),
                F("perk", FieldKind.Text, "the game's perk id", "perk.StringId"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),

        // ---- clan (relations, tier, renown, creation, kingdom membership) ----

        new TriggerDefinition("relationChanged", "clan",
            "The relation between two heroes moved by `change` — a player favor or insult, an emissary mission, a marriage, a new clan leader inheriting the old leader's grudge rows; only the game's own relation actions fire it, direct relation writes do not.",
            "HeroRelationChanged",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero whose relation moved (the game reroutes some changes to clan leaders or relatives)", "effectiveHero"),
                F("other", FieldKind.Hero, "the other hero of the effective pair", "effectiveHeroGainedRelationWith"),
                F("change", FieldKind.Int, "the relation points gained or lost", "relationChange"),
                F("detail", FieldKind.Text, "the game's ChangeRelationDetail: Default or Emissary", "detail"),
                F("actor", FieldKind.Hero, "the hero the change was really aimed at; differs from `hero` when the game rerouted it", "originalHero"),
                F("target", FieldKind.Hero, "the other hero of the original pair", "originalGainedRelationWith"),
            },
            subjectFields: S("hero", "other", "actor", "target"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("clanTierIncreased", "clan",
            "A clan reached a higher tier — the player clan growing is the moment for pride or companion-slot banter.",
            "ClanTierIncrease",
            fields: new[] { F("clan", FieldKind.Clan, "the clan that just gained a tier", "clan") },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),
        new TriggerDefinition("renownGained", "clan",
            "A clan earned renown — `hero` is the hero whose deed brought it (usually the clan leader), `amount` what was gained; the clan is the one to react.",
            "RenownGained",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero whose deed gained the renown, usually the clan leader", "hero"),
                F("amount", FieldKind.Int, "the renown gained", "gainedRenown"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("clanCreated", "clan",
            "A new clan formed — `isCompanion` true means a companion raised to a lord just got a clan of their own; false covers rebel factions, kingdoms struck from within and excommunicated clans.",
            "OnClanCreatedEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the new clan; read its leader and kingdom live", "clan"),
                F("isCompanion", FieldKind.Bool, "true = a promoted companion's clan; false = rebels, breakaways or excommunicated lords", "isCompanion"),
            },
            subjectFields: S("clan"), defaultSubject: Subjects.Player),
        new TriggerDefinition("clanChangedKingdom", "clan",
            "A clan joined or left a kingdom — the player clan going mercenary or vassal, joining as a rebel faction or being absorbed by force all fire it; `detail` says exactly which move it was.",
            "OnClanChangedKingdomEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the clan that changed kingdom", "clan"),
                F("oldKingdom", FieldKind.Kingdom, "the kingdom left; empty when a kingdom is founded", "oldKingdom"),
                F("newKingdom", FieldKind.Kingdom, "the kingdom joined; empty when leaving without joining one", "newKingdom"),
                F("detail", FieldKind.Text, "the game's ChangeKingdomActionDetail: JoinAsMercenary, JoinKingdom, JoinKingdomByDefection, LeaveKingdom, LeaveWithRebellion, LeaveAsMercenary, LeaveByClanDestruction, CreateKingdom, LeaveByKingdomDestruction", "detail"),
            },
            subjectFields: S("clan", "oldKingdom", "newKingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("clanDefected", "clan",
            "A clan defected from one kingdom to another, dragging its parties and fiefs along — the narrower `clanChangedKingdom`; `newKingdom` is empty when it only left.",
            "OnClanDefectedEvent",
            fields: new[]
            {
                F("clan", FieldKind.Clan, "the defecting clan", "clan"),
                F("oldKingdom", FieldKind.Kingdom, "the kingdom it left", "oldKingdom"),
                F("newKingdom", FieldKind.Kingdom, "the kingdom it joined; empty if it just left", "newKingdom"),
            },
            subjectFields: S("clan", "oldKingdom", "newKingdom"), defaultSubject: Subjects.Player),

        // ---- kingdom (council decisions, wars, ruler, destruction) ----

        new TriggerDefinition("kingdomDecisionProposed", "kingdom",
            "A kingdom's council began a vote on a decision — war, peace, exile and the like; the vote only starts here and the decision may still be cancelled, so treat it as news of the plan, not the result.",
            "KingdomDecisionAdded",
            fields: new[]
            {
                F("kingdom", FieldKind.Kingdom, "the kingdom whose council is voting", "decision.Kingdom"),
                F("playerInvolved", FieldKind.Bool, "true when the player is the proposer or sponsor of this vote", "isPlayerInvolved"),
            },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("kingdomDecisionCancelled", "kingdom",
            "A kingdom council vote was cancelled before or during — a participant died, the proposer left the kingdom, or a queued plan went stale; it will never conclude.",
            "KingdomDecisionCancelled",
            fields: new[]
            {
                F("kingdom", FieldKind.Kingdom, "the kingdom that dropped the decision", "decision.Kingdom"),
                F("playerInvolved", FieldKind.Bool, "true when the player was proposer or sponsor", "isPlayerInvolved"),
            },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("kingdomDecisionConcluded", "kingdom",
            "A kingdom council finished voting and its outcome is already in force — a decided war has been declared by the time this fires.",
            "KingdomDecisionConcluded",
            fields: new[]
            {
                F("kingdom", FieldKind.Kingdom, "the kingdom that decided", "decision.Kingdom"),
                F("playerInvolved", FieldKind.Bool, "true when the player was proposer, sponsor or a supporter in the vote", "isPlayerInvolved"),
            },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),
        new TriggerDefinition("warDeclared", "kingdom",
            "A war started between any two kingdoms or clans — including the player clan going hostile on its own; `attacker` and `defender` can be a kingdom or a clan, so test both against the player's clan and kingdom.",
            "WarDeclared",
            fields: new[]
            {
                F("attacker", FieldKind.Faction, "the faction that declared — a kingdom or a clan", "attacker"),
                F("defender", FieldKind.Faction, "the faction war was declared on", "defender"),
                F("detail", FieldKind.Text, "the game's DeclareWarDetail: how the war was started", "detail"),
            },
            subjectFields: S("attacker", "defender"), defaultSubject: Subjects.Player, common: true),
        new TriggerDefinition("peaceMade", "kingdom",
            "Two warring factions made peace — a full kingdom war or a single clan's private feud; `detail` is `ByKingdomDecision` when a council vote ended it.",
            "MakePeace",
            fields: new[]
            {
                F("firstFaction", FieldKind.Faction, "one side of the peace — a kingdom or a clan", "first"),
                F("secondFaction", FieldKind.Faction, "the other side", "second"),
                F("detail", FieldKind.Text, "the game's MakePeaceDetail: Default or ByKingdomDecision", "detail"),
            },
            subjectFields: S("firstFaction", "secondFaction"), defaultSubject: Subjects.Player, common: true),
        new TriggerDefinition("rulingClanChanged", "kingdom",
            "A kingdom got a new ruling clan — an elected new ruler or a clan that seized power; the event only carries the old clan, so the payload reads the new ruler from the kingdom itself.",
            "RulingClanChanged",
            fields: new[]
            {
                F("kingdom", FieldKind.Kingdom, "the kingdom with a new ruler", "kingdom"),
                F("oldRuler", FieldKind.Clan, "the clan that loses the crown", "oldRulingClan"),
                F("newRuler", FieldKind.Clan, "the clan taking over, read live from the kingdom", "kingdom.RulingClan"),
            },
            subjectFields: S("kingdom", "oldRuler", "newRuler"), defaultSubject: Subjects.Player),
        new TriggerDefinition("kingdomDestroyed", "kingdom",
            "A kingdom was destroyed and is about to be gone — its clans have already left it and read their new kingdoms live; comment on the end of a realm.",
            "KingdomDestroyedEvent",
            fields: new[] { F("kingdom", FieldKind.Kingdom, "the kingdom being destroyed; read its last state now", "kingdom") },
            subjectFields: S("kingdom"), defaultSubject: Subjects.Player),

        // ---- family (romance, marriage, children) ----

        new TriggerDefinition("romanticStateChanged", "family",
            "The romance between two heroes moved a step — courting, betrothal, marriage or a broken affair; in vanilla one of the two is the player.",
            "RomanticStateChanged",
            fields: new[]
            {
                F("firstHero", FieldKind.Hero, "one hero of the pair; the order is not fixed, match either field", "hero1"),
                F("secondHero", FieldKind.Hero, "the other hero of the pair", "hero2"),
                F("level", FieldKind.Text, "the game's Romance.RomanceLevelEnum: the romance step reached", "level"),
            },
            subjectFields: S("firstHero", "secondHero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroesAboutToMarry", "family",
            "Two heroes are about to marry — the wedding action has begun but relations, households and clan moves are not applied yet; a companion is marryable only while single, so react before their status changes. The argument order may be swapped.",
            "BeforeHeroesMarried",
            fields: new[]
            {
                F("firstHero", FieldKind.Hero, "one of the two heroes about to wed", "firstHero"),
                F("secondHero", FieldKind.Hero, "the other one", "secondHero"),
            },
            subjectFields: S("firstHero", "secondHero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("childEducationCompleted", "family",
            "A child hero finished an education stage — it fires at every stage of a childhood, not only the last, and `age` is the child's age at that moment.",
            "ChildEducationCompletedEvent",
            fields: new[]
            {
                F("child", FieldKind.Hero, "the child who finished the stage", "child"),
                F("age", FieldKind.Int, "the child's age at the moment it fires", "age"),
            },
            subjectFields: S("child"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroCameOfAge", "family",
            "A young hero came of age — a child's last stage ended and the adult world opens up.",
            "HeroComesOfAgeEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the hero who came of age", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroGrewOutOfInfancy", "family",
            "An infant hero grew out of infancy into childhood.",
            "HeroGrowsOutOfInfancyEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the child who grew out of infancy", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("heroBecameTeenager", "family",
            "A child hero became a teenager — the game may reroll their personality traits right at this moment.",
            "HeroReachesTeenAgeEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the child who became a teenager", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
    };
}
