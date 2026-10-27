using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static AlbocariusCompanions.Game.TriggerPayloads;

namespace AlbocariusCompanions.Game;

internal static partial class TriggerAdapters
{
    /// <summary>GAME_MAP Q41 triggers (TriggerRegistry.Q41.cs); field names and sources as in the registry.</summary>
    private static void Q41(Dictionary<string, TriggerAdapter> d)
    {
        d["questLogEntryAdded"] = (owner, fire) => CampaignEvents.QuestLogAddedEvent.AddNonSerializedListener(owner,
            (quest, _) => Emit(fire, "questLogEntryAdded", () => Of(("questGiver", Hero(quest?.QuestGiver)))));
        d["issueLogEntryAdded"] = (owner, fire) => CampaignEvents.IssueLogAddedEvent.AddNonSerializedListener(owner,
            (issue, _) => Emit(fire, "issueLogEntryAdded", () => Of(("issueOwner", Hero(issue?.IssueOwner)))));
        d["barterAccepted"] = (owner, fire) => CampaignEvents.OnBarterAcceptedEvent.AddNonSerializedListener(owner,
            (offerer, other, _) => Emit(fire, "barterAccepted", () => Of(("offerer", Hero(offerer)), ("other", Hero(other)))));
        d["barterCancelled"] = (owner, fire) => CampaignEvents.OnBarterCanceledEvent.AddNonSerializedListener(owner,
            (offerer, other, _) => Emit(fire, "barterCancelled", () => Of(("offerer", Hero(offerer)), ("other", Hero(other)))));
        d["portraitOpened"] = (owner, fire) => CampaignEvents.CharacterPortraitPopUpOpenedEvent.AddNonSerializedListener(owner,
            character => Emit(fire, "portraitOpened", () => Of(("character", Hero(character)))));
        d["boardGameFinished"] = (owner, fire) => CampaignEvents.OnPlayerBoardGameOverEvent.AddNonSerializedListener(owner,
            (opposingHero, state) => Emit(fire, "boardGameFinished", () => Of(("opponent", Hero(opposingHero)), ("outcome", Text(state)))));
        d["tradeRumorHeard"] = (owner, fire) => CampaignEvents.OnTradeRumorIsTakenEvent.AddNonSerializedListener(owner,
            (_, _) => Emit(fire, "tradeRumorHeard", () => Of()));
    }
}
