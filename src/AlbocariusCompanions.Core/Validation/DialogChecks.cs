using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>dialogs.json rules (SPEC §2.5). Returns issues, never throws.</summary>
public static class DialogChecks
{
    public static IReadOnlyList<ValidationIssue> Validate(IReadOnlyList<CompanionPackage> packages)
    {
        var issues = new List<ValidationIssue>();
        if (packages == null)
            return issues;

        // Line ids are unique globally: within a companion's lines and across all packages.
        var seenLineIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var package in packages)
            ValidatePackage(package, seenLineIds, issues);

        return issues;
    }

    private static void ValidatePackage(CompanionPackage package, HashSet<string> seenLineIds, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var lines = package.Dialogs?.Lines;
        var slotsPresent = new HashSet<string>(StringComparer.Ordinal);

        if (lines != null)
            foreach (var line in lines)
                ValidateLine(package, line, slotsPresent, seenLineIds, issues);

        foreach (var slot in DialogSlots.Required)
            if (!slotsPresent.Contains(slot))
                issues.Add(ValidationIssue.Error(IssueCodes.MissingRequiredSlot, folder,
                    $"Required dialog slot '{slot}' has no lines in dialogs.json."));
    }

    private static void ValidateLine(
        CompanionPackage package, DialogLine? line, HashSet<string> slotsPresent, HashSet<string> seenLineIds, List<ValidationIssue> issues)
    {
        var folder = package.FolderName;
        var slot = line?.Slot;

        if (!DialogSlots.IsKnown(slot))
        {
            issues.Add(ValidationIssue.Error(IssueCodes.UnknownSlot, folder, slot == null
                ? "A dialog line is missing 'slot'."
                : $"Dialog slot '{slot}' is not a known slot (known: {string.Join(", ", DialogSlots.All)})."));
        }
        else
        {
            slotsPresent.Add(slot!);
            if (line!.Id == null || !LineIds.IsValid(line.Id, package.Id, slot!))
            {
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidLineId, folder, line.Id == null
                    ? $"A dialog line in slot '{slot}' is missing 'id' (expected e.g. '{LineIds.Format(package.Id, slot!, 1)}')."
                    : $"Line id '{line.Id}' does not follow <companionId>_<slot>_<nn> (expected prefix '{package.Id}_{slot}_' and a two-digit number)."));
            }
        }

        if (line?.Id != null && !seenLineIds.Add(line.Id))
            issues.Add(ValidationIssue.Error(IssueCodes.DuplicateLineId, folder,
                $"Line id '{line.Id}' is used by more than one dialog line."));

        ValidateText(line, folder, issues);
        ValidateAudio(line, folder, issues);
    }

    private static void ValidateText(DialogLine? line, string folder, List<ValidationIssue> issues)
    {
        var text = line?.Text;
        if (text == null)
        {
            issues.Add(ValidationIssue.Error(IssueCodes.MissingEnglishText, folder,
                LineRef(line) + " has no 'text' object (it must contain a non-empty 'en' text)."));
            return;
        }

        if (!text.TryGetValue(CompanionRules.EnglishCode, out var english) || string.IsNullOrWhiteSpace(english))
            issues.Add(ValidationIssue.Error(IssueCodes.MissingEnglishText, folder,
                LineRef(line) + " is missing a non-empty 'en' text."));

        foreach (var entry in text)
            if (!CompanionRules.LanguageCodePattern.IsMatch(entry.Key))
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidLanguageCode, folder,
                    LineRef(line) + $" has language key '{entry.Key}' which is not a lowercase language code (expected e.g. en, tr, pt-br)."));

        // Voiced lines become recorded audio: they address the player as "Captain", never with name tokens (SPEC §2.5).
        if (line != null && line.Voiced)
            foreach (var value in text.Values)
                if (value != null && CompanionRules.PlayerTokenPattern.IsMatch(value))
                {
                    issues.Add(ValidationIssue.Error(IssueCodes.PlayerTokenInVoicedLine, folder,
                        LineRef(line) + " is voiced but a text contains a player-name token; voiced lines must address the player as \"Captain\"."));
                    break;
                }
    }

    private static void ValidateAudio(DialogLine? line, string folder, List<ValidationIssue> issues)
    {
        var audio = line?.Audio;
        if (audio == null)
            return;

        if (string.IsNullOrWhiteSpace(audio) || IsRooted(audio) || HasParentSegment(audio))
            issues.Add(ValidationIssue.Error(IssueCodes.InvalidAudioPath, folder,
                LineRef(line) + $" audio path '{audio}' is invalid: it must be a relative path inside the companion folder (no root, no drive, no '..')."));
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
        line?.Id != null ? $"Line '{line.Id}'" : "A dialog line";
}
