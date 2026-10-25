using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Turns a validated conversation into game sentences (GAME_MAP Q7, Q20). The game's dialog graph is token → token, one
/// input token per sentence, so a node is registered once per way into it: once per start (from the hub or the
/// conversation start token) and once per parent that lists it in "next". After a node the active token is the node's
/// own token, unless its "next" is "hub" or "end".
/// </summary>
public static class ConversationGraph
{
    /// <summary>The game's own tokens (GAME_MAP Q18/Q20).</summary>
    public const string StartToken = "start";
    public const string HubToken = "hero_main_options";
    /// <summary>NPC "Is there anything else?" → hub; used after a player line so the companion speaks before the menu.</summary>
    public const string PretalkToken = "lord_pretalk";
    public const string CloseToken = "close_window";

    public static IReadOnlyList<ConversationEdge> Build(string companionId, ConversationDefinition conversation)
    {
        if (conversation == null) throw new ArgumentNullException(nameof(conversation));

        var nodes = (conversation.Nodes ?? new List<ConversationNode>())
            .Where(n => n?.Id != null)
            .GroupBy(n => n.Id!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var edges = new List<ConversationEdge>();
        var sentencePrefix = TokenPrefix(companionId) + conversation.Id + ":";

        var entryToken = conversation.Entry == ConversationRules.EntryGreeting ? StartToken : HubToken;
        foreach (var id in conversation.Start ?? new List<string>())
            if (id != null && nodes.TryGetValue(id, out var node))
                edges.Add(new ConversationEdge(sentencePrefix + "start:" + id, entryToken, OutputToken(companionId, node), node,
                    isStart: true));

        foreach (var parent in nodes.Values)
            foreach (var id in parent.Next ?? new List<string>())
                if (id != null && nodes.TryGetValue(id, out var child))
                    edges.Add(new ConversationEdge(sentencePrefix + parent.Id + ":" + id, NodeToken(companionId, parent.Id!),
                        OutputToken(companionId, child), child, isStart: false));

        return edges;
    }

    /// <summary>
    /// The token after a node: node ids are unique per companion and never contain ':', so "albocarius:&lt;companion&gt;:&lt;node&gt;"
    /// cannot collide with another node, another companion or the game's tokens (DialogFlow itself uses "atk:N").
    /// </summary>
    public static string NodeToken(string companionId, string nodeId) => TokenPrefix(companionId) + nodeId;

    private static string TokenPrefix(string companionId) => "albocarius:" + companionId + ":";

    private static string OutputToken(string companionId, ConversationNode node)
    {
        var next = node.Next ?? new List<string>();
        if (next.Count == 1 && next[0] == ConversationRules.NextEnd)
            return CloseToken;
        if (next.Count == 1 && next[0] == ConversationRules.NextHub)
            return node.Speaker == ConversationRules.SpeakerPlayer ? PretalkToken : HubToken;
        return NodeToken(companionId, node.Id!);
    }
}

/// <summary>One sentence to register: <see cref="Node"/> said when the active token is <see cref="InputToken"/>.</summary>
public sealed class ConversationEdge
{
    public ConversationEdge(string sentenceId, string inputToken, string outputToken, ConversationNode node, bool isStart)
    {
        SentenceId = sentenceId;
        InputToken = inputToken;
        OutputToken = outputToken;
        Node = node;
        IsStart = isStart;
    }

    public string SentenceId { get; }
    public string InputToken { get; }
    public string OutputToken { get; }
    public ConversationNode Node { get; }
    /// <summary>True for the conversation's entry sentences: they also check the conversation's "when" and "once".</summary>
    public bool IsStart { get; }
    public bool IsPlayer => Node.Speaker == ConversationRules.SpeakerPlayer;
}
