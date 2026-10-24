using System;
using System.Collections.Generic;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Models;

/// <summary>Text lookup over a folder's lang files: the requested language, else English (SPEC_CORE_GAPS T6).</summary>
public static class LangText
{
    public static string? Find(IReadOnlyDictionary<string, LangFile> languages, string lineId, string languageCode)
    {
        if (languages == null || string.IsNullOrEmpty(lineId))
            return null;

        return Lookup(languages, languageCode, lineId) ?? Lookup(languages, CompanionRules.EnglishCode, lineId);
    }

    private static string? Lookup(IReadOnlyDictionary<string, LangFile> languages, string? code, string lineId)
    {
        if (code == null || !languages.TryGetValue(code, out var file) || file?.Lines == null)
            return null;
        return file.Lines.TryGetValue(lineId, out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    }

    public static IReadOnlyDictionary<string, LangFile> Empty { get; } =
        new Dictionary<string, LangFile>(StringComparer.Ordinal);
}
