using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Core.Validation;

/// <summary>Per-language count of missing translations, for one log line per language at load (SPEC_CORE_GAPS T6).</summary>
public static class MissingTranslations
{
    private static readonly Regex LangFile = new Regex(@"lang/([a-z]{2})\.json");

    /// <summary>Language code → number of lines without a text in that language, ordered by code.</summary>
    public static IReadOnlyList<KeyValuePair<string, int>> CountByLanguage(IEnumerable<ValidationIssue> issues) =>
        (issues ?? Enumerable.Empty<ValidationIssue>())
            .Where(i => i.Code == IssueCodes.MissingTranslation)
            .Select(i => LangFile.Match(i.Message))
            .Where(m => m.Success)
            .GroupBy(m => m.Groups[1].Value, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
            .ToList();
}
