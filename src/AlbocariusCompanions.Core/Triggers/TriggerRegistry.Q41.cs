using System.Collections.Generic;

namespace AlbocariusCompanions.Core.Triggers;

public static partial class TriggerRegistry
{
    /// <summary>GAME_MAP Q41 triggers (orchestra/triggers.tsv rows with q = Q41).</summary>
    private static IEnumerable<TriggerDefinition> Q41() => new TriggerDefinition[]
    {
        // Raise sites are campaign code and the game-menu UI — QuestBase / IssueBase log helpers, BarterManager,
        // SettlementMenuOverlayVM / ArmyMenuOverlayVM, CaravansCampaignBehavior — only the board game runs inside a
        // mission (MissionBoardGameLogic), so only boardGameFinished is inMission.
        // hideInformation (both journal-line events) is a pure UI flag and is left out of both entries.

        // ---- journal lines: quest and issue progress the player just received ----

        new TriggerDefinition("questLogEntryAdded", "quests",
            "A quest wrote a new journal entry the player just received — it fires for every journal line of a quest, not only at acceptance or completion.",
            "QuestLogAddedEvent",
            // quest is a complex argument; the row names no member of it but QuestGiver, so that is the only field.
            fields: new[] { F("questGiver", FieldKind.Hero, "the hero the quest came from — the player or one of their clan", "quest.QuestGiver") },
            subjectFields: S("questGiver"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("issueLogEntryAdded", "quests",
            "A campaign issue wrote a new journal entry the player just received — issue-arc progress between the issue's own created/resolved beats.",
            "IssueLogAddedEvent",
            // issue is a complex argument; the row names no member of it but IssueOwner (its IssueQuest link is a
            // quest object, left out).
            fields: new[] { F("issueOwner", FieldKind.Hero, "the hero the issue belongs to; usually the player", "issue.IssueOwner") },
            subjectFields: S("issueOwner"), defaultSubject: Subjects.PlayerClan),

        // ---- barter offers between lords (accepted/canceled pair; the offer collector event is not exposed) ----
        // barters / offeredBarters (List<Barterable>, what was exchanged) is left out of both entries: no GAME_MAP
        // member names a simple value of it.

        new TriggerDefinition("barterAccepted", "economy",
            "A barter offer was accepted and the exchange is already applied — heroes, prisoners, items, gold or a fief changed hands; it also fires for lord-to-lord offers, so gate on the player side.",
            "OnBarterAcceptedEvent",
            fields: new[]
            {
                F("offerer", FieldKind.Hero, "the hero who made the offer", "offererHero"),
                F("other", FieldKind.Hero, "the hero who accepted it", "otherHero"),
            },
            subjectFields: S("offerer", "other"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("barterCancelled", "economy",
            "A barter offer was refused and closed — nothing changed hands; the relation effect of the refusal shows in the relation triggers.",
            "OnBarterCanceledEvent",
            fields: new[]
            {
                F("offerer", FieldKind.Hero, "the hero who had made the offer", "offererHero"),
                F("other", FieldKind.Hero, "the hero who refused it", "otherHero"),
            },
            subjectFields: S("offerer", "other"), defaultSubject: Subjects.PlayerClan),

        // ---- scenes: portrait pop-up, board games, trade rumors ----

        new TriggerDefinition("portraitOpened", "scenes",
            "The player opened a character portrait pop-up in a settlement or army menu; the campaign is already paused while it is open — idle flavor bark about the inspected hero.",
            "CharacterPortraitPopUpOpenedEvent",
            // the character argument is a CharacterObject; the row says to take its HeroObject (the pop-up is only
            // opened for characters shown as heroes).
            fields: new[] { F("character", FieldKind.Hero, "the hero whose portrait was opened", "character.HeroObject") },
            subjectFields: S("character"), defaultSubject: Subjects.PlayerClan),
        new TriggerDefinition("boardGameFinished", "scenes",
            "A board game the player played ended in a win, loss or draw, before the relation and XP consequences are applied; a game the player canceled never fires it.",
            "OnPlayerBoardGameOverEvent",
            fields: new[]
            {
                F("opponent", FieldKind.Hero, "the opponent lord; empty when the opponent agent was not a hero", "opposingHero"),
                F("outcome", FieldKind.Text, "the game's BoardGameState: Win, Loss or Draw", "state"),
            },
            // always the player's own game (like playerBattleEnded): "any" is the natural default and matches every
            // firing; the opponent is usually not a player-side hero, so no subject field would match by default.
            defaultSubject: Subjects.Any,
            inMission: true), // SandBox MissionBoardGameLogic.SetGameOver: the board game runs as a mission scene
        new TriggerDefinition("tradeRumorHeard", "scenes",
            "The player asked a caravan captain for trade rumors and a batch of market gossip was heard — the rumor list itself is not in the payload.",
            "OnTradeRumorIsTakenEvent",
            // newRumors (List<TradeRumor>) is left out: the row names no member that gives it a simple payload value;
            // sourceSettlement is left out too: the only non-DLC raiser omits it, so it is always null (GAME_MAP
            // Q41 Uncertain). The caravan captain is a campaign global (MobileParty.ConversationParty), not an event
            // argument — the row itself notes "no subject arg", so only "any" can match.
            defaultSubject: Subjects.Any),
    };
}
