using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// Shadowing (M9 I2): inside one selection set a candidate that always holds and has a strictly higher priority than
/// another candidate always wins, so that one is never chosen. Equal priorities are fine (random). Hub conversations are
/// not a selection set (all are listed).
/// </summary>
public static class SelectionChecks
{
    public static void Validate(CompanionPackage package, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;

        var greetings = package.Conversations.Where(c => c.Entry == ConversationRules.EntryGreeting).ToList();
        Report(greetings, c => c.Priority, AlwaysOffered, c => $"greeting '{c.Id}'", "Greetings", folder, issues);

        foreach (var conversation in package.Conversations)
        {
            var byId = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null)
                .GroupBy(n => n.Id!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            List<ConversationNode> Npc(IEnumerable<string>? ids) => (ids ?? Enumerable.Empty<string>())
                .Where(id => id != null && byId.ContainsKey(id)).Select(id => byId[id])
                .Where(n => n.Speaker == ConversationRules.SpeakerNpc).Distinct().ToList();

            var where = $"Conversation '{conversation.Id}'";
            if (conversation.Entry == ConversationRules.EntryGreeting)
                Report(Npc(conversation.Start), n => n.Priority, n => !ConversationChecks.HasCondition(n), NodeName,
                    $"{where} start", folder, issues);
            foreach (var parent in byId.Values)
                Report(Npc(parent.Next), n => n.Priority, n => !ConversationChecks.HasCondition(n), NodeName,
                    $"{where} replies after '{parent.Id}'", folder, issues);
        }

        foreach (var slot in (package.Dialogs?.Lines ?? new List<DialogLine>()).Where(l => l?.Slot != null)
                     .GroupBy(l => l.Slot!, StringComparer.Ordinal))
            Report(slot.ToList(), l => l.Priority, l => !ConversationChecks.IsSet(l.When), l => $"line '{l.Id}'",
                $"Slot '{slot.Key}'", folder, issues);
    }

    /// <summary>A greeting is always offered when nothing at its level can fail and one of its start nodes always holds.</summary>
    private static bool AlwaysOffered(ConversationDefinition conversation)
    {
        if (conversation.Once || ConversationChecks.IsSet(conversation.When))
            return false;
        var nodes = (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n?.Id != null).ToList();
        return (conversation.Start ?? new List<string>()).Any(id => nodes.Any(n => n.Id == id
            && n.Speaker == ConversationRules.SpeakerNpc && !ConversationChecks.HasCondition(n)));
    }

    private static string NodeName(ConversationNode node) => $"node '{node.Id}'";

    private static void Report<T>(IReadOnlyList<T> set, Func<T, int?> priority, Func<T, bool> alwaysHolds, Func<T, string> name,
        string where, string folder, List<ValidationIssue> issues)
    {
        foreach (var winner in set.Where(alwaysHolds))
        {
            var top = Selection.PriorityOf(priority(winner));
            foreach (var hidden in set.Where(c => Selection.PriorityOf(priority(c)) < top))
                issues.Add(ValidationIssue.Warning(IssueCodes.ShadowedCandidate, folder,
                    $"{where}: {name(winner)} has no conditions and priority {top}, so {name(hidden)} " +
                    $"(priority {Selection.PriorityOf(priority(hidden))}) is never chosen. Give {name(winner)} a condition or equal priorities."));
        }
    }
}
