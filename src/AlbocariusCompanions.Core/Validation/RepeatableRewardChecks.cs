using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// I5 "repeatable reward": a conversation that is not "once" and can reward the player (relation up, or gold/items to
/// the player from anyone but the player) along a path whose own writes never turn one of its guards false — the player
/// can collect it again and again. Guards are the conversation's "when" and the "when" of each node on the path from a
/// start node to the reward (top level and nested "all" only: "any"/"not" guarantee nothing). A guard turns false when
/// the path sets a bool it requires false (notFlags / eq false) or clears one it requires true, moves an int out of its
/// min/max/eq. Gold checks do not count: paying only limits how often, it does not stop the repeat.
/// </summary>
public static class RepeatableRewardChecks
{
    /// <summary>Simple paths are enumerated per start node; conversations are small, this caps pathological graphs.</summary>
    private const int MaxPaths = 2000;

    public static void Validate(ConversationDefinition conversation, string ownId, string folder, List<ValidationIssue> issues)
    {
        if (conversation.Once)
            return;
        var byId = (conversation.Nodes ?? new List<ConversationNode>())
            .Where(n => n?.Id != null)
            .GroupBy(n => n.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var reported = new HashSet<string>(StringComparer.Ordinal);
        var budget = MaxPaths;
        foreach (var start in conversation.Start ?? new List<string>())
            if (start != null && byId.TryGetValue(start, out var node))
                Walk(conversation, ownId, byId, new List<ConversationNode> { node }, reported, ref budget);

        foreach (var nodeId in reported)
            issues.Add(ValidationIssue.Warning(IssueCodes.RepeatableReward, folder,
                $"Conversation '{conversation.Id}': node '{nodeId}' ({Reward(byId[nodeId].Effects, ownId)}) can be collected again and again; " +
                "make the conversation \"once\" or set a flag on that path that its condition checks (e.g. setFlags + notFlags)."));
    }

    private static void Walk(ConversationDefinition conversation, string ownId, Dictionary<string, ConversationNode> byId,
        List<ConversationNode> path, HashSet<string> reported, ref int budget)
    {
        if (--budget < 0)
            return;
        var last = path[path.Count - 1];
        if (Reward(last.Effects, ownId) != null && !reported.Contains(last.Id!) && !PathBlocksRepeat(conversation, ownId, path))
            reported.Add(last.Id!);

        foreach (var next in last.Next ?? new List<string>())
        {
            if (next == null || !byId.TryGetValue(next, out var child) || path.Contains(child))
                continue;
            path.Add(child);
            Walk(conversation, ownId, byId, path, reported, ref budget);
            path.RemoveAt(path.Count - 1);
        }
    }

    /// <summary>What the node gives the player, for the message; null when nothing.</summary>
    private static string? Reward(EffectSet? effects, string ownId)
    {
        if (effects == null)
            return null;
        if (effects.Relation > 0)
            return $"relation +{effects.Relation}";
        var transfer = Transfers.Of(effects);
        if (transfer != null && transfer.To == Transfers.Player && Transfers.NormalizeActor(transfer.From, ownId) != Transfers.Player)
            return transfer.Gold != null ? $"{transfer.Gold} gold" : $"{Transfers.CountOf(transfer)} {transfer.Item}";
        return null;
    }

    /// <summary>True when a write on the path makes one of the path's guards false the next time.</summary>
    private static bool PathBlocksRepeat(ConversationDefinition conversation, string ownId, List<ConversationNode> path)
    {
        var writes = new List<(string Key, VariableUse Use, EffectSet Effects)>();
        foreach (var node in path)
            foreach (var use in VariableUses.InEffects(node.Effects))
                if (Key(ownId, use.Reference) is { } key)
                    writes.Add((key, use, node.Effects!));

        var guards = Guards(conversation.When).Concat(path.SelectMany(n => Guards(n.When)));
        return guards.Any(g => Falsifies(g, ownId, writes));
    }

    /// <summary>Condition objects that must all hold: the object itself and its nested "all" elements.</summary>
    private static IEnumerable<ConditionSet> Guards(ConditionSet? when)
    {
        if (when == null)
            yield break;
        yield return when;
        foreach (var nested in when.All ?? new List<ConditionSet>())
            foreach (var guard in Guards(nested))
                yield return guard;
    }

    private static bool Falsifies(ConditionSet guard, string ownId, List<(string Key, VariableUse Use, EffectSet Effects)> writes)
    {
        bool Writes(string? reference, Func<VariableUse, EffectSet, bool> breaks) =>
            Key(ownId, reference) is { } key && writes.Any(w => w.Key == key && breaks(w.Use, w.Effects));

        foreach (var flag in guard.NotFlags ?? new List<string>())
            if (Writes(flag, (use, e) => SetsBool(use, e) == true))
                return true;
        foreach (var flag in guard.Flags ?? new List<string>())
            if (Writes(flag, (use, e) => SetsBool(use, e) == false))
                return true;
        if (guard.Var != null)
        {
            if (guard.Eq != null && VariableUses.TypeOf(guard.Eq) is { } type)
            {
                var wanted = VariableUses.ValueOf(guard.Eq);
                if (Writes(guard.Var, (use, e) => type == VariableType.Bool
                        ? SetsBool(use, e) is { } b && (b ? 1 : 0) != wanted
                        : SetsInt(use, e) is { } v ? v != wanted : AddsTo(use, e) != 0))
                    return true;
            }
            if (guard.Max != null && Writes(guard.Var, (use, e) => AddsTo(use, e) > 0 || SetsInt(use, e) > guard.Max))
                return true;
            if (guard.Min != null && Writes(guard.Var, (use, e) => AddsTo(use, e) < 0 || SetsInt(use, e) < guard.Min))
                return true;
        }
        return false;
    }

    /// <summary>The bool a write stores; null when the write is not a bool store.</summary>
    private static bool? SetsBool(VariableUse use, EffectSet effects)
    {
        if (use.Key == "setFlags") return true;
        if (use.Key == "clearFlags") return false;
        if (use.Key == "set" && Value(effects, use) is { } token && VariableUses.TypeOf(token) == VariableType.Bool) return VariableUses.ValueOf(token) != 0;
        return null;
    }

    private static int? SetsInt(VariableUse use, EffectSet effects) =>
        use.Key == "set" && Value(effects, use) is { } token && VariableUses.TypeOf(token) == VariableType.Int ? VariableUses.ValueOf(token) : (int?)null;

    private static int AddsTo(VariableUse use, EffectSet effects) =>
        use.Key == "add" && effects.Add != null && use.Reference != null && effects.Add.TryGetValue(use.Reference, out var amount) ? amount : 0;

    private static Newtonsoft.Json.Linq.JToken? Value(EffectSet effects, VariableUse use) =>
        effects.Set != null && use.Reference != null && effects.Set.TryGetValue(use.Reference, out var token) ? token : null;

    /// <summary>"name" and "&lt;own id&gt;.name" are the same variable: compare as "owner.name".</summary>
    private static string? Key(string ownId, string? reference) =>
        VariableRules.TryParseReference(ownId, reference, out var r) ? r.ToString() : null;
}
