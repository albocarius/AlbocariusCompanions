using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q42 triggers (orchestra/triggers.tsv rows with q = Q42).</summary>
    private static IEnumerable<TriggerDefinition> Q42() => new TriggerDefinition[]
    {
        // Every Q42 raise site is campaign code, a campaign game model or a campaign UI view model (InventoryLogic,
        // CaravansCampaignBehavior, DefaultClanFinanceModel, workshop/village-good behaviors, CraftingCampaignBehavior,
        // QuestBase/IssueBase/Incident, MapIncidentVM, Persuasion) — never a Mission*/Agent* class — so none of these
        // is inMission.

        // ---- trade, inventory and caravan transactions ----
        // The list arguments of these rows — List<(ItemRosterElement,int)> purchased/sold, the post-close ItemRoster of
        // the discard event, List<(EquipmentElement,int)> tradedItems — are left out: no row names a member of them that
        // gives a simple payload value (no count, no first item).

        new TriggerDefinition("inventoryExchanged", "economy",
            "The player closed a trade, loot or gift screen and all the items have already changed parties; the counterparty gold is settled right after.",
            "PlayerInventoryExchangeEvent",
            fields: new[] { F("isTrading", FieldKind.Bool, "true when the screen was a trade; false for looting, gifting or donating", "isTrading") },
            // always the player's own inventory session, and the item lists are not in the payload → only "any" can match
            defaultSubject: Subjects.Any),
        new TriggerDefinition("itemsDiscarded", "economy",
            "The player discarded items from the party inventory (loot mode, or with no other party present); the items are already gone and the payload does not say which ones.",
            "OnItemsDiscardedByPlayerEvent",
            // the only argument is the resulting main-party ItemRoster, not the discarded items → no usable field, "any" only
            defaultSubject: Subjects.Any),
        new TriggerDefinition("tradeProfitMade", "economy",
            "The vanilla trade skill counted the expected resale profit of the trade the player just closed, before the gold for it is settled.",
            "OnPlayerTradeProfitEvent",
            fields: new[] { F("profit", FieldKind.Int, "the estimated resale profit of this trade, in gold", "profit") },
            // always the player, and the single argument is a number → only "any" can match
            defaultSubject: Subjects.Any),
        new TriggerDefinition("caravanTradeCompleted", "economy",
            "A caravan finished one daily buy/sell round in a town: its goods, gold and rosters are already updated; it fires for every caravan in the world, so gate on the player's own.",
            "OnCaravanTransactionCompletedEvent",
            fields: new[]
            {
                F("caravan", FieldKind.Party, "the caravan party that traded", "caravanParty"),
                F("town", FieldKind.Settlement, "the town it bought from or sold to", "town.Settlement"),
            },
            subjectFields: S("caravan", "town"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("assetIncomeEarned", "economy",
            "The player's clan was paid for one of its assets in the daily income pass: fief taxes, a tribute share, a caravan's profit or a workshop's profit.",
            "OnPlayerEarnedGoldFromAssetEvent",
            fields: new[]
            {
                F("incomeType", FieldKind.Text, "what the money came from: Taxes | TributesEarned | Caravan | Workshop", "type"),
                F("amount", FieldKind.Int, "the gold just booked; a tribute site can report a negative amount (the player paid)", "amount"),
            },
            // already player-guarded at every raise site and no argument names an entity → only "any" can match
            defaultSubject: Subjects.Any),

        // ---- workshops and fief goods ----
        // Workshop has no payload kind of its own and the only documented member of it is Owner, so both workshop
        // triggers expose that hero; the town of a workshop and its new production type are not named by the rows.

        new TriggerDefinition("workshopOwnerChanged", "economy",
            "A workshop changed hands and the price was paid: the player bought or sold one, an heir inherited it, or the old owner died, went bankrupt or lost the town in war.",
            "WorkshopOwnerChangedEvent",
            fields: new[]
            {
                F("owner", FieldKind.Hero, "the hero who owns it now; player-side means the clan just gained it", "workshop.Owner"),
                F("previousOwner", FieldKind.Hero, "the hero who owned it until now; player-side means the clan sold or lost it", "previousOwner"),
            },
            subjectFields: S("owner", "previousOwner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("workshopTypeChanged", "economy",
            "The production line of a workshop was switched and the conversion cost was paid; the payload does not say what it produces now.",
            "WorkshopTypeChangedEvent",
            fields: new[] { F("owner", FieldKind.Hero, "the hero who owns the workshop", "workshop.Owner") },
            subjectFields: S("owner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("itemProduced", "economy",
            "A village or a workshop turned goods out into a settlement's store; it fires for every village and workshop in the world each day, so gate on the player's own fiefs.",
            "OnItemProducedEvent",
            fields: new[]
            {
                F("item", FieldKind.Item, "the item that was produced", "item"),
                F("settlement", FieldKind.Settlement, "the village or town whose store received it", "settlement"),
                F("amount", FieldKind.Int, "how many of the item were produced", "amount"),
            },
            subjectFields: S("settlement"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("itemConsumed", "economy",
            "A workshop consumed its inputs from the town warehouse or bought them on the town market; it fires for every workshop in the world, so gate on the player's own.",
            "OnItemConsumedEvent",
            fields: new[]
            {
                F("item", FieldKind.Item, "the item that was consumed", "item"),
                F("settlement", FieldKind.Settlement, "the town whose store lost it", "settlement"),
                F("amount", FieldKind.Int, "how many of the item were consumed", "amount"),
            },
            subjectFields: S("settlement"), defaultSubject: Subjects.PlayerClan),

        // ---- crafting: town forge, smelting and refinement (campaign crafting screens) ----
        // modifier (ItemModifier), piece (CraftingPiece), formula (RefiningFormula) and order (CraftingOrder) are complex
        // arguments; their rows name no member of them, so no field is exposed for them.

        new TriggerDefinition("itemCrafted", "crafting",
            "A weapon the player designed was built at a town forge and is already registered; its tier and category are on the item field, the design itself is not in the payload.",
            "OnNewItemCraftedEvent",
            fields: new[]
            {
                F("item", FieldKind.Item, "the newly crafted weapon", "item"),
                F("isCraftingOrder", FieldKind.Bool, "true when it was built to fill a crafting order; then it is not added to the party inventory", "isCraftingOrderItem"),
            },
            // player-only by construction and no argument names an entity → only "any" can match
            defaultSubject: Subjects.Any),
        new TriggerDefinition("craftingPartUnlocked", "crafting",
            "The player unlocked a new weapon-smithing part with smithing XP; the part itself is not in the payload.",
            "CraftingPartUnlockedEvent",
            // the CraftingPiece argument names no documented member → no field, "any" only
            defaultSubject: Subjects.Any),
        new TriggerDefinition("itemSmelted", "crafting",
            "A hero at the smithing screen melted down a piece of equipment; the materials and the XP are already granted, the stamina cost comes after.",
            "OnEquipmentSmeltedByHeroEvent",
            fields: new[]
            {
                F("hero", FieldKind.Hero, "the hero who smelted it — the hero selected in the crafting screen, not always the player", "hero"),
                F("item", FieldKind.Item, "the item that was melted down", "equipment.Item"),
            },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("itemsRefined", "crafting",
            "Raw materials were refined into other goods and added to the party inventory, and the XP was granted; the input/output formula is not in the payload.",
            "OnItemsRefinedEvent",
            fields: new[] { F("hero", FieldKind.Hero, "the hero who refined — the hero selected in the crafting screen", "hero") },
            subjectFields: S("hero"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("craftingOrderCompleted", "crafting",
            "A weapon-smithing order was completed: its gold reward, quality bonus and perk relation effects are already applied and the order is gone from the town.",
            "OnCraftingOrderCompletedEvent",
            // order (CraftingOrder) is left out: the row names no member of it (its owner clan is only described in words).
            fields: new[]
            {
                F("town", FieldKind.Settlement, "the town whose forge ran the order", "town.Settlement"),
                F("item", FieldKind.Item, "the item delivered for the order", "craftedItem"),
                F("completer", FieldKind.Hero, "the hero who finished it; the player when no crafting hero was chosen", "completerHero"),
            },
            subjectFields: S("completer", "town"), defaultSubject: Subjects.PlayerClan),

        // ---- quests, issues and incidents (the journal-line pair of the same families is in Q41) ----
        // quest (QuestBase), issue (IssueBase) and incident (Incident) are complex arguments: the rows name only
        // QuestGiver and IssueOwner on them, so the quest/issue triggers expose just that member and incidentResolved
        // gets no field at all.

        new TriggerDefinition("questStarted", "quests",
            "A quest the player took started and is now ongoing in the quest log; 1.4.8 keeps one global quest list, so every quest is the player's.",
            "OnQuestStartedEvent",
            // quest is a complex argument; the row names no member of it but QuestGiver (there is no AcceptedHero), so
            // that is the only field — and it is usually a lord outside the player clan, so content that should react to
            // any new quest has to ask for "any" explicitly.
            fields: new[] { F("questGiver", FieldKind.Hero, "the hero the quest came from — a lord who offered it, or a clan member", "quest.QuestGiver") },
            subjectFields: S("questGiver"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("questCompleted", "quests",
            "A quest the player had ended — succeeded, timed out, failed, was betrayed or cancelled — and its outcome hook has run.",
            "OnQuestCompletedEvent",
            fields: new[]
            {
                F("questGiver", FieldKind.Hero, "the hero the quest came from", "quest.QuestGiver"),
                F("outcome", FieldKind.Text, "the game's QuestCompleteDetails: Success, Timeout, Fail, FailWithBetrayal or Cancel", "details"),
            },
            subjectFields: S("questGiver"), defaultSubject: Subjects.PlayerClan, common: true),
        new TriggerDefinition("issueCreated", "quests",
            "A new campaign issue appeared for a hero — an ongoing problem with a quest or a troop-sending solution attached; it fires across the world, so gate on the player's side.",
            "OnNewIssueCreatedEvent",
            // issue is a complex argument; the row names IssueOwner (its CurrentSettlement/OwnerClan only appear inside
            // the row's own clan filter) and a nullable type, so the owner hero is the only field.
            fields: new[] { F("issueOwner", FieldKind.Hero, "the hero the issue belongs to; usually the player", "issue.IssueOwner") },
            subjectFields: S("issueOwner"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("issueUpdated", "quests",
            "A campaign issue the player is dealing with moved one step forward: its quest started, troops were sent, or it ended in success, failure, betrayal, timeout or an AI lord's solution.",
            "OnIssueUpdatedEvent",
            fields: new[]
            {
                F("issueOwner", FieldKind.Hero, "the hero the issue belongs to; usually the player", "issue.IssueOwner"),
                F("outcome", FieldKind.Text, "the game's IssueUpdateDetails step, e.g. PlayerStartedIssueQuestClassicSolution, IssueFinishedWithSuccess, IssueTimedOut", "details"),
                F("solver", FieldKind.Hero, "the hero credited at this step; empty unless the player had tried before or a quest was started", "solverHero"),
            },
            subjectFields: S("issueOwner", "solver"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("incidentResolved", "quests",
            "The player chose an option in a map incident pop-up and every consequence of that choice (gold, party morale, relations) has already run.",
            "OnIncidentResolvedEvent",
            // the Incident argument names no documented member, so its id/text cannot be exposed: no field, "any" only.
            // The pop-up is the player's own by construction.
            defaultSubject: Subjects.Any),

        // ---- persuasion: one option of a persuasion conversation (category scenes) ----

        new TriggerDefinition("persuasionProgressed", "scenes",
            "The player used one option in a persuasion conversation and its result was rolled, rerolled by perks and applied; neither the option nor its result is in the payload.",
            "PersuasionProgressCommittedEvent",
            // the tuple argument (PersuasionOptionArgs, PersuasionOptionResult) names no documented member, and the
            // conversation partner is the campaign global ConversationManager.OneToOneConversationHero rather than an
            // event argument (Q42 Uncertain) — so there is no field and only "any" can match.
            defaultSubject: Subjects.Any),
    };
}
