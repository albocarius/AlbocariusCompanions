using System.Collections.Generic;
using System.Linq;
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
    /// <summary>
    /// A variable as referenced by this companion's conditions ("name" or "companion.name"), bool as 0/1; null when its
    /// companion is not loaded (or the variable is unknown).
    /// </summary>
    int? GetVariable(string reference);
}

/// <summary>Condition checks of conversations (M6). Pure: no game types, tested with a fake context.</summary>
public static class ConversationConditions
{
    /// <summary>
    /// Every set field of <paramref name="when"/> holds (an implicit "all"); null means no condition. A condition that
    /// references a variable of a companion that is not loaded is false as a whole, whatever "not"/"any" around it says.
    /// </summary>
    public static bool Holds(ConditionSet? when, IConversationContext context)
    {
        if (when == null)
            return true;
        foreach (var use in VariableUses.InCondition(when))
            if (use.Reference == null || context.GetVariable(use.Reference) == null)
                return false;
        return Evaluate(when, context);
    }

    private static bool Evaluate(ConditionSet when, IConversationContext context)
    {
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
            if (context.GetVariable(flag) == 0)
                return false;
        foreach (var flag in when.NotFlags ?? new List<string>())
            if (context.GetVariable(flag) != 0)
                return false;
        if (when.Var != null)
        {
            var value = context.GetVariable(when.Var) ?? 0;
            if (when.Min is { } min && value < min)
                return false;
            if (when.Max is { } max && value > max)
                return false;
            if (when.Eq != null && VariableUses.TypeOf(when.Eq) != null && value != VariableUses.ValueOf(when.Eq))
                return false;
        }
        foreach (var nested in when.All ?? new List<ConditionSet>())
            if (nested != null && !Evaluate(nested, context))
                return false;
        if (when.Any != null && !when.Any.Any(nested => nested != null && Evaluate(nested, context)))
            return false;
        if (when.Not != null && Evaluate(when.Not, context))
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
