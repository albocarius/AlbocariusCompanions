using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Logging;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Generic dialog registration for every registry companion (GAME_MAP Q6, Q7). Each line is registered once as
/// <c>{=!}{VAR}</c>; its condition picks one of the slot's lines at random (SPEC_CORE_GAPS T5) in the active game language
/// (T6) and sets VAR to <c>{=&lt;full id&gt;}&lt;text&gt;</c>, so voice-over and lipsync still find the line (Q14 note 2).
/// Player texts come from the companion's player_* slot lines; no player-facing text lives in code. Authored conversations
/// (dialogs.json "conversations", M6) are registered after the fixed flow (GAME_MAP Q20).
/// </summary>
internal static class CompanionDialogs
{
    private const string HeroMainOptions = "hero_main_options";
    private const string CloseWindow = "close_window";
    private const string HireCostVariable = "ALBOCARIUS_HIRE_COST";

    // Beats vanilla start_wanderer_unmet (110): NPC lines are first-passing-match by priority (Q7).
    private const int IntroPriority = 120;

    // Greeting conversations: above the vanilla greetings (100/110), below the intro, which needs an unmet companion (Q20).
    private const int GreetingPriority = 119;

    // Menu options of conversations: listed above the vanilla 100 options, below story quests (Q7, Q20).
    private const int HubOptionPriority = 105;

    public static void Register(CampaignGameStarter starter)
    {
        foreach (var id in ModRuntime.Templates.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var companionId = id;
            Safe.Run(ModRuntime.Log, $"register dialogs for '{companionId}'", () => RegisterCompanion(starter, companionId));
        }
    }

    private static void RegisterCompanion(CampaignGameStarter starter, string id)
    {
        if (!ModRuntime.Registry.TryGet(id, out var package) || package == null)
        {
            ModRuntime.Log.Warning($"Dialogs: '{id}' has a template but no registry entry; skipped.");
            return;
        }

        foreach (var slot in DialogSlots.Required)
            if (DialogText.LinesOf(package, slot).Count == 0)
            {
                ModRuntime.Log.Error($"Dialogs: '{id}' has no usable '{slot}' line; no dialogs registered for it.");
                return;
            }

        var hireCost = package.Companion.HireCost ?? 0;
        var prefix = "albocarius_" + id + "_";
        var backstoryToken = prefix + "backstory";
        var hireOfferToken = prefix + "hire_offer";
        var hireResponseToken = prefix + "hire_response";
        var hireAcceptToken = prefix + "hire_accept";
        var hireDeclineToken = prefix + "hire_decline";
        var registered = 0;

        bool IsThisCompanion()
        {
            var hero = Hero.OneToOneConversationHero;
            return ModRuntime.IsModHero(hero) && hero.Template.StringId == id;
        }

        ConversationSentence.OnConditionDelegate Condition(string context, Func<bool> condition) =>
            () => Safe.Run(ModRuntime.Log, $"dialog condition {prefix}{context}", condition, fallback: false);

        ConversationSentence.OnConsequenceDelegate Consequence(string context, Action consequence) =>
            () => Safe.Run(ModRuntime.Log, $"dialog consequence {prefix}{context}", consequence);

        // Every registered line has its own variable: all player options' conditions run before the options are shown.
        string Variable(string context) => ("ALBOCARIUS_" + id + "_" + context).ToUpperInvariant();

        // Line text: a random line of the slot in the active language, keyed by its full id for voice-over (NPC slots) —
        // player slots work the same, the player is never voiced.
        bool SetText(string variable, string slot)
        {
            var text = DialogText.PickText(package, slot);
            if (text == null)
                return false;
            MBTextManager.SetTextVariable(variable, text);
            return true;
        }

        string Text(string variable) => "{=!}{" + variable + "}";

        // 1. First meeting.
        starter.AddDialogLine(prefix + "intro", "start", HeroMainOptions, Text(Variable("intro")),
            Condition("intro", () => IsThisCompanion() && !Hero.OneToOneConversationHero.HasMet
                && SetText(Variable("intro"), DialogSlots.Intro)),
            Consequence("intro", () => Hero.OneToOneConversationHero.SetHasMet()),
            IntroPriority);
        registered++;

        // 2. Backstory.
        starter.AddPlayerLine(prefix + "ask_backstory", HeroMainOptions, backstoryToken, Text(Variable("ask_backstory")),
            Condition("ask_backstory", () => IsThisCompanion() && SetText(Variable("ask_backstory"), PlayerSlots.AskAbout)), null);
        starter.AddDialogLine(prefix + "backstory", backstoryToken, HeroMainOptions, Text(Variable("backstory")),
            Condition("backstory", () => SetText(Variable("backstory"), DialogSlots.Backstory)), null);
        registered += 2;

        // 3. Hire offer.
        starter.AddPlayerLine(prefix + "ask_hire", HeroMainOptions, hireOfferToken, Text(Variable("ask_hire")),
            Condition("ask_hire", () =>
            {
                if (!IsThisCompanion())
                    return false;
                var hero = Hero.OneToOneConversationHero;
                return !hero.IsPlayerCompanion && hero.PartyBelongedTo == null
                    && SetText(Variable("ask_hire"), PlayerSlots.AskHire);
            }),
            null);
        starter.AddDialogLine(prefix + "hire_offer", hireOfferToken, hireResponseToken, Text(Variable("hire_offer")),
            Condition("hire_offer", () =>
            {
                MBTextManager.SetTextVariable(HireCostVariable, hireCost);
                return SetText(Variable("hire_offer"), DialogSlots.HireOffer);
            }),
            null);
        registered += 2;

        // 4. Player answers (all passing options are listed, Q7).
        starter.AddPlayerLine(prefix + "hire_pay", hireResponseToken, hireAcceptToken, Text(Variable("hire_pay")),
            Condition("hire_pay", () => Hero.MainHero.Gold >= hireCost && !TooManyCompanions()
                && SetText(Variable("hire_pay"), PlayerSlots.HirePay)),
            Consequence("hire_pay", () => Hire(id, hireCost)));
        starter.AddPlayerLine(prefix + "hire_full", hireResponseToken, hireDeclineToken, Text(Variable("hire_full")),
            Condition("hire_full", () => TooManyCompanions() && SetText(Variable("hire_full"), PlayerSlots.HireFull)), null);
        starter.AddPlayerLine(prefix + "hire_later", hireResponseToken, hireDeclineToken, Text(Variable("hire_later")),
            Condition("hire_later", () => !TooManyCompanions() && SetText(Variable("hire_later"), PlayerSlots.HireLater)), null);
        registered += 3;

        // 5. Companion's reply.
        starter.AddDialogLine(prefix + "hire_accept", hireAcceptToken, CloseWindow, Text(Variable("hire_accept")),
            Condition("hire_accept", () => SetText(Variable("hire_accept"), DialogSlots.HireAccept)), null);
        starter.AddDialogLine(prefix + "hire_decline", hireDeclineToken, HeroMainOptions, Text(Variable("hire_decline")),
            Condition("hire_decline", () => SetText(Variable("hire_decline"), DialogSlots.HireDecline)), null);
        registered += 2;

        foreach (var conversation in package.Conversations)
            registered += RegisterConversation(starter, package, conversation, IsThisCompanion);

        ModRuntime.Log.Info($"Dialogs: registered {registered} lines for '{id}' (hire cost {hireCost}, " +
            $"{package.Conversations.Count} conversation(s), " +
            $"languages {string.Join(", ", package.Languages.Keys.OrderBy(k => k, StringComparer.Ordinal))}).");
    }

    /// <summary>
    /// One sentence per edge of the conversation graph (Core ConversationGraph). Start sentences check the companion, the
    /// conversation's "when" and "once"; every sentence checks its node's "when" and implied conditions, sets its own
    /// text variable (all player options' conditions run before they are shown) and applies the node's effects.
    /// </summary>
    private static int RegisterConversation(CampaignGameStarter starter, CompanionPackage package, ConversationDefinition conversation,
        Func<bool> isThisCompanion)
    {
        var id = package.Id;
        var edges = ConversationGraph.Build(id, conversation);
        foreach (var edge in edges)
        {
            var current = edge;
            var context = $"conversation {conversation.Id} {current.SentenceId}";
            var variable = ("ALBOCARIUS_" + id + "_" + conversation.Id + "_" + current.SentenceId.Replace(':', '_')).ToUpperInvariant();
            var text = "{=!}{" + variable + "}";

            ConversationSentence.OnConditionDelegate condition = () => Safe.Run(ModRuntime.Log, "dialog condition " + context, () =>
            {
                if (current.IsStart && !isThisCompanion())
                    return false;
                var state = new HeroConversationContext(Hero.OneToOneConversationHero, id);
                if (!ConversationConditions.CanSay(conversation, current, state, ModRuntime.Flags.HasStarted(id, conversation.Id!)))
                    return false;
                var line = DialogText.TextOf(package, current.Node);
                if (line == null)
                    return false;
                MBTextManager.SetTextVariable(variable, line);
                return true;
            }, fallback: false);

            ConversationSentence.OnConsequenceDelegate consequence = () => Safe.Run(ModRuntime.Log, "dialog consequence " + context, () =>
            {
                if (current.IsStart)
                    ModRuntime.Flags.MarkStarted(id, conversation.Id!);
                ApplyEffects(id, current.Node);
            });

            if (current.IsPlayer)
                starter.AddPlayerLine(current.SentenceId, current.InputToken, current.OutputToken, text, condition, consequence,
                    current.IsStart ? HubOptionPriority : 100);
            else
                starter.AddDialogLine(current.SentenceId, current.InputToken, current.OutputToken, text, condition, consequence,
                    current.IsStart ? GreetingPriority : 100);
        }
        return edges.Count;
    }

    /// <summary>Node effects (GAME_MAP Q20): flags first, then relation, gold and joining, each logged.</summary>
    private static void ApplyEffects(string id, ConversationNode node)
    {
        var effects = node.Effects;
        if (effects == null)
            return;

        var hero = Hero.OneToOneConversationHero;
        foreach (var flag in effects.SetFlags ?? new List<string>())
            ModRuntime.Flags.Set(id, flag);
        foreach (var flag in effects.ClearFlags ?? new List<string>())
            ModRuntime.Flags.Clear(id, flag);

        if (effects.Relation is { } relation)
            ChangeRelationAction.ApplyPlayerRelation(hero, relation);

        if (effects.Gold is { } gold && gold < 0)
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, -gold);
        else if (effects.Gold is { } received && received > 0)
            GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, received);

        if (effects.Join)
        {
            AddCompanionAction.Apply(Clan.PlayerClan, hero);
            AddHeroToPartyAction.Apply(hero, MobileParty.MainParty);
        }

        ModRuntime.Log.Info($"Conversation effects of '{id}' node '{node.Id}': " +
            $"set [{string.Join(", ", effects.SetFlags ?? new List<string>())}], clear [{string.Join(", ", effects.ClearFlags ?? new List<string>())}], " +
            $"relation {effects.Relation?.ToString() ?? "-"}, gold {effects.Gold?.ToString() ?? "-"}, join {effects.Join}.");
    }

    /// <summary>The live values conversation conditions read, over the hero being talked to.</summary>
    private sealed class HeroConversationContext : IConversationContext
    {
        private readonly Hero _hero;
        private readonly string _companionId;

        public HeroConversationContext(Hero hero, string companionId)
        {
            _hero = hero;
            _companionId = companionId;
        }

        public bool HasMet => _hero.HasMet;
        public bool IsHired => _hero.IsPlayerCompanion;
        public bool IsInPlayerParty => _hero.PartyBelongedTo == MobileParty.MainParty;
        public int Relation => (int)Math.Round(_hero.GetRelationWithPlayer());
        public int PlayerGold => Hero.MainHero.Gold;
        public bool ClanIsFull => TooManyCompanions();
        public bool HasFlag(string flag) => ModRuntime.Flags.Has(_companionId, flag);
    }

    /// <summary>Vanilla limit check (Q6): the hire option is refused once the clan is at its companion limit.</summary>
    private static bool TooManyCompanions() =>
        Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit;

    /// <summary>Vanilla hire order (Q6): pay, join the clan, join the main party.</summary>
    private static void Hire(string id, int hireCost)
    {
        var hero = Hero.OneToOneConversationHero;
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, hireCost);
        AddCompanionAction.Apply(Clan.PlayerClan, hero);
        AddHeroToPartyAction.Apply(hero, MobileParty.MainParty);
        ModRuntime.Log.Info($"Hired '{id}' (hero {hero.StringId}) for {hireCost} denars.");
    }
}
