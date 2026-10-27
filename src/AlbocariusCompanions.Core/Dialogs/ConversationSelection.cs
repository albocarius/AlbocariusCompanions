using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// The selection sets of one companion (M9 F1) and their per-talk decisions. Player-started: hub conversations are all
/// listed, ordered by priority. NPC-started / NPC side, one winner each (<see cref="Selection"/>): the greeting that
/// opens a talk, its start node, the npc reply among one "next" list, the line of a slot.
/// </summary>
public static class ConversationSelection
{
    /// <summary>The companion's hub conversations in menu order: higher priority first, ties in file order.</summary>
    public static List<ConversationDefinition> HubOrder(CompanionPackage package) =>
        Selection.Ordered(package.Conversations.Where(c => c.Entry == ConversationRules.EntryHub), c => c.Priority);

    /// <summary>Greetings that may open the talk: offered, with at least one start npc node that holds.</summary>
    public static List<ConversationDefinition> OfferedGreetings(CompanionPackage package, IConversationContext context,
        Func<string, bool> hasStarted) =>
        package.Conversations
            .Where(c => c.Entry == ConversationRules.EntryGreeting && c.Id != null
                && ConversationConditions.IsOffered(c, context, hasStarted(c.Id))
                && NpcCandidates(c, c.Start, context).Count > 0)
            .ToList();

    /// <summary>The npc nodes among <paramref name="ids"/> (a "next" list or a greeting start) whose conditions hold, in list order.</summary>
    public static List<ConversationNode> NpcCandidates(ConversationDefinition conversation, IEnumerable<string>? ids,
        IConversationContext context)
    {
        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null)
            .GroupBy(n => n.Id!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return (ids ?? Enumerable.Empty<string>())
            .Where(id => id != null && nodes.ContainsKey(id)).Select(id => nodes[id])
            .Where(n => n.Speaker == ConversationRules.SpeakerNpc && ConversationConditions.NodeHolds(n, context))
            .Distinct().ToList();
    }

    /// <summary>The lines whose condition holds, in list order.</summary>
    public static List<DialogLine> SlotCandidates(IEnumerable<DialogLine> lines, IConversationContext context) =>
        lines.Where(l => l != null && ConversationConditions.Holds(l.When, context)).ToList();

    /// <summary>The greeting that opens this talk (kept for the talk), or null when none is offered.</summary>
    public static ConversationDefinition? Greeting(CompanionPackage package, IConversationContext context, Func<string, bool> hasStarted,
        SelectionMemory memory, IRandomSource random) =>
        memory.Decide(Key(package.Id, "greeting"), OfferedGreetings(package, context, hasStarted), c => c.Priority, random);

    /// <summary>
    /// The npc node said after <paramref name="parentId"/> (null: the conversation's start) — one winner among the
    /// passing npc nodes, kept for the talk.
    /// </summary>
    public static ConversationNode? NpcReply(string companionId, ConversationDefinition conversation, string? parentId,
        IConversationContext context, SelectionMemory memory, IRandomSource random)
    {
        IEnumerable<string>? ids = conversation.Start;
        if (parentId != null)
            ids = (conversation.Nodes ?? new List<ConversationNode>()).FirstOrDefault(n => n?.Id == parentId)?.Next;
        return memory.Decide(Key(companionId, conversation.Id + "|" + (parentId ?? "start")),
            NpcCandidates(conversation, ids, context), n => n.Priority, random);
    }

    /// <summary>The slot's line for this talk among <paramref name="lines"/> (the slot's usable lines), or null.</summary>
    public static DialogLine? SlotLine(string companionId, string slot, IEnumerable<DialogLine> lines, IConversationContext context,
        SelectionMemory memory, IRandomSource random) =>
        memory.Decide(Key(companionId, "slot|" + slot), SlotCandidates(lines, context), l => l.Priority, random);

    private static string Key(string companionId, string set) => companionId + "|" + set;
}
