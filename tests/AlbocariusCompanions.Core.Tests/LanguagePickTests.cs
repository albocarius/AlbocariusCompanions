using AlbocariusCompanions.Core.Dialogs;

namespace AlbocariusCompanions.Core.Tests;

public class LanguagePickTests
{
    // Vanilla supported_iso: tr → Türkçe, de → Deutsch, everything else unknown → English (the game's own fallback).
    private static string? GameLanguage(string iso) => iso switch
    {
        "tr" => "Türkçe",
        "de" => "Deutsch",
        _ => "English",
    };

    [Fact]
    public void Turkish_game_language_picks_tr() =>
        Assert.Equal("tr", LanguagePick.Choose(new[] { "en", "tr" }, "Türkçe", GameLanguage));

    [Fact]
    public void English_game_language_picks_en() =>
        Assert.Equal("en", LanguagePick.Choose(new[] { "en", "tr" }, "English", GameLanguage));

    [Fact]
    public void Language_without_a_file_falls_back_to_en() =>
        Assert.Equal("en", LanguagePick.Choose(new[] { "en", "tr" }, "Deutsch", GameLanguage));

    [Fact]
    public void Unknown_code_mapping_to_english_never_shadows_en() =>
        Assert.Equal("en", LanguagePick.Choose(new[] { "en", "xx" }, "English", GameLanguage));

    [Fact]
    public void Missing_active_language_falls_back_to_en() =>
        Assert.Equal("en", LanguagePick.Choose(new[] { "tr" }, null, GameLanguage));
}
