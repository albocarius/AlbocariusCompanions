using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>The live state a conversation condition reads; the game implements it over the talked-to hero.</summary>
public interface IConversationContext
{
    bool HasMet { get; }
    bool IsHired { get; }
    bool IsInPlayerParty { get; }
    int Relation { get; }
    int PlayerGold { get; }
    /// <summary>True when the clan is at its companion limit.</summary>
    bool ClanIsFull { get; }
    bool HasFlag(string flag);
}

/// <summary>Condition checks of conversations (M6). Pure: no game types, tested with a fake context.</summary>
public static class ConversationConditions
{
    /// <summary>Every set field of <paramref name="when"/> holds; null means no condition.</summary>
    public static bool Holds(ConditionSet? when, IConversationContext context)
    {
        if (when == null)
            return true;
        if (when.Met is { } met && context.HasMet != met)
            return false;
        if (when.Hired is { } hired && context.IsHired != hired)
            return false;
        if (when.InParty is { } inParty && context.IsInPlayerParty != inParty)
            return false;
        if (when.MinRelation is { } minRelation && context.Relation < minRelation)
            return false;
        if (when.MaxRelation is { } maxRelation && context.Relation > maxRelation)
            return false;
        if (when.MinGold is { } minGold && context.PlayerGold < minGold)
            return false;
        foreach (var flag in when.Flags ?? new List<string>())
            if (!context.HasFlag(flag))
                return false;
        foreach (var flag in when.NotFlags ?? new List<string>())
            if (context.HasFlag(flag))
                return false;
        return true;
    }

    /// <summary>
    /// Conditions the node's effects imply: a payment needs the gold, joining needs a companion that is not hired yet and
    /// a clan with room. A node whose effects cannot apply is not offered.
    /// </summary>
    public static bool EffectsCanApply(EffectSet? effects, IConversationContext context)
    {
        if (effects == null)
            return true;
        if (effects.Gold is { } gold && gold < 0 && context.PlayerGold < -gold)
            return false;
        if (effects.Join && (context.IsHired || context.ClanIsFull))
            return false;
        return true;
    }

    /// <summary>
    /// Whether the sentence for <paramref name="edge"/> may be said: the node's own condition and implied conditions,
    /// plus, for a start sentence, the conversation's condition and "once"; a greeting also needs a met companion (the
    /// first meeting belongs to the intro).
    /// </summary>
    public static bool CanSay(ConversationDefinition conversation, ConversationEdge edge, IConversationContext context,
        bool alreadyStarted)
    {
        if (edge.IsStart)
        {
            if (conversation.Once && alreadyStarted)
                return false;
            if (conversation.Entry == ConversationRules.EntryGreeting && !context.HasMet)
                return false;
            if (!Holds(conversation.When, context))
                return false;
        }
        return Holds(edge.Node.When, context) && EffectsCanApply(edge.Node.Effects, context);
    }
}
