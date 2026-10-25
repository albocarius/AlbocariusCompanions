using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// I5 authoring hints over the whole loaded set, run once by <see cref="Registry.CompanionCatalog"/> on the final kept
/// list (companions skipped for errors or missing modules are absent and never referenced here): a declared variable
/// nothing ever writes, and a transfer whose source is never checked to hold on the path into its node. Both are
/// warnings, never errors, so they can never skip a companion.
/// </summary>
public static class AuthoringChecks
{
    public static List<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> loaded)
    {
        var issues = new List<ValidationIssue>();
        NeverChangingVariables(loaded, issues);
        UncheckedTransfers(loaded, issues);
        return issues;
    }

    // ---- variable never changes ----

    /// <summary>
    /// A declared variable is dead when no effect in any loaded companion's conversations writes it — references are
    /// resolved, so another companion's write to "owner.name" counts for the owner — and its default is the type
    /// default, so it can never hold anything else. The built-in "mood" is never declared and never warned about.
    /// </summary>
    private static void NeverChangingVariables(IReadOnlyList<CompanionPackage> loaded, List<ValidationIssue> issues)
    {
        var written = new HashSet<(string Owner, string Name)>();
        foreach (var writer in loaded)
            foreach (var node in writer.Conversations.SelectMany(c => c.Nodes ?? new List<ConversationNode>()).Where(n => n != null))
                foreach (var use in VariableUses.InEffects(node.Effects))
                    if (VariableRules.TryParseReference(writer.Id, use.Reference, out var reference))
                        written.Add((reference.Owner, reference.Name));

        foreach (var owner in loaded)
            foreach (var pair in owner.Dialogs?.Variables ?? new Dictionary<string, VariableDeclaration>())
            {
                if (pair.Key == VariableRules.Mood || written.Contains((owner.Id, pair.Key)) || !AtTypeDefault(pair.Value))
                    continue;
                issues.Add(ValidationIssue.Warning(IssueCodes.VariableNeverChanges, owner.FolderName,
                    $"Variable '{pair.Key}' is never written and starts at its type default, so it never changes."));
            }
    }

    /// <summary>True when the default is absent or the type default (false / 0); a broken declaration or a default of
    /// the wrong type is an error checked by <see cref="VariableChecks"/> and counts as non-default here.</summary>
    private static bool AtTypeDefault(VariableDeclaration? declaration)
    {
        if (!VariableRules.TryParseType(declaration?.Type, out var type))
            return false;
        var value = declaration!.Default;
        return value == null || value.Type == JTokenType.Null ||
            VariableUses.TypeOf(value) == type && VariableUses.ValueOf(value) == 0;
    }

    // ---- transfer without a matching check ----

    /// <summary>
    /// A transfer whose "from" is not "world" should be guarded by a matching
    /// check: the node's own "when", the conversation's "when" or the "when" of any node from which the node can be
    /// reached along "next" (an ancestor; conditions are ignored when walking). A gold transfer matches a "hasGold"
    /// of the same actor (the companion's own id names "self") with min ≥ the amount, or a "minGold" when the actor
    /// is the player; an item transfer from "player" matches a "hasItem" for that item with (min ?? 1) ≥ the count.
    /// </summary>
    private static void UncheckedTransfers(IReadOnlyList<CompanionPackage> loaded, List<ValidationIssue> issues)
    {
        foreach (var package in loaded)
            foreach (var conversation in package.Conversations)
            {
                var byId = (conversation.Nodes ?? new List<ConversationNode>())
                    .Where(n => n?.Id != null)
                    .GroupBy(n => n.Id!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var parents = Parents(byId);

                foreach (var node in byId.Values)
                {
                    var transfer = Transfers.Of(node.Effects);
                    if (transfer == null || transfer.From == null || transfer.From == Transfers.World ||
                        transfer.Gold == null && transfer.Item == null || HasCheck(transfer, node, conversation, parents, package.Id))
                        continue;
                    issues.Add(ValidationIssue.Warning(IssueCodes.TransferNotChecked, package.FolderName,
                        $"Conversation '{conversation.Id ?? "(no id)"}' node '{node.Id}' transfers {Given(transfer)} from " +
                        $"'{transfer.From}' without a matching {Needed(transfer, package.Id)} on its path."));
                }
            }
    }

    /// <summary>child id → the nodes listing it in "next": structural reachability, conditions ignored.</summary>
    private static Dictionary<string, List<ConversationNode>> Parents(Dictionary<string, ConversationNode> byId)
    {
        var parents = new Dictionary<string, List<ConversationNode>>(StringComparer.Ordinal);
        foreach (var parent in byId.Values)
            foreach (var id in parent.Next ?? new List<string>())
                if (id != null && byId.TryGetValue(id, out var child))
                {
                    if (!parents.TryGetValue(child.Id!, out var list))
                        parents[child.Id!] = list = new List<ConversationNode>();
                    list.Add(parent);
                }
        return parents;
    }

    /// <summary>True when a matching check guards the node: its own "when", an ancestor's or the conversation's.</summary>
    private static bool HasCheck(TransferEffect transfer, ConversationNode node, ConversationDefinition conversation,
        Dictionary<string, List<ConversationNode>> parents, string ownId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<ConversationNode> { node };
        while (pending.Count > 0)
        {
            var current = pending[pending.Count - 1];
            pending.RemoveAt(pending.Count - 1);
            if (current.Id != null && !seen.Add(current.Id))
                continue;
            if (Matches(transfer, current.When, ownId))
                return true;
            if (current.Id != null && parents.TryGetValue(current.Id, out var ancestors))
                pending.AddRange(ancestors);
        }
        return Matches(transfer, conversation.When, ownId);
    }

    private static bool Matches(TransferEffect transfer, ConditionSet? when, string ownId)
    {
        foreach (var guard in Guards(when))
        {
            if (transfer.Gold is { } gold)
            {
                var actor = Transfers.NormalizeActor(transfer.From, ownId);
                if (guard.HasGold is { } money && (money.Min ?? 0) >= gold && Transfers.NormalizeActor(money.Who, ownId) == actor)
                    return true;
                if (actor == Transfers.Player && guard.MinGold is { } playerGold && playerGold >= gold)
                    return true;
            }
            else if (transfer.Item is { } item && transfer.From == Transfers.Player && guard.HasItem is { } goods &&
                     goods.Who == Transfers.Player && goods.Id == item && (goods.Min ?? 1) >= Transfers.CountOf(transfer))
                return true;
        }
        return false;
    }

    /// <summary>Condition objects that guarantee the transfer's source holds: the object itself and its nested "all"
    /// elements; checks inside "any" or "not" do not.</summary>
    private static IEnumerable<ConditionSet> Guards(ConditionSet? when)
    {
        if (when == null)
            yield break;
        yield return when;
        foreach (var nested in when.All ?? new List<ConditionSet>())
            foreach (var guard in Guards(nested))
                yield return guard;
    }

    /// <summary>What the node moves, for the message.</summary>
    private static string Given(TransferEffect transfer) =>
        transfer.Gold is { } gold ? $"{gold} gold" : $"{Transfers.CountOf(transfer)} {transfer.Item}";

    /// <summary>The check that would have guarded the transfer, for the message.</summary>
    private static string Needed(TransferEffect transfer, string ownId) => transfer.Gold is { } gold
        ? Transfers.NormalizeActor(transfer.From, ownId) == Transfers.Player ? $"hasGold (min {gold}) or minGold" : $"hasGold (min {gold})"
        : $"hasItem '{transfer.Item}' (min {Transfers.CountOf(transfer)})";
}
