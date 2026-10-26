using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// dialogs.json + lang file rules (SPEC §2.5, SPEC_CORE_GAPS T3/T6, M6): slot lines (the companion's and the player's),
/// conversations (<see cref="ConversationChecks"/>), then texts and audio of every line and node. Returns issues, never throws.
/// </summary>
public static class DialogChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages)
    {
        var issues = new List<ValidationIssue>();
        if (packages == null)
            return issues;

        // Full ids (<companion>_<local>) are unique globally (lines and conversation nodes); a clash is reported on the later folder.
        var seenFullIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var package in packages)
            ValidatePackage(package, seenFullIds, issues);

        return issues;
    }

    private static void ValidatePackage(CompanionPackage package, HashSet<string> seenFullIds, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var lines = package.Dialogs?.Lines ?? new List<DialogLine>();
        var slotsPresent = new HashSet<string>(StringComparer.Ordinal);
        var localIds = new HashSet<string>(StringComparer.Ordinal);
        var textLines = new List<LangChecks.TextLine>();

        foreach (var line in lines)
        {
            if (line == null)
            {
                issues.Add(ValidationIssue.Error(IssueCodes.UnknownSlot, folder, "dialogs.json contains an empty (null) line."));
                continue;
            }

            var isPlayer = PlayerSlots.IsKnown(line.Slot);
            if (!DialogSlots.IsKnown(line.Slot))
                issues.Add(ValidationIssue.Error(IssueCodes.UnknownSlot, folder, line.Slot == null
                    ? LineRef(line) + " is missing 'slot'."
                    : $"Dialog slot '{line.Slot}' is not a known companion slot (known: {string.Join(", ", DialogSlots.All)})."));
            else
                slotsPresent.Add(line.Slot!);

            if (!LineIds.IsValidLocal(line.Id))
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidLineId, folder, line.Id == null
                    ? $"A dialog line in slot '{line.Slot}' is missing 'id' (a local id such as 'intro_01')."
                    : $"Line id '{line.Id}' must match {LineIds.LocalIdPattern} (lowercase letters, digits, '_'; e.g. 'intro_01')."));
            else if (!localIds.Add(line.Id!))
                issues.Add(ValidationIssue.Error(IssueCodes.DuplicateLineId, folder,
                    $"Line id '{line.Id}' is used by more than one dialog line of this companion."));
            else
            {
                if (!seenFullIds.Add(line.FullId!))
                    issues.Add(ValidationIssue.Error(IssueCodes.DuplicateLineId, folder,
                        $"Line '{line.Id}' has the full id '{line.FullId}', which another companion's line already uses."));
                textLines.Add(new LangChecks.TextLine(line.Id!, line.Voiced));
            }

            ConversationChecks.ValidateConditions(line.When, LineRef(line) + " 'when'",
                message => issues.Add(ValidationIssue.Error(IssueCodes.InvalidCondition, folder, message)));

            if (isPlayer && (line.Voiced || line.Audio != null))
                issues.Add(ValidationIssue.Error(IssueCodes.VoicedPlayerLine, folder,
                    LineRef(line) + $" is in the player slot '{line.Slot}'; the player is never voiced (remove 'voiced'/'audio')."));
            else
                ValidateAudio(line, folder, issues);
        }

        // A slot whose every line has a condition can be empty at runtime: a mod slot is then not offered, a vanilla
        // point falls back to vanilla text (reported in game).
        foreach (var slot in lines.Where(l => l?.Slot != null && DialogSlots.IsKnown(l.Slot)).GroupBy(l => l.Slot!, StringComparer.Ordinal))
            if (slot.All(l => ConversationChecks.IsSet(l.When)))
                issues.Add(ValidationIssue.Warning(IssueCodes.SlotMayHaveNoLine, folder,
                    $"Every line of slot '{slot.Key}' has a 'when'; when none holds the slot has no line. Add one line without 'when'."));

        // One error listing every missing slot, so a companion made before a slot existed gives one readable log line.
        var missing = DialogSlots.Required.Where(slot => !slotsPresent.Contains(slot)).ToList();
        if (missing.Count > 0)
            issues.Add(ValidationIssue.Error(IssueCodes.MissingRequiredSlot, folder,
                $"dialogs.json has no lines for {missing.Count} required dialog slot(s): {string.Join(", ", missing)}. " +
                "Every slot of tools/companion-template/dialogs.json is required."));

        foreach (var node in ConversationChecks.Validate(package, localIds, seenFullIds, issues))
        {
            textLines.Add(new LangChecks.TextLine(node.Id!, node.Voiced));
            if (node.Speaker != ConversationRules.SpeakerPlayer)
                ValidateAudio(node, folder, issues);
        }

        LangChecks.Validate(folder, textLines, package.Languages, issues);
    }

    private static void ValidateAudio(ISpokenLine? line, string folder, List<ValidationIssue> issues)
    {
        var audio = line?.Audio;
        if (audio == null)
            return;

        if (string.IsNullOrWhiteSpace(audio) || IsRooted(audio) || HasParentSegment(audio))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidAudioPath, folder,
                LineRef(line) + $" audio path '{audio}' is invalid: it must be a relative path inside the companion folder (no root, no drive, no '..')."));
            return;
        }

        if (!HasOggExtension(audio))
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidAudioExtension, folder,
                LineRef(line) + $" audio path '{audio}' must end in .ogg: every audio file is an OGG file."));
    }

    /// <summary>Extension of the last path segment, any case. A bare ".ogg" name counts as having no extension.</summary>
    private static bool HasOggExtension(string path)
    {
        var separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var fileName = path.Substring(separator + 1);
        var dot = fileName.LastIndexOf('.');
        return dot > 0 && fileName.Substring(dot).Equals(".ogg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rooted on any platform: '/x', '\x' or a drive-qualified path like 'C:\x' or 'C:/x'.</summary>
    private static bool IsRooted(string path) =>
        (path.Length > 0 && (path[0] == '/' || path[0] == '\\')) ||
        (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':');

    private static bool HasParentSegment(string path)
    {
        foreach (var segment in path.Split('/', '\\'))
            if (segment == "..")
                return true;
        return false;
    }

    private static string LineRef(ISpokenLine? line) =>
        line?.FullId != null ? $"Line '{line.FullId}'" : line?.Id != null ? $"Line '{line.Id}'" : "A dialog line";
}
