using System;
using System.Collections.Generic;
using System.Linq;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Rules;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace AlbocariusCompanions.Game;

/// <summary>
/// Text of a companion line at the moment it is shown: the slot's chosen line (selection rule, M9) in the active game
/// language with English fallback (T6), as <c>{=&lt;full id&gt;}&lt;text&gt;</c> so voice-over finds it (GAME_MAP Q14 note 2).
/// </summary>
internal static class DialogText
{
    /// <summary>The game's random generator for every selection (slot lines, greetings, npc replies).</summary>
    public static readonly IRandomSource Random = new MBRandomSource();

    private static readonly Dictionary<string, string?> GameLanguageByIso = new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>The slot's lines that have a full id and an English text (validation guarantees both).</summary>
    public static List<DialogLine> LinesOf(CompanionPackage package, string slot) =>
        (package.Dialogs?.Lines ?? new List<DialogLine>())
            .Where(l => l?.Slot == slot && l.FullId != null && package.Text(l.Id!, CompanionRules.EnglishCode) != null)
            .ToList();

    /// <summary>
    /// The slot's line for this talk (companion or player slot) as a TextObject, or null when no line holds: lines whose
    /// "when" holds for the companion's hero, chosen by priority, ties at random, kept for the talk (M9 F1/F2/F4). Without
    /// a hero only lines without "when" are candidates.
    /// </summary>
    public static TextObject? PickText(CompanionPackage package, string slot)
    {
        var lines = LinesOf(package, slot);
        var hero = ModRuntime.HeroOf(package.Id) ?? Hero.OneToOneConversationHero;
        var line = hero != null
            ? ConversationSelection.SlotLine(package.Id, slot, lines, new HeroConversationContext(hero, package.Id), ModRuntime.Selections, Random)
            : Selection.Pick(lines.Where(l => l.When == null), l => l.Priority, Random);
        return line == null ? null : TextOf(package, line);
    }

    /// <summary>
    /// Text of one line or conversation node in the active language, keyed by its full id (voice-over finds voiced ones;
    /// player lines are never voiced). Player slot lines are picked like NPC ones, through <see cref="PickText"/>.
    /// </summary>
    public static TextObject? TextOf(CompanionPackage package, ISpokenLine line)
    {
        if (line.Id == null || line.FullId == null)
            return null;
        var text = package.Text(line.Id, LanguageCode(package.Languages.Keys));
        return text == null ? null : new TextObject("{=" + line.FullId + "}" + text);
    }

    /// <summary>The lang file code for the game's active language (Q14 note 2), else "en".</summary>
    private static string LanguageCode(IEnumerable<string> available) =>
        LanguagePick.Choose(available, MBTextManager.ActiveTextLanguage, GameLanguageOf);

    /// <summary>ISO code → game language id, cached: the game asserts on unknown codes, so each is asked once per session.</summary>
    private static string? GameLanguageOf(string isoCode)
    {
        if (!GameLanguageByIso.TryGetValue(isoCode, out var language))
        {
            language = LocalizedTextManager.GetLocalizationCodeOfISOLanguageCode(isoCode);
            GameLanguageByIso[isoCode] = language;
        }
        return language;
    }

    /// <summary>Slot variation through the game's random generator (GAME_MAP Q1 MBRandom).</summary>
    private sealed class MBRandomSource : IRandomSource
    {
        public int Next(int maxExclusive) => MBRandom.RandomInt(maxExclusive);
    }
}
