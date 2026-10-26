using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// Companion ids used as transfer actors ("from"/"to") or as hasGold "who" (I3), over the whole loaded set like
/// <see cref="VariableChecks.CrossCompanion"/>: an id that is not a loaded companion is a warning (at runtime that
/// actor's gold and items are unknown, so a transfer from it never applies and a hasGold about it is false).
/// "player", "self" and "world" are always known; every name has already been checked to be a valid actor by
/// <see cref="ConversationChecks"/>, so this only adds warnings and never errors.
/// </summary>
public static class TransferChecks
{
    public static List<ValidationIssue> CrossCompanion(IReadOnlyList<CompanionPackage> loaded)
    {
        var issues = new List<ValidationIssue>();
        var ids = new HashSet<string>(loaded.Select(p => p.Id), StringComparer.Ordinal);
        foreach (var package in loaded)
            foreach (var (actor, where) in Actors(package))
            {
                if (actor == null || actor == Transfers.Player || actor == Transfers.Self || actor == Transfers.World ||
                    ids.Contains(actor) || !CompanionRules.IdPattern.IsMatch(actor))
                    continue;
                issues.Add(ValidationIssue.Warning(IssueCodes.MissingCompanionReference, package.FolderName,
                    $"{where}: '{actor}' is a companion that is not loaded; its gold and items are unknown, so a " +
                    "transfer from it never applies and a hasGold about it is false."));
            }
        return issues;
    }

    /// <summary>Every actor name the companion's conversations transfer from or to, or check gold of, with a location for messages.</summary>
    private static IEnumerable<(string? Actor, string Where)> Actors(CompanionPackage package)
    {
        foreach (var line in (package.Dialogs?.Lines ?? new List<DialogLine>()).Where(l => l != null))
            foreach (var check in GoldChecks(line.When))
                yield return (check.Who, $"Line '{line.Id}' hasGold");
        foreach (var conversation in package.Conversations)
        {
            var name = conversation.Id ?? "(no id)";
            foreach (var check in GoldChecks(conversation.When))
                yield return (check.Who, $"Conversation '{name}' hasGold");
            foreach (var node in (conversation.Nodes ?? new List<ConversationNode>()).Where(n => n != null))
            {
                var where = $"Conversation '{name}' node '{node.Id}'";
                foreach (var check in GoldChecks(node.When))
                    yield return (check.Who, $"{where} hasGold");
                var transfer = Transfers.Of(node.Effects);
                if (transfer == null)
                    continue;
                yield return (transfer.From, $"{where} transfer 'from'");
                yield return (transfer.To, $"{where} transfer 'to'");
            }
        }
    }

    /// <summary>Every hasGold of a condition, nested all/any/not included.</summary>
    private static IEnumerable<GoldCheck> GoldChecks(ConditionSet? when)
    {
        if (when == null)
            yield break;
        if (when.HasGold != null)
            yield return when.HasGold;
        foreach (var nested in (when.All ?? new List<ConditionSet>()).Concat(when.Any ?? new List<ConditionSet>()).Append(when.Not))
            foreach (var check in GoldChecks(nested))
                yield return check;
    }
}
