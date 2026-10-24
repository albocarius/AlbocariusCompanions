using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>
/// Lang file rules shared by companion folders and _shared (SPEC_CORE_GAPS T6): file names are ISO 639-1 codes; en.json
/// has a non-blank text for every line (error); another language missing a line is a warning (en is used); a text for an
/// unknown line is a warning; voiced lines never contain player-name tokens, in any language.
/// </summary>
public static class LangChecks
{
    public sealed class TextLine
    {
        public TextLine(string id, bool voiced)
        {
            Id = id;
            Voiced = voiced;
        }

        public string Id { get; }
        public bool Voiced { get; }
    }

    public static void Validate(string folder, IReadOnlyList<TextLine> lines, IReadOnlyDictionary<string, LangFile> languages,
        List<ValidationIssue> issues)
    {
        var ids = new HashSet<string>(lines.Select(l => l.Id), StringComparer.Ordinal);
        var valid = new List<KeyValuePair<string, LangFile>>();

        foreach (var language in languages.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            if (CompanionRules.LanguageCodePattern.IsMatch(language.Key))
                valid.Add(language);
            else
                issues.Add(ValidationIssue.Error(IssueCodes.InvalidLanguageCode, folder,
                    $"Lang file '{language.Key}.json' is not named with an ISO 639-1 code (two lowercase letters, e.g. en.json, tr.json)."));
        }

        languages.TryGetValue(CompanionRules.EnglishCode, out var english);
        foreach (var line in lines)
            if (Get(english, line.Id) == null)
                issues.Add(ValidationIssue.Error(IssueCodes.MissingEnglishText, folder,
                    $"Line '{line.Id}' has no text in lang/{CompanionRules.EnglishCode}.json (English is required for every line)."));

        foreach (var language in valid.Where(l => l.Key != CompanionRules.EnglishCode))
            foreach (var line in lines)
                if (Get(language.Value, line.Id) == null)
                    issues.Add(ValidationIssue.Warning(IssueCodes.MissingTranslation, folder,
                        $"Line '{line.Id}' has no text in lang/{language.Key}.json; English is shown instead."));

        foreach (var language in valid)
            foreach (var id in language.Value.Lines?.Keys ?? Enumerable.Empty<string>())
                if (!ids.Contains(id))
                    issues.Add(ValidationIssue.Warning(IssueCodes.UnknownTranslationId, folder,
                        $"lang/{language.Key}.json has a text for '{id}', but no line has that id; it is never shown."));

        // Voiced lines become recorded audio: they address the player as "Captain", never with name tokens (SPEC §2.5).
        foreach (var line in lines.Where(l => l.Voiced))
            if (valid.Any(l => Get(l.Value, line.Id) is { } text && CompanionRules.PlayerTokenPattern.IsMatch(text)))
                issues.Add(ValidationIssue.Error(IssueCodes.PlayerTokenInVoicedLine, folder,
                    $"Line '{line.Id}' is voiced but a text contains a player-name token; voiced lines must address the player as \"Captain\"."));
    }

    private static string? Get(LangFile? file, string id) =>
        file?.Lines != null && file.Lines.TryGetValue(id, out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
