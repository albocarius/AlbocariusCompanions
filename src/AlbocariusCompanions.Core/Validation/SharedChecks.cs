using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>_shared/player_lines.json + its lang files (SPEC_CORE_GAPS T6). Issues use the folder "_shared".</summary>
public static class SharedChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(SharedPackage shared, IReadOnlyList<CompanionPackage> companions)
    {
        var issues = new List<ValidationIssue>();
        if (shared == null)
            return issues;

        const string folder = SharedPackage.FolderName;
        var companionIds = new HashSet<string>(
            (companions ?? new List<CompanionPackage>()).SelectMany(p => p.Dialogs?.Lines ?? new List<DialogLine>())
                .Where(l => l?.FullId != null).Select(l => l.FullId!), StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var slotsPresent = new HashSet<string>(StringComparer.Ordinal);
        var textLines = new List<LangChecks.TextLine>();

        foreach (var line in shared.Lines)
        {
            if (line == null)
                continue;

            if (!PlayerSlots.IsKnown(line.Slot))
                issues.Add(ValidationIssue.Error(IssueCodes.UnknownSlot, folder,
                    $"Player line slot '{line.Slot}' is not a known player slot (known: {string.Join(", ", PlayerSlots.All)})."));
            else
                slotsPresent.Add(line.Slot!);

            if (!LineIds.IsValidLocal(line.Id))
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidLineId, folder,
                    $"Player line id '{line.Id}' must match {LineIds.LocalIdPattern} (e.g. 'player_ask_about')."));
            else if (!ids.Add(line.Id!))
                issues.Add(ValidationIssue.Error(IssueCodes.DuplicateLineId, folder,
                    $"Player line id '{line.Id}' is used by more than one player line."));
            else if (companionIds.Contains(line.Id!))
                issues.Add(ValidationIssue.Error(IssueCodes.DuplicateLineId, folder,
                    $"Player line id '{line.Id}' equals the full id of a companion line; player ids must not collide with companion lines."));
            else
                textLines.Add(new LangChecks.TextLine(line.Id!, voiced: false));
        }

        foreach (var slot in PlayerSlots.All)
            if (!slotsPresent.Contains(slot))
                issues.Add(ValidationIssue.Error(IssueCodes.MissingRequiredSlot, folder,
                    $"Player slot '{slot}' has no line in {SharedPackage.PlayerLinesFileName}."));

        LangChecks.Validate(folder, textLines, shared.Languages, issues);
        return issues;
    }
}
