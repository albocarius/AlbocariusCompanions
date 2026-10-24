using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>dialogs.json + lang file rules (SPEC §2.5, SPEC_CORE_GAPS T3/T6). Returns issues, never throws.</summary>
public static class DialogChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages)
    {
        var issues = new List<ValidationIssue>();
        if (packages == null)
            return issues;

        // Full ids (<companion>_<local>) are unique globally; a clash is reported on the later folder.
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

            ValidateAudio(line, folder, issues);
        }

        foreach (var slot in DialogSlots.Required)
            if (!slotsPresent.Contains(slot))
                issues.Add(ValidationIssue.Error(IssueCodes.MissingRequiredSlot, folder,
                    $"Required dialog slot '{slot}' has no lines in dialogs.json."));

        LangChecks.Validate(folder, textLines, package.Languages, issues);
    }

    private static void ValidateAudio(DialogLine? line, string folder, List<ValidationIssue> issues)
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

    private static string LineRef(DialogLine? line) =>
        line?.FullId != null ? $"Line '{line.FullId}'" : line?.Id != null ? $"Line '{line.Id}'" : "A dialog line";
}
