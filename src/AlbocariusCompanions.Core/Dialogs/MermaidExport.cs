using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Renders the authored conversations (dialogs.json "conversations", SCHEMA.md) as Mermaid flowcharts, for docs and
/// debugging: one block per conversation in file order, node texts in English, conditions and effects as labels.
/// </summary>
public static class MermaidExport
{
    /// <summary>
    /// The flowchart of every conversation of the package in file order, or of the single one named by
    /// <paramref name="conversationId"/>; an unknown id throws.
    /// </summary>
    public static string Render(CompanionPackage package, string? conversationId = null)
    {
        if (package == null) throw new ArgumentNullException(nameof(package));

        var conversations = package.Conversations;
        if (conversationId != null)
        {
            conversations = conversations.Where(c => c.Id == conversationId).ToList();
            if (conversations.Count == 0)
                throw new ArgumentException($"Unknown conversation \"{conversationId}\" of companion {package.Id}.", nameof(conversationId));
        }

        var lines = new List<string> { "flowchart TD" };
        foreach (var conversation in conversations)
            RenderConversation(lines, package, conversation);
        lines.Add("  classDef player fill:#e8f0fe,stroke:#4a6fa5");
        lines.Add("  classDef npc fill:#fdf2e3,stroke:#b07a2a");
        return string.Join("\n", lines) + "\n";
    }

    private static void RenderConversation(List<string> lines, CompanionPackage package, ConversationDefinition conversation)
    {
        var cid = conversation.Id ?? "";
        string NodeId(string localId) => cid + "__" + localId;

        var entry = $"{cid} ({conversation.Entry}{(conversation.Once ? ", once" : "")})";
        var entryWhen = Describe(conversation.When);
        if (entryWhen.Length > 0)
            entry += "<br/>when: " + entryWhen;
        lines.Add($"  {NodeId("entry")}([\"{Quote(entry)}\"])");

        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null).ToList();
        var byId = new Dictionary<string, ConversationNode>(StringComparer.Ordinal);
        var players = new List<string>();
        var npcs = new List<string>();
        foreach (var node in nodes)
        {
            if (byId.ContainsKey(node.Id!))
                continue;
            byId[node.Id!] = node;

            var label = $"{node.Id}: {package.Text(node.Id!, CompanionRules.EnglishCode) ?? "(no text)"}";
            var effects = Describe(node.Effects);
            if (effects.Length > 0)
                label += "<br/>effects: " + effects;
            var isPlayer = node.Speaker == ConversationRules.SpeakerPlayer;
            lines.Add($"  {NodeId(node.Id!)}{(isPlayer ? "[/" : "[")}\"{Quote(label)}\"{(isPlayer ? "/]" : "]")}");
            (isPlayer ? players : npcs).Add(NodeId(node.Id!));
        }

        foreach (var start in conversation.Start ?? new List<string>())
            if (start != null)
                lines.Add(Arc(NodeId("entry"), start, NodeId(start), byId));
        foreach (var node in nodes)
            foreach (var target in node.Next ?? new List<string>())
                if (target != null)
                    lines.Add(Arc(NodeId(node.Id!), target, NodeId(target), byId));

        var nexts = new HashSet<string>(nodes.SelectMany(n => n.Next ?? new List<string>()));
        if (nexts.Contains(ConversationRules.NextHub))
            lines.Add($"  {NodeId(ConversationRules.NextHub)}((\"{ConversationRules.NextHub}\"))");
        if (nexts.Contains(ConversationRules.NextEnd))
            lines.Add($"  {NodeId(ConversationRules.NextEnd)}((\"{ConversationRules.NextEnd}\"))");

        if (players.Count > 0)
            lines.Add("  class " + string.Join(",", players) + " player");
        if (npcs.Count > 0)
            lines.Add("  class " + string.Join(",", npcs) + " npc");
    }

    /// <summary>
    /// An edge from a node into <paramref name="toId"/> (the id of <paramref name="targetId"/>, a node of this
    /// conversation or the hub / end terminal), labelled with the target node's condition when it has one.
    /// </summary>
    private static string Arc(string from, string targetId, string toId, Dictionary<string, ConversationNode> byId)
    {
        var label = "";
        if (byId.TryGetValue(targetId, out var node))
        {
            var when = Describe(node.When);
            if (when.Length > 0)
                label = $"|\"{Quote(when)}\"|";
        }
        return $"  {from} -->{label} {toId}";
    }

    /// <summary>A text inside a quoted label; every quote becomes the Mermaid entity #quot;.</summary>
    private static string Quote(string text) => text.Replace("\"", "#quot;");

    /// <summary>The condition as one compact text (parts joined with " and "); empty object → empty string.</summary>
    public static string Describe(ConditionSet? when)
    {
        if (when == null)
            return "";
        var parts = new List<string>();
        AddState(parts, "met", when.Met);
        AddState(parts, "hired", when.Hired);
        AddState(parts, "inParty", when.InParty);
        if (when.MinRelation is { } minRelation)
            parts.Add($"relation >= {minRelation}");
        if (when.MaxRelation is { } maxRelation)
            parts.Add($"relation <= {maxRelation}");
        if (when.MinGold is { } minGold)
            parts.Add($"gold >= {minGold}");
        foreach (var flag in when.Flags ?? new List<string>())
            parts.Add(flag);
        foreach (var flag in when.NotFlags ?? new List<string>())
            parts.Add("!" + flag);
        if (when.Var != null)
        {
            if (when.Min is { } min)
                parts.Add($"{when.Var} >= {min}");
            if (when.Max is { } max)
                parts.Add($"{when.Var} <= {max}");
            if (when.Eq != null)
                parts.Add($"{when.Var} = {Value(when.Eq)}");
        }
        if (when.HasGold != null)
            parts.Add($"{when.HasGold.Who} gold >= {when.HasGold.Min ?? 0}");
        if (when.HasItem != null)
            parts.Add($"{when.HasItem.Who} has {when.HasItem.Min ?? 1} {when.HasItem.Id}");
        if (when.All != null)
            parts.Add("(" + string.Join(" and ", when.All.Select(Describe)) + ")");
        if (when.Any != null)
            parts.Add("(" + string.Join(" or ", when.Any.Select(Describe)) + ")");
        if (when.Not != null)
            parts.Add("not (" + Describe(when.Not) + ")");
        return string.Join(" and ", parts);
    }

    /// <summary>The effects as one compact text (parts joined with ", "); empty object → empty string.</summary>
    public static string Describe(EffectSet? effects)
    {
        if (effects == null)
            return "";
        var parts = new List<string>();
        if (effects.Relation is { } relation)
            parts.Add(Signed("relation", relation));
        if (effects.Gold is { } gold)
            parts.Add(Signed("gold", gold));
        if (effects.Transfer is { } transfer)
            parts.Add(transfer.Gold is { } transferGold
                ? $"{transferGold} gold {transfer.From} -> {transfer.To}"
                : $"{transfer.Count ?? 1} {transfer.Item} {transfer.From} -> {transfer.To}");
        foreach (var flag in effects.SetFlags ?? new List<string>())
            parts.Add(flag + " = true");
        foreach (var flag in effects.ClearFlags ?? new List<string>())
            parts.Add(flag + " = false");
        foreach (var pair in effects.Set ?? new Dictionary<string, JToken>())
            parts.Add($"{pair.Key} = {Value(pair.Value)}");
        foreach (var pair in effects.Add ?? new Dictionary<string, int>())
            parts.Add($"{pair.Key} += {pair.Value}");
        if (effects.Join)
            parts.Add("join");
        return string.Join(", ", parts);
    }

    private static void AddState(List<string> parts, string name, bool? value)
    {
        if (value is { } holds)
            parts.Add(holds ? name : "!" + name);
    }

    private static string Signed(string name, int value) => name + (value >= 0 ? " +" : " ") + value;

    /// <summary>A JSON bool or int as written: true / false / a number.</summary>
    private static string Value(JToken value) => value.Type == JTokenType.Boolean ? (value.Value<bool>() ? "true" : "false") : value.ToString();
}
