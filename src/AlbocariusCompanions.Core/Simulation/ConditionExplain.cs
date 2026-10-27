using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Simulation;

/// <summary>One checked field: what it asks, what the state has, whether it holds; nested for all/any/not.</summary>
public sealed class ConditionResult
{
    [JsonProperty("field")] public string Field { get; set; } = "";
    [JsonProperty("holds")] public bool Holds { get; set; }
    [JsonProperty("expected", NullValueHandling = NullValueHandling.Ignore)] public JToken? Expected { get; set; }
    [JsonProperty("actual", NullValueHandling = NullValueHandling.Ignore)] public JToken? Actual { get; set; }
    [JsonProperty("nested", NullValueHandling = NullValueHandling.Ignore)] public List<ConditionResult>? Nested { get; set; }

    internal static ConditionResult Of(string field, bool holds, JToken? expected, JToken? actual) =>
        new ConditionResult { Field = field, Holds = holds, Expected = expected, Actual = actual };
}

/// <summary>
/// Field-by-field explanation of a condition for the simulator. The pass/fail verdict the simulator reports always comes
/// from <see cref="ConversationConditions"/> (the game's code); this only says why.
/// </summary>
public static class ConditionExplain
{
    public static List<ConditionResult> When(ConditionSet? when, IConversationContext context)
    {
        var results = new List<ConditionResult>();
        if (when == null)
            return results;
        foreach (var use in VariableUses.InCondition(when))
            if (use.Reference == null || context.GetVariable(use.Reference) == null)
                results.Add(ConditionResult.Of("companionLoaded", false, use.Reference, null));
        results.AddRange(Fields(when, context));
        return results;
    }

    private static IEnumerable<ConditionResult> Fields(ConditionSet when, IConversationContext c)
    {
        if (when.Met is { } met)
            yield return ConditionResult.Of("met", c.HasMet == met, met, c.HasMet);
        if (when.Hired is { } hired)
            yield return ConditionResult.Of("hired", c.IsHired == hired, hired, c.IsHired);
        if (when.InParty is { } inParty)
            yield return ConditionResult.Of("inParty", c.IsInPlayerParty == inParty, inParty, c.IsInPlayerParty);
        if (when.MinRelation is { } minRelation)
            yield return ConditionResult.Of("minRelation", c.Relation >= minRelation, minRelation, c.Relation);
        if (when.MaxRelation is { } maxRelation)
            yield return ConditionResult.Of("maxRelation", c.Relation <= maxRelation, maxRelation, c.Relation);
        if (when.MinGold is { } minGold)
            yield return ConditionResult.Of("minGold", c.PlayerGold >= minGold, minGold, c.PlayerGold);
        if (when.HasGold is { } hasGold)
        {
            var gold = c.GoldOf(hasGold.Who ?? "");
            yield return ConditionResult.Of("hasGold." + hasGold.Who, gold is { } g && g >= (hasGold.Min ?? 1), hasGold.Min ?? 1,
                gold is { } shown ? (JToken)shown : JValue.CreateNull());
        }
        if (when.HasItem is { } hasItem)
        {
            var count = c.PlayerItemCount(hasItem.Id ?? "");
            yield return ConditionResult.Of("hasItem." + hasItem.Id, count >= (hasItem.Min ?? 1), hasItem.Min ?? 1, count);
        }
        foreach (var flag in when.Flags ?? new List<string>())
            yield return Flag("flags." + flag, c.GetVariable(flag), true);
        foreach (var flag in when.NotFlags ?? new List<string>())
            yield return Flag("notFlags." + flag, c.GetVariable(flag), false);
        if (when.Var != null && TriggerConditions.IsEventReference(when.Var))
        {
            var payload = (c as ITriggerContext)?.Event;
            object? actual = null;
            var found = payload != null && payload.TryResolve(TriggerConditions.PathOf(when.Var), out actual);
            yield return ConditionResult.Of(when.Var, TriggerConditions.EventHolds(when, payload),
                when.Eq ?? new JObject { ["min"] = when.Min, ["max"] = when.Max }, found && actual != null ? JToken.FromObject(actual) : JValue.CreateNull());
        }
        else if (when.Var != null)
        {
            var value = c.GetVariable(when.Var) ?? 0;
            if (when.Min is { } min)
                yield return ConditionResult.Of($"var.{when.Var}.min", value >= min, min, value);
            if (when.Max is { } max)
                yield return ConditionResult.Of($"var.{when.Var}.max", value <= max, max, value);
            if (when.Eq != null && VariableUses.TypeOf(when.Eq) is { } type)
                yield return ConditionResult.Of($"var.{when.Var}.eq", value == VariableUses.ValueOf(when.Eq), when.Eq,
                    type == VariableType.Bool ? (JToken)(value != 0) : value);
        }
        if (when.Role != null)
        {
            var role = (c as ITriggerContext)?.Role;
            yield return ConditionResult.Of("role", role == when.Role, when.Role, role == null ? JValue.CreateNull() : (JToken)role);
        }
        if (when.All != null)
            yield return Group("all", when.All.Where(n => n != null).Select(n => Fields(n, c).ToList()).ToList(), all: true);
        if (when.Any != null)
            yield return Group("any", when.Any.Where(n => n != null).Select(n => Fields(n, c).ToList()).ToList(), all: false);
        if (when.Not != null)
        {
            var inner = Fields(when.Not, c).ToList();
            yield return new ConditionResult { Field = "not", Holds = !inner.All(r => r.Holds), Nested = inner };
        }
    }

    private static ConditionResult Flag(string field, int? value, bool expected) =>
        ConditionResult.Of(field, value != null && (value != 0) == expected, expected, value == null ? JValue.CreateNull() : (JToken)(value != 0));

    private static ConditionResult Group(string field, List<List<ConditionResult>> members, bool all)
    {
        var nested = members.Select((m, i) => new ConditionResult { Field = $"{field}[{i}]", Holds = m.All(r => r.Holds), Nested = m }).ToList();
        return new ConditionResult { Field = field, Holds = all ? nested.All(n => n.Holds) : nested.Any(n => n.Holds), Nested = nested };
    }

    /// <summary>The conditions a node's effects imply (a transfer source must hold the gold/items; join needs room).</summary>
    public static List<ConditionResult> Implied(EffectSet? effects, IConversationContext c)
    {
        var results = new List<ConditionResult>();
        var transfer = Transfers.Of(effects);
        if (transfer != null && transfer.From != null && transfer.From != Transfers.World)
        {
            if (transfer.Gold is { } gold)
            {
                var has = c.GoldOf(transfer.From);
                results.Add(ConditionResult.Of("transfer.from." + transfer.From, (has ?? -1) >= gold, gold,
                    has is { } shown ? (JToken)shown : JValue.CreateNull()));
            }
            if (transfer.Item != null)
            {
                var count = c.PlayerItemCount(transfer.Item);
                results.Add(ConditionResult.Of("transfer.from.player." + transfer.Item, count >= Transfers.CountOf(transfer),
                    Transfers.CountOf(transfer), count));
            }
        }
        if (effects?.Join == true)
        {
            results.Add(ConditionResult.Of("join.notHired", !c.IsHired, false, c.IsHired));
            results.Add(ConditionResult.Of("join.clanHasRoom", !c.ClanIsFull, false, c.ClanIsFull));
        }
        return results;
    }

    /// <summary>The conversation-level checks of a start (<see cref="ConversationConditions.IsOffered"/>).</summary>
    public static List<ConditionResult> Offered(ConversationDefinition conversation, IConversationContext c, bool alreadyStarted)
    {
        var results = new List<ConditionResult>();
        if (conversation.Once)
            results.Add(ConditionResult.Of("once", !alreadyStarted, false, alreadyStarted));
        if (conversation.Entry == ConversationRules.EntryGreeting)
            results.Add(ConditionResult.Of("greeting.met", c.HasMet, true, c.HasMet));
        results.AddRange(When(conversation.When, c));
        return results;
    }
}
