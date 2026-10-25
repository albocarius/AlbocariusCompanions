using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 54 (M6): the player's lines live in each companion's dialogs.json (player_* slots); _shared is gone.
public class PlayerLinesTests
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

    static JObject PlayerLine(JObject dialogs, string slot) =>
        (JObject)Fixtures.Lines(dialogs).First(l => (string?)l["slot"] == slot);

    [Fact]
    public void Player_slots_are_required_slots_of_every_companion()
    {
        Assert.Equal(new[] { "player_ask_about", "player_ask_hire", "player_hire_pay", "player_hire_full", "player_hire_later" },
            PlayerSlots.All);
        Assert.All(PlayerSlots.All, slot => Assert.True(DialogSlots.IsRequired(slot)));
    }

    [Fact]
    public void Missing_player_slot_is_an_error()
    {
        var issues = Validate(d => PlayerLine(d, "player_hire_full").Remove(),
            langs => Fixtures.LangLines(langs["en"]).Remove("p_hire_full_01"));
        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.MissingRequiredSlot, error.Code);
        Assert.Contains("player_hire_full", error.Message);
    }

    [Theory]
    [InlineData("voiced")]
    [InlineData("audio")]
    public void Player_lines_are_never_voiced(string field)
    {
        var issues = Validate(d =>
        {
            var line = PlayerLine(d, "player_ask_about");
            if (field == "voiced") line["voiced"] = true;
            else line["audio"] = "audio/p.ogg";
        });
        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.VoicedPlayerLine, error.Code);
        Assert.Contains("p_ask_about_01", error.Message);
    }

    [Fact]
    public void Player_lines_may_use_name_tokens() => Assert.Empty(Validate(_ => { },
        langs => Fixtures.LangLines(langs["en"])["p_ask_about_01"] = "{PLAYER.NAME} asks: who are you?"));

    [Fact]
    public void Several_player_lines_per_slot_and_per_companion_texts()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "a_comp");
        var dialogs = Fixtures.Dialogs("b_comp");
        Fixtures.Lines(dialogs).Add(new JObject { ["id"] = "p_ask_about_02", ["slot"] = "player_ask_about" });
        var langs = Fixtures.Langs();
        Fixtures.LangLines(langs["en"])["p_ask_about_01"] = "Who are you, stranger?";
        Fixtures.LangLines(langs["en"])["p_ask_about_02"] = "What brings you here?";
        langs["tr"] = JObject.Parse("""{ "schemaVersion": 1, "lines": { "p_ask_about_01": "Kimsin sen, yabancı?" } }""");
        Fixtures.Write(tmp.Path, "b_comp", Fixtures.Companion("b_comp"), dialogs, langs);

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Empty(result.Issues.Errors());
        result.Registry.TryGet("a_comp", out var a);
        result.Registry.TryGet("b_comp", out var b);
        Assert.Equal("Tell me about yourself.", a!.Text("p_ask_about_01", "en"));
        Assert.Equal("Who are you, stranger?", b!.Text("p_ask_about_01", "en"));
        Assert.Equal("Kimsin sen, yabancı?", b.Text("p_ask_about_01", "tr"));
        Assert.Equal(2, b.Dialogs.Lines!.Count(l => l.Slot == "player_ask_about"));
    }

    [Fact]
    public void Leftover_shared_folder_is_skipped_with_a_warning()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, Id);
        var shared = Directory.CreateDirectory(Path.Combine(tmp.Path, "_shared")).FullName;
        File.WriteAllText(Path.Combine(shared, "player_lines.json"), """{ "schemaVersion": 1, "lines": [] }""");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { Id }, result.Registry.Ids);
        var warning = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.ObsoleteSharedFolder, warning.Code);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal("_shared", warning.Folder);
        Assert.DoesNotContain(CompanionLoader.LoadAll(tmp.Path).Packages, p => p.FolderName == "_shared");
    }
}
