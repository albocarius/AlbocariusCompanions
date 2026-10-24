using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Rules;

namespace AlbocariusCompanions.Core.Dialogs;

/// <summary>
/// Which lang file to use for the game's active language (GAME_MAP Q14 note 2): the first available ISO 639-1 code whose
/// game language (as the game maps ISO codes) is the active one, else English.
/// </summary>
public static class LanguagePick
{
    /// <param name="availableCodes">Lang file codes of a folder (en, tr, …).</param>
    /// <param name="activeLanguageId">The game's active language id (English, Türkçe, …).</param>
    /// <param name="gameLanguageOf">ISO code → game language id (Game: LocalizedTextManager, cached).</param>
    public static string Choose(IEnumerable<string> availableCodes, string? activeLanguageId, Func<string, string?> gameLanguageOf)
    {
        if (string.IsNullOrEmpty(activeLanguageId) || gameLanguageOf == null
            || string.Equals(gameLanguageOf(CompanionRules.EnglishCode), activeLanguageId, StringComparison.Ordinal))
            return CompanionRules.EnglishCode;

        return (availableCodes ?? Enumerable.Empty<string>())
            .Where(c => c != CompanionRules.EnglishCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .FirstOrDefault(c => string.Equals(gameLanguageOf(c), activeLanguageId, StringComparison.Ordinal))
            ?? CompanionRules.EnglishCode;
    }
}
