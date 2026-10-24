using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 19-core-validation-dialogs. Each test breaks exactly one rule and expects exactly that error.
public class DialogValidationTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate)
    {
        var d = Fixtures.Dialogs(Id);
        mutate(d);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d) });
    }

    static JObject First(JObject d) => (JObject)Fixtures.Lines(d)[0];

    static ValidationIssue SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
        Assert.Equal(Id, error.Folder);
        return error;
    }

    [Fact]
    public void Valid_dialogs_have_no_issues() => Assert.Empty(Validate(_ => { }));

    [Fact]
    public void Optional_slot_lines_are_accepted() =>
        Assert.Empty(Validate(d => Fixtures.Lines(d).Add(Fixtures.Line(Id + "_idle_01", "idle", "Quiet day, Captain."))));

    [Theory]
    [InlineData("test_companion_intro_1")]
    [InlineData("test_companion_intro_001")]
    [InlineData("test_companion_backstory_02")]
    [InlineData("other_intro_02")]
    [InlineData("intro_02")]
    public void Line_id_must_follow_companion_slot_nn(string lineId)
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line(lineId, "intro", "Hello again, Captain.")));
        Assert.Contains(lineId, SingleError(issues, IssueCodes.InvalidLineId).Message);
    }

    [Fact]
    public void Line_id_missing()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(new JObject { ["slot"] = "intro", ["text"] = new JObject { ["en"] = "x" } }));
        SingleError(issues, IssueCodes.InvalidLineId);
    }

    [Fact]
    public void Line_ids_unique_within_companion()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line(Id + "_intro_01", "intro", "Duplicate.")));
        Assert.Contains(Id + "_intro_01", SingleError(issues, IssueCodes.DuplicateLineId).Message);
    }

    [Fact]
    public void Line_ids_unique_across_companions()
    {
        var other = Fixtures.Dialogs("other");
        Fixtures.Lines(other).Add(Fixtures.Line(Id + "_intro_01", "intro", "Stolen line."));
        var issues = DialogChecks.Validate(new[]
        {
            Fixtures.Package(Id),
            Fixtures.Package("other", Fixtures.Companion("other"), other),
        });

        Assert.Contains(issues.Errors(), i => i.Code == IssueCodes.DuplicateLineId && i.Folder == "other");
        Assert.DoesNotContain(issues, i => i.Folder == Id);
    }

    [Theory]
    [InlineData("greeting")]
    [InlineData("Intro")]
    public void Unknown_slot(string slot)
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(Fixtures.Line(Id + "_" + slot + "_01", slot, "x")));
        Assert.Contains(slot, SingleError(issues, IssueCodes.UnknownSlot).Message);
    }

    [Fact]
    public void Slot_missing()
    {
        var issues = Validate(d => Fixtures.Lines(d).Add(new JObject { ["id"] = Id + "_intro_02", ["text"] = new JObject { ["en"] = "x" } }));
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
        });
        Assert.Contains(slot, SingleError(issues, IssueCodes.MissingRequiredSlot).Message);
    }

    [Fact]
    public void No_lines_at_all_reports_every_required_slot()
    {
        var issues = Validate(d => d.Remove("lines"));
        Assert.Equal(5, issues.Errors().Count(i => i.Code == IssueCodes.MissingRequiredSlot));
    }

    [Fact]
    public void English_text_required()
    {
        var issues = Validate(d => First(d)["text"] = new JObject { ["tr"] = "Selam, Kaptan." });
        SingleError(issues, IssueCodes.MissingEnglishText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void English_text_not_blank(string en)
    {
        var issues = Validate(d => First(d)["text"]!["en"] = en);
        SingleError(issues, IssueCodes.MissingEnglishText);
    }

    [Fact]
    public void Text_object_missing()
    {
        var issues = Validate(d => First(d).Remove("text"));
        SingleError(issues, IssueCodes.MissingEnglishText);
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("turkish")]
    [InlineData("t")]
    public void Language_keys_must_be_lowercase_codes(string key)
    {
        var issues = Validate(d => First(d)["text"]![key] = "x");
        Assert.Contains(key, SingleError(issues, IssueCodes.InvalidLanguageCode).Message);
    }

    [Theory]
    [InlineData("en", "Welcome, {PLAYER_NAME}.")]
    [InlineData("en", "Welcome, {PLAYER.NAME}.")]
    [InlineData("tr", "Hoş geldin {PLAYER_NAME}.")]
    public void Voiced_lines_must_not_use_player_tokens_in_any_language(string lang, string text)
    {
        var issues = Validate(d => First(d)["text"]![lang] = text);
        SingleError(issues, IssueCodes.PlayerTokenInVoicedLine);
    }

    [Fact]
    public void Unvoiced_lines_may_use_player_tokens()
    {
        var issues = Validate(d =>
        {
            First(d)["voiced"] = false;
            First(d)["text"]!["en"] = "Welcome, {PLAYER.NAME}.";
        });
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("audio/test_companion_intro_01.ogg")]
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
