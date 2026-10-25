using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// dialogs.json "conversations" (M6): structure, node ids (shared namespace with the companion's lines), links,
/// conditions and effects. Node texts and audio are checked with the lines by <see cref="DialogChecks"/>.
/// </summary>
public static class ConversationChecks
{
    /// <summary>
    /// Validates every conversation of <paramref name="package"/>. <paramref name="localIds"/> holds the line ids already
    /// seen (node ids are added); valid nodes are returned for the text/audio checks.
    /// </summary>
    public static IReadOnlyList<ConversationNode> Validate(CompanionPackage package, HashSet<string> localIds,
        HashSet<string> seenFullIds, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var valid = new List<ConversationNode>();
        var conversationIds = new HashSet<string>(StringComparer.Ordinal);

        VariableChecks.Validate(package, issues);
        if (package.Dialogs?.Conversations?.Any(c => c == null) == true)
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidConversation, folder, "dialogs.json contains an empty (null) conversation."));

        foreach (var conversation in package.Conversations)
        {
            var name = conversation.Id ?? "(no id)";
            if (!LineIds.IsValidLocal(conversation.Id))
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                    $"needs an 'id' matching {LineIds.LocalIdPattern} (e.g. 'old_wound')."));
            else if (!conversationIds.Add(conversation.Id!))
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name, "has the same id as another conversation of this companion."));

            valid.AddRange(ValidateConversation(conversation, name, package.Id, folder, localIds, seenFullIds, issues));
        }

        return valid;
    }

    private static List<ConversationNode> ValidateConversation(ConversationDefinition conversation, string name, string ownId, string folder,
        HashSet<string> localIds, HashSet<string> seenFullIds, List<ValidationIssue> issues)
    {
        var valid = new List<ConversationNode>();
        if (!ConversationRules.Entries.Contains(conversation.Entry ?? "", StringComparer.Ordinal))
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                $"has entry '{conversation.Entry}'; use one of: {string.Join(", ", ConversationRules.Entries)}."));

        var nodes = conversation.Nodes ?? new List<ConversationNode>();
        if (nodes.Count == 0)
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name, "has no nodes."));

        var byId = new Dictionary<string, ConversationNode>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node == null)
            {
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name, "contains an empty (null) node."));
                continue;
            }

            if (!LineIds.IsValidLocal(node.Id) || ConversationRules.IsReservedNext(node.Id))
            {
                issues.Add(Error(IssueCodes.InvalidLineId, folder, name, node.Id == null
                    ? "has a node without 'id'."
                    : $"has node id '{node.Id}': ids match {LineIds.LocalIdPattern} and '{ConversationRules.NextHub}'/'{ConversationRules.NextEnd}' are reserved."));
            }
            else if (!localIds.Add(node.Id!))
            {
                // Still linkable, so one duplicate id is one error, not a cascade of unknown-node errors.
                if (!byId.ContainsKey(node.Id!))
                    byId[node.Id!] = node;
                issues.Add(Error(IssueCodes.DuplicateLineId, folder, name,
                    $"has node id '{node.Id}', which another line or node of this companion already uses."));
            }
            else
            {
                byId[node.Id!] = node;
                if (!seenFullIds.Add(node.FullId!))
                    issues.Add(Error(IssueCodes.DuplicateLineId, folder, name,
                        $"has node '{node.Id}' with the full id '{node.FullId}', which another companion already uses."));
                else
                    valid.Add(node);
            }

            if (!ConversationRules.Speakers.Contains(node.Speaker ?? "", StringComparer.Ordinal))
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                    $"node '{node.Id}' has speaker '{node.Speaker}'; use '{ConversationRules.SpeakerNpc}' or '{ConversationRules.SpeakerPlayer}'."));
            else if (node.Speaker == ConversationRules.SpeakerPlayer && (node.Voiced || node.Audio != null))
                issues.Add(Error(IssueCodes.VoicedPlayerLine, folder, name,
                    $"node '{node.Id}' is a player line; the player is never voiced (remove 'voiced'/'audio')."));

            ValidateConditions(node.When, $"node '{node.Id}'", folder, name, issues);
            ValidateEffects(node.Effects, node.Id, ownId, folder, name, issues);
        }

        ValidateStart(conversation, byId, folder, name, issues);
        foreach (var node in byId.Values)
            ValidateNext(node, byId, folder, name, issues);
        ValidateConditions(conversation.When, "the conversation", folder, name, issues);
        if (conversation.Entry == ConversationRules.EntryGreeting && conversation.When?.Met == false)
            issues.Add(Error(IssueCodes.InvalidCondition, folder, name,
                "is a greeting with \"met\": false; greetings are for companions already met (the first meeting is the intro)."));

        ReportUnreachable(conversation, byId, folder, name, issues);
        RepeatableRewardChecks.Validate(conversation, ownId, folder, issues);
        return valid;
    }

    private static void ValidateStart(ConversationDefinition conversation, Dictionary<string, ConversationNode> byId, string folder,
        string name, List<ValidationIssue> issues)
    {
        var start = conversation.Start ?? new List<string>();
        if (start.Count == 0)
        {
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name, "has no 'start' node."));
            return;
        }

        var expected = conversation.Entry == ConversationRules.EntryGreeting ? ConversationRules.SpeakerNpc : ConversationRules.SpeakerPlayer;
        foreach (var id in start)
        {
            if (id == null || !byId.TryGetValue(id, out var node))
                issues.Add(Error(IssueCodes.UnknownNode, folder, name, $"starts with '{id}', which is not a node of this conversation."));
            else if (ConversationRules.Entries.Contains(conversation.Entry ?? "", StringComparer.Ordinal) && node.Speaker != expected)
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name, conversation.Entry == ConversationRules.EntryGreeting
                    ? $"is a greeting, so its start node '{id}' must be a companion ({ConversationRules.SpeakerNpc}) line."
                    : $"starts from the menu, so its start node '{id}' must be a {ConversationRules.SpeakerPlayer} line."));
        }

        if (start.Distinct(StringComparer.Ordinal).Count() != start.Count)
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name, "lists a start node twice."));
        // No stall check here: a start option that fails is just not offered, a greeting that fails leaves vanilla's.
    }

    private static void ValidateNext(ConversationNode node, Dictionary<string, ConversationNode> byId, string folder, string name,
        List<ValidationIssue> issues)
    {
        var next = node.Next ?? new List<string>();
        var where = $"node '{node.Id}'";
        if (next.Count == 0)
        {
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                $"{where} has no 'next'; list the following nodes, or use [\"{ConversationRules.NextHub}\"] or [\"{ConversationRules.NextEnd}\"]."));
            return;
        }

        if (next.Any(ConversationRules.IsReservedNext))
        {
            if (next.Count > 1)
                issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                    $"{where}: '{ConversationRules.NextHub}' and '{ConversationRules.NextEnd}' must be the only entry of 'next'."));
            return;
        }

        if (next.Distinct(StringComparer.Ordinal).Count() != next.Count)
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name, $"{where} lists a next node twice."));

        var children = new List<ConversationNode>();
        foreach (var id in next)
        {
            if (id == null || !byId.TryGetValue(id, out var child))
                issues.Add(Error(IssueCodes.UnknownNode, folder, name, $"{where} continues with '{id}', which is not a node of this conversation."));
            else
                children.Add(child);
        }

        var speakers = children.Select(c => c.Speaker).Where(s => s != null).Distinct().ToList();
        if (speakers.Count > 1)
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                $"{where} continues with both companion and player lines; 'next' is either the companion's possible replies or the player's options."));
        else if (node.Speaker == ConversationRules.SpeakerPlayer && speakers.SingleOrDefault() == ConversationRules.SpeakerPlayer)
            issues.Add(Error(IssueCodes.InvalidConversation, folder, name,
                $"{where} is a player line followed by player lines; the companion must answer in between."));
        else
            WarnIfMayStall(next, byId, $"{where}'s next", folder, name, issues);
    }

    /// <summary>When every follow-up has a condition, none may hold and the conversation would hang there.</summary>
    private static void WarnIfMayStall(IReadOnlyList<string> ids, Dictionary<string, ConversationNode> byId, string where, string folder,
        string name, List<ValidationIssue> issues)
    {
        var nodes = ids.Where(id => id != null && byId.ContainsKey(id)).Select(id => byId[id]).ToList();
        if (nodes.Count > 0 && nodes.All(HasCondition))
            issues.Add(ValidationIssue.Warning(IssueCodes.ConversationMayStall, folder,
                $"Conversation '{name}': every node of {where} has a condition; if none holds the conversation gets stuck. Add one without a condition."));
    }

    private static bool HasCondition(ConversationNode node) =>
        IsSet(node.When) || node.Effects?.Join == true || TransferNeedsSource(node.Effects);

    /// <summary>A transfer whose source is not "world" needs that source to hold: an implied hasGold/hasItem condition.</summary>
    private static bool TransferNeedsSource(EffectSet? effects)
    {
        var transfer = Transfers.Of(effects);
        return transfer != null && transfer.From != null && transfer.From != Transfers.World;
    }

    private static bool IsSet(ConditionSet? when) =>
        when != null && (when.Met != null || when.Hired != null || when.InParty != null || when.MinRelation != null ||
            when.MaxRelation != null || when.MinGold != null || when.HasGold != null || when.HasItem != null ||
            when.Flags?.Count > 0 || when.NotFlags?.Count > 0 ||
            when.All?.Any(IsSet) == true || when.Any != null || when.Not != null || when.Var != null);

    private static void ReportUnreachable(ConversationDefinition conversation, Dictionary<string, ConversationNode> byId, string folder,
        string name, List<ValidationIssue> issues)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>((conversation.Start ?? new List<string>()).Where(id => id != null && byId.ContainsKey(id)));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reached.Add(id))
                continue;
            foreach (var next in byId[id].Next ?? new List<string>())
                if (next != null && byId.ContainsKey(next))
                    queue.Enqueue(next);
        }

        foreach (var id in byId.Keys.Where(id => !reached.Contains(id)))
            issues.Add(ValidationIssue.Warning(IssueCodes.UnreachableNode, folder,
                $"Conversation '{name}': node '{id}' cannot be reached from the start; it is never said."));
    }

    private static void ValidateConditions(ConditionSet? when, string where, string folder, string name, List<ValidationIssue> issues)
    {
        if (when == null)
            return;
        void Bad(string message) => issues.Add(Error(IssueCodes.InvalidCondition, folder, name, $"{where}: {message}"));

        if (when.MinRelation is { } min && !InRelationRange(min))
            Bad($"minRelation {min} is outside {ConversationRules.MinRelation}..{ConversationRules.MaxRelation}.");
        if (when.MaxRelation is { } max && !InRelationRange(max))
            Bad($"maxRelation {max} is outside {ConversationRules.MinRelation}..{ConversationRules.MaxRelation}.");
        if (when.MinRelation > when.MaxRelation)
            Bad($"minRelation {when.MinRelation} is greater than maxRelation {when.MaxRelation}.");
        if (when.MinGold < 0)
            Bad($"minGold {when.MinGold} is negative.");
        if (when.HasGold != null && GoldCheckProblem(when.HasGold) is { } goldProblem)
            Bad($"hasGold: {goldProblem}.");
        if (when.HasItem != null && ItemCheckProblem(when.HasItem) is { } itemProblem)
            Bad($"hasItem: {itemProblem}.");
        foreach (var flag in Both(when.Flags, when.NotFlags))
            Bad($"'{flag}' is in both 'flags' and 'notFlags'; that can never hold.");

        if (when.Var == null && (when.Min != null || when.Max != null || when.Eq != null))
            Bad("'min'/'max'/'eq' need a 'var' in the same object.");
        if (when.Var != null && when.Min == null && when.Max == null && when.Eq == null)
            Bad($"'var' '{when.Var}' needs 'min', 'max' or 'eq'.");
        if (when.Eq != null && (when.Min != null || when.Max != null))
            Bad($"'var' '{when.Var}': use either 'eq' or 'min'/'max'.");
        if (when.Min > when.Max)
            Bad($"'var' '{when.Var}': min {when.Min} is greater than max {when.Max}.");

        void Nested(List<ConditionSet>? list, string key)
        {
            if (list == null)
                return;
            if (list.Count == 0)
                Bad($"'{key}' is empty.");
            for (var i = 0; i < list.Count; i++)
                if (list[i] == null)
                    Bad($"'{key}' contains an empty (null) condition.");
                else
                    ValidateConditions(list[i], $"{where} {key}[{i}]", folder, name, issues);
        }
        Nested(when.All, "all");
        Nested(when.Any, "any");
        if (when.Not != null)
            ValidateConditions(when.Not, $"{where} not", folder, name, issues);
    }

    private static string? GoldCheckProblem(GoldCheck check)
    {
        if (check.Who == null)
            return $"'who' is missing; use \"{Transfers.Player}\", \"{Transfers.Self}\" or a companion id";
        if (!Transfers.IsValidActor(check.Who))
            return BadActor("'who'", check.Who);
        if (check.Who == Transfers.World)
            return $"'who' cannot be \"{Transfers.World}\"; it is a source/sink and has no gold of its own";
        if (check.Min == null)
            return "'min' is missing";
        if (check.Min < 1)
            return $"min {check.Min.Value} must be at least 1";
        return null;
    }

    private static string? ItemCheckProblem(ItemCheck check)
    {
        if (check.Who == null)
            return $"'who' is missing; use \"{Transfers.Player}\"";
        if (check.Who != Transfers.Player)
            return $"'who' '{check.Who}' is not supported; items are counted in the player's party inventory, so use \"{Transfers.Player}\"";
        if (string.IsNullOrWhiteSpace(check.Id))
            return "'id' is missing or empty";
        if (check.Min is { } min && min < 1)
            return $"min {min} must be at least 1";
        return null;
    }

    private static void ValidateEffects(EffectSet? effects, string? nodeId, string ownId, string folder, string name, List<ValidationIssue> issues)
    {
        if (effects == null)
            return;
        void Bad(string message) => issues.Add(Error(IssueCodes.InvalidEffect, folder, name, $"node '{nodeId}' effects: {message}"));

        if (effects.Relation is { } relation && (relation == 0 || !InRelationRange(relation)))
            Bad($"relation {relation} must be a non-zero change within {ConversationRules.MinRelation}..{ConversationRules.MaxRelation}.");
        if (effects.Gold == 0)
            Bad("gold 0 has no effect; use a negative amount (the player pays) or a positive one (the player receives).");
        if (effects.Transfer != null)
        {
            if (effects.Gold != null)
                Bad("has both 'transfer' and the old 'gold' key; 'gold' is only the shorthand for a player/world transfer.");
            else if (TransferProblem(effects.Transfer, ownId) is { } problem)
                Bad($"transfer: {problem}.");
        }
        foreach (var pair in effects.Add ?? new Dictionary<string, int>())
            if (pair.Value == 0)
                Bad($"add {pair.Value} to '{pair.Key}' has no effect.");
        foreach (var group in VariableUses.InEffects(effects).Where(u => u.Reference != null).GroupBy(u => u.Reference!, StringComparer.Ordinal))
            if (group.Count() > 1)
                Bad($"'{group.Key}' is written more than once ({string.Join(", ", group.Select(u => u.Key))}).");
    }

    /// <summary>What is wrong with a transfer (I3), or null when it is valid; the companion's own id names "self".</summary>
    private static string? TransferProblem(TransferEffect transfer, string ownId)
    {
        if (transfer.Gold == null && transfer.Item == null)
            return "needs either 'gold' or 'item'";
        if (transfer.Gold != null && transfer.Item != null)
            return "has both 'gold' and 'item'; transfer one of them";
        if (transfer.Gold is { } gold && gold <= 0)
            return $"gold {gold} is not an amount; it must be positive and the direction comes from 'from'/'to'";
        if (transfer.Item is { } item && string.IsNullOrWhiteSpace(item))
            return "'item' is empty";
        if (transfer.Count != null && transfer.Item == null)
            return "'count' only applies to an 'item'";
        if (transfer.Count is { } count && count < 1)
            return $"count {count} must be at least 1";
        if (transfer.From == null)
            return "'from' is missing";
        if (transfer.To == null)
            return "'to' is missing";
        if (!Transfers.IsValidActor(transfer.From))
            return BadActor("'from'", transfer.From);
        if (!Transfers.IsValidActor(transfer.To))
            return BadActor("'to'", transfer.To);
        var from = Transfers.NormalizeActor(transfer.From, ownId);
        var to = Transfers.NormalizeActor(transfer.To, ownId);
        if (from == to)
            return $"'from' and 'to' are both '{from}'; a transfer needs two different actors";
        if (transfer.Item != null &&
            (from != Transfers.Player && from != Transfers.World || to != Transfers.Player && to != Transfers.World))
            return "items only move between the player and the world; companions have no inventory of their own, those in the player's party share its inventory";
        return null;
    }

    private static string BadActor(string key, string? actor) =>
        $"{key} '{actor}' is not an actor; use \"{Transfers.Player}\", \"{Transfers.Self}\", \"{Transfers.World}\" or a companion id";

    private static bool InRelationRange(int value) => value >= ConversationRules.MinRelation && value <= ConversationRules.MaxRelation;

    private static IEnumerable<string> Both(List<string>? a, List<string>? b) =>
        (a ?? new List<string>()).Intersect(b ?? new List<string>(), StringComparer.Ordinal).Where(f => f != null);

    private static ValidationIssue Error(string code, string folder, string conversation, string message) =>
        ValidationIssue.Error(code, folder, $"Conversation '{conversation}' {message}");
}
