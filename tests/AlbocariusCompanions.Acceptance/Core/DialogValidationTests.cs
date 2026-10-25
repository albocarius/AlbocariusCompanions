using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Tasks 19-core-validation-dialogs, 40/41/49 (local line ids, lang files). Each test breaks exactly one rule.
public class DialogValidationTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate, Action<Dictionary<string, JObject>>? mutateLangs = null)
    {
        var d = Fixtures.Dialogs(Id);
        mutate(d);
        var langs = Fixtures.Langs();
        mutateLangs?.Invoke(langs);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static IReadOnlyList<ValidationIssue> ValidateLangs(Action<Dictionary<string, JObject>> mutateLangs) => Validate(_ => { }, mutateLangs);

    static JObject First(JObject d) => (JObject)Fixtures.Lines(d)[0];

    static JObject EnLines(Dictionary<string, JObject> langs) => Fixtures.LangLines(langs["en"]);

    static ValidationIssue SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
        Assert.Equal(Id, error.Folder);
        return error;
    }

    static ValidationIssue SingleWarning(IReadOnlyList<ValidationIssue> issues, string code)
    {
        Assert.Empty(issues.Errors());
        var warning = Assert.Single(issues);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal(code, warning.Code);
        Assert.Equal(Id, warning.Folder);
        return warning;
    }

    [Fact]
    public void Valid_dialogs_have_no_issues() => Assert.Empty(Validate(_ => { }));

    [Fact]
    public void Optional_slot_lines_are_accepted() => Assert.Empty(Validate(
        d => Fixtures.Lines(d).Add(Fixtures.Line("idle_01", "idle")),
        langs => EnLines(langs)["idle_01"] = "Quiet day, Captain."));

    [Fact]
    public void Local_ids_are_free_form_snake_case() => Assert.Empty(Validate(
        d => Fixtures.Lines(d).Add(Fixtures.Line("greeting_when_raining", "intro")),
        langs => EnLines(langs)["greeting_when_raining"] = "Wet day, Captain."));

    [Theory]
    [InlineData("Intro_02")]
    [InlineData("intro-02")]
    [InlineData("intro 02")]
    [InlineData("")]
    public void Local_id_must_be_lowercase_snake(string lineId)
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line(lineId, "intro")),
            langs => EnLines(langs)[lineId] = "Hello again, Captain.");
        SingleError(issues, IssueCodes.InvalidLineId);
    }

    [Fact]
    public void Line_id_missing()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(new JObject { ["slot"] = "intro" }));
        SingleError(issues, IssueCodes.InvalidLineId);
    }

    [Fact]
    public void Local_ids_unique_within_companion()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line("intro_01", "intro")));
        Assert.Contains("intro_01", SingleError(issues, IssueCodes.DuplicateLineId).Message);
    }

    [Fact]
    public void Full_ids_unique_across_companions()
    {
        // "a" + "b_intro_01" and "a_b" + "intro_01" both become "a_b_intro_01".
        var a = Fixtures.Dialogs("a");
        Fixtures.Lines(a).Add(Fixtures.Line("b_intro_01", "intro"));
        var aLangs = Fixtures.Langs();
        Fixtures.LangLines(aLangs["en"])["b_intro_01"] = "Clash.";

        var issues = DialogChecks.Validate(new[]
        {
            Fixtures.Package("a", Fixtures.Companion("a"), a, langs: aLangs),
            Fixtures.Package("a_b"),
        });

        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.DuplicateLineId, error.Code);
        Assert.Equal("a_b", error.Folder);
        Assert.Contains("a_b_intro_01", error.Message);
    }

    [Theory]
    [InlineData("greeting")]
    [InlineData("Intro")]
    [InlineData("ask_about")] // a player slot is not a companion slot
    public void Unknown_slot(string slot)
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line("extra_01", slot)),
            langs => EnLines(langs)["extra_01"] = "x");
        Assert.Contains(slot, SingleError(issues, IssueCodes.UnknownSlot).Message);
    }

    [Fact]
    public void Slot_missing()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(new JObject { ["id"] = "intro_02" }),
            langs => EnLines(langs)["intro_02"] = "x");
        SingleError(issues, IssueCodes.UnknownSlot);
    }

    [Theory]
    [InlineData("intro")]
    [InlineData("backstory")]
    [InlineData("hire_offer")]
    [InlineData("hire_accept")]
    [InlineData("hire_decline")]
    public void Every_required_slot_needs_a_line(string slot)
    {
        var issues = Validate(d =>
        {
            foreach (var l in Fixtures.Lines(d).Where(l => (string?)l["slot"] == slot).ToList()) l.Remove();
        }, langs => EnLines(langs).Remove(slot + "_01"));
        Assert.Contains(slot, SingleError(issues, IssueCodes.MissingRequiredSlot).Message);
    }

    [Fact]
    public void No_lines_at_all_reports_every_required_slot()
    {
        var issues = Validate(d => d.Remove("lines"), langs => EnLines(langs).RemoveAll());
        Assert.Equal(10, issues.Errors().Count(i => i.Code == IssueCodes.MissingRequiredSlot)); // 5 companion + 5 player slots
    }

    [Fact]
    public void Text_field_is_gone_from_dialogs() =>
        Assert.Null(typeof(AlbocariusCompanions.Core.Models.DialogLine).GetProperty("Text"));

    // ---- lang files ----

    [Fact]
    public void Line_missing_from_en_is_an_error_naming_the_line()
    {
        var issues = ValidateLangs(langs => EnLines(langs).Remove("backstory_01"));
        Assert.Contains("backstory_01", SingleError(issues, IssueCodes.MissingEnglishText).Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_english_text_is_an_error(string en)
    {
        var issues = ValidateLangs(langs => EnLines(langs)["intro_01"] = en);
        SingleError(issues, IssueCodes.MissingEnglishText);
    }

    [Fact]
    public void No_en_file_reports_every_line()
    {
        var issues = ValidateLangs(langs => langs.Remove("en"));
        Assert.Equal(10, issues.Errors().Count(i => i.Code == IssueCodes.MissingEnglishText));
        Assert.Equal(10, issues.Count);
    }

    [Fact]
    public void Line_missing_from_another_language_is_a_warning()
    {
        var issues = ValidateLangs(langs => langs["tr"] = Fixtures.Tr());
        Assert.Empty(issues.Errors());
        var warnings = issues.Where(i => i.Code == IssueCodes.MissingTranslation).ToList();
        Assert.Equal(9, warnings.Count); // tr has only intro_01
        Assert.Equal(9, issues.Count);
        Assert.All(warnings, w => Assert.Equal(IssueSeverity.Warning, w.Severity));
        Assert.Contains(warnings, w => w.Message.Contains("backstory_01") && w.Message.Contains("tr"));
    }

    [Fact]
    public void Translation_for_an_unknown_line_is_a_warning()
    {
        var issues = ValidateLangs(langs => EnLines(langs)["typo_01"] = "Never used.");
        Assert.Contains("typo_01", SingleWarning(issues, IssueCodes.UnknownTranslationId).Message);
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("turkish")]
    [InlineData("t")]
    [InlineData("tur")]
    [InlineData("pt-br")]
    public void Lang_file_names_must_be_iso_639_1(string code)
    {
        var issues = ValidateLangs(langs => langs[code] = Fixtures.En());
        Assert.Contains(code, SingleError(issues, IssueCodes.InvalidLanguageCode).Message);
    }

    [Theory]
    [InlineData("en", "Welcome, {PLAYER_NAME}.")]
    [InlineData("en", "Welcome, {PLAYER.NAME}.")]
    [InlineData("tr", "Hoş geldin {PLAYER_NAME}.")]
    public void Voiced_lines_must_not_use_player_tokens_in_any_language(string lang, string text)
    {
        var issues = ValidateLangs(langs =>
        {
            if (!langs.ContainsKey(lang)) langs[lang] = Fixtures.En();
            Fixtures.LangLines(langs[lang])["intro_01"] = text;
        });
        SingleError(issues, IssueCodes.PlayerTokenInVoicedLine);
    }

    [Fact]
    public void Unvoiced_lines_may_use_player_tokens()
    {
        var issues = Validate(d => First(d)["voiced"] = false,
            langs => EnLines(langs)["intro_01"] = "Welcome, {PLAYER.NAME}.");
        Assert.Empty(issues);
    }

    // ---- audio ----

    [Theory]
    [InlineData("audio/intro_01.ogg")]
    [InlineData("sub/dir/x.ogg")]
    public void Relative_audio_paths_are_fine(string audio) => Assert.Empty(Validate(d => First(d)["audio"] = audio));

    [Theory]
    [InlineData("../x.ogg")]
    [InlineData("audio/../../x.ogg")]
    [InlineData("audio\\..\\..\\x.ogg")]
    [InlineData("/abs/x.ogg")]
    [InlineData("\\abs\\x.ogg")]
    [InlineData("C:\\x.ogg")]
    [InlineData("C:/x.ogg")]
    [InlineData("")]
    public void Audio_path_must_stay_inside_the_companion_folder(string audio)
    {
        var issues = Validate(d => First(d)["audio"] = audio);
        SingleError(issues, IssueCodes.InvalidAudioPath);
    }
}
