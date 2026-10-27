using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q42 triggers (TriggerRegistry.Q42.cs); field names and sources as in the registry.</summary>
    private static void Q42(Dictionary<string, TriggerAdapter> d)
    {
        // ---- trade, inventory, caravans ----
        d["inventoryExchanged"] = (owner, fire) => CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(owner,
            (_, _, isTrading) => Emit(fire, "inventoryExchanged", () => Of(("isTrading", Bool(isTrading)))));
        d["itemsDiscarded"] = (owner, fire) => CampaignEvents.OnItemsDiscardedByPlayerEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "itemsDiscarded", () => Of()));
        d["tradeProfitMade"] = (owner, fire) => CampaignEvents.OnPlayerTradeProfitEvent.AddNonSerializedListener(owner,
            profit => Emit(fire, "tradeProfitMade", () => Of(("profit", Int(profit)))));
        d["caravanTradeCompleted"] = (owner, fire) => CampaignEvents.OnCaravanTransactionCompletedEvent.AddNonSerializedListener(owner,
            (caravan, town, _) => Emit(fire, "caravanTradeCompleted", () => Of(("caravan", Party(caravan)), ("town", Settlement(town)))));
        d["assetIncomeEarned"] = (owner, fire) => CampaignEvents.OnPlayerEarnedGoldFromAssetEvent.AddNonSerializedListener(owner,
            (type, amount) => Emit(fire, "assetIncomeEarned", () => Of(("incomeType", Text(type)), ("amount", Int(amount)))));

        // ---- workshops and fief production ----
        d["workshopOwnerChanged"] = (owner, fire) => CampaignEvents.WorkshopOwnerChangedEvent.AddNonSerializedListener(owner,
            (workshop, previousOwner) => Emit(fire, "workshopOwnerChanged", () => Of(("owner", Hero(workshop?.Owner)), ("previousOwner", Hero(previousOwner)))));
        d["workshopTypeChanged"] = (owner, fire) => CampaignEvents.WorkshopTypeChangedEvent.AddNonSerializedListener(owner,
            workshop => Emit(fire, "workshopTypeChanged", () => Of(("owner", Hero(workshop?.Owner)))));
        d["itemProduced"] = (owner, fire) => CampaignEvents.OnItemProducedEvent.AddNonSerializedListener(owner,
            (item, settlement, amount) => Emit(fire, "itemProduced", () => Of(("item", Item(item)), ("settlement", Settlement(settlement)), ("amount", Int(amount)))));
        d["itemConsumed"] = (owner, fire) => CampaignEvents.OnItemConsumedEvent.AddNonSerializedListener(owner,
            (item, settlement, amount) => Emit(fire, "itemConsumed", () => Of(("item", Item(item)), ("settlement", Settlement(settlement)), ("amount", Int(amount)))));

        // ---- smithing ----
        d["itemCrafted"] = (owner, fire) => CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(owner,
            (item, _, isCraftingOrderItem) => Emit(fire, "itemCrafted", () => Of(("item", Item(item)), ("isCraftingOrder", Bool(isCraftingOrderItem)))));
        d["craftingPartUnlocked"] = (owner, fire) => CampaignEvents.CraftingPartUnlockedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "craftingPartUnlocked", () => Of()));
        d["itemSmelted"] = (owner, fire) => CampaignEvents.OnEquipmentSmeltedByHeroEvent.AddNonSerializedListener(owner,
            (hero, equipment) => Emit(fire, "itemSmelted", () => Of(("hero", Hero(hero)), ("item", Item(equipment.Item)))));
        d["itemsRefined"] = (owner, fire) => CampaignEvents.OnItemsRefinedEvent.AddNonSerializedListener(owner,
            (hero, _) => Emit(fire, "itemsRefined", () => Of(("hero", Hero(hero)))));
        d["craftingOrderCompleted"] = (owner, fire) => CampaignEvents.OnCraftingOrderCompletedEvent.AddNonSerializedListener(owner,
            (town, _, craftedItem, completerHero) => Emit(fire, "craftingOrderCompleted", () => Of(("town", Settlement(town)), ("item", Item(craftedItem)), ("completer", Hero(completerHero)))));

        // ---- quests, issues, incidents, persuasion ----
        d["questStarted"] = (owner, fire) => CampaignEvents.OnQuestStartedEvent.AddNonSerializedListener(owner,
            quest => Emit(fire, "questStarted", () => Of(("questGiver", Hero(quest?.QuestGiver)))));
        d["questCompleted"] = (owner, fire) => CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(owner,
            (quest, details) => Emit(fire, "questCompleted", () => Of(("questGiver", Hero(quest?.QuestGiver)), ("outcome", Text(details)))));
        d["issueCreated"] = (owner, fire) => CampaignEvents.OnNewIssueCreatedEvent.AddNonSerializedListener(owner,
            issue => Emit(fire, "issueCreated", () => Of(("issueOwner", Hero(issue?.IssueOwner)))));
        d["issueUpdated"] = (owner, fire) => CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(owner,
            (issue, details, solverHero) => Emit(fire, "issueUpdated", () => Of(("issueOwner", Hero(issue?.IssueOwner)), ("outcome", Text(details)), ("solver", Hero(solverHero)))));
        d["incidentResolved"] = (owner, fire) => CampaignEvents.OnIncidentResolvedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "incidentResolved", () => Of()));
        d["persuasionProgressed"] = (owner, fire) => CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(owner,
            _ => Emit(fire, "persuasionProgressed", () => Of()));
    }
}
