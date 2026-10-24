using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Tasks 40 (reserved _shared folder) and 49 (player lines + lang files, SPEC_CORE_GAPS T6).
public class SharedLinesTests
{
    const string Shared = "_shared";

    static CatalogResult Load(Action<JObject>? mutateLines = null, Action<Dictionary<string, JObject>>? mutateLangs = null,
        params string[] companions)
    {
        using var tmp = new TempDir();
        var lines = Fixtures.PlayerLines();
        mutateLines?.Invoke(lines);
        var langs = new Dictionary<string, JObject> { ["en"] = Fixtures.PlayerEn() };
        mutateLangs?.Invoke(langs);
        Fixtures.WriteShared(tmp.Path, lines, langs);
        foreach (var id in companions.Length == 0 ? new[] { "test_companion" } : companions)
            Fixtures.WriteValid(tmp.Path, id);
        return CompanionCatalog.Load(tmp.Path);
    }

    static JArray Lines(JObject playerLines) => (JArray)playerLines["lines"]!;

    static ValidationIssue SingleSharedError(CatalogResult result, string code)
    {
        var error = Assert.Single(result.Issues.Errors());
        Assert.Equal(code, error.Code);
        Assert.Equal(Shared, error.Folder);
        return error;
    }

    [Fact]
    public void Valid_shared_folder_loads_into_the_registry()
    {
        var result = Load();

        Assert.Empty(result.Issues);
        var player = result.Registry.PlayerLines!;
        Assert.Equal(5, player.Lines.Count);
        Assert.Equal("Tell me about yourself.", player.Text("player_ask_about", "en"));
        Assert.Equal(new[] { "test_companion" }, result.Registry.Ids);
    }

    [Fact]
    public void Shared_is_never_loaded_as_a_companion()
    {
        using var tmp = new TempDir();
        Fixtures.WriteShared(tmp.Path);
        // Even with companion files inside, _shared is reserved.
        Fixtures.Write(tmp.Path, Shared, Fixtures.Companion(Shared), Fixtures.Dialogs(Shared));

        var load = CompanionLoader.LoadAll(tmp.Path);
        Assert.Empty(load.Packages);
        Assert.Empty(load.Issues);
    }

    [Fact]
    public void Player_slots_are_part_of_the_global_slot_list()
    {
        Assert.Equal(new[] { "ask_about", "ask_hire", "hire_pay", "hire_full", "hire_later" }, PlayerSlots.All);
        Assert.All(PlayerSlots.All, slot => Assert.DoesNotContain(slot, DialogSlots.All));
    }

    [Fact]
    public void Missing_shared_folder_is_an_error_but_companions_still_load()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "test_companion");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Null(result.Registry.PlayerLines);
        Assert.Equal(new[] { "test_companion" }, result.Registry.Ids);
        Assert.Equal(IssueCodes.MissingSharedFile, SingleSharedError(result, IssueCodes.MissingSharedFile).Code);
    }

    [Fact]
    public void Shared_files_need_a_schema_version()
    {
        var result = Load(lines => lines.Remove("schemaVersion"));
        SingleSharedError(result, IssueCodes.MissingSchemaVersion);
        Assert.Null(result.Registry.PlayerLines);
    }

    [Fact]
    public void Lang_files_need_a_schema_version_too()
    {
        var result = Load(mutateLangs: langs => langs["en"]["schemaVersion"] = 2);
        SingleSharedError(result, IssueCodes.UnsupportedSchemaVersion);
    }

    [Fact]
    public void Every_player_slot_needs_a_line()
    {
        var result = Load(lines => Lines(lines).Where(l => (string?)l["slot"] == "hire_full").ToList().ForEach(l => l.Remove()),
            langs => Fixtures.LangLines(langs["en"]).Remove("player_hire_full"));
        Assert.Contains("hire_full", SingleSharedError(result, IssueCodes.MissingRequiredSlot).Message);
    }

    [Theory]
    [InlineData("intro")] // a companion slot is not a player slot
    [InlineData("chat")]
    public void Unknown_player_slot(string slot)
    {
        var result = Load(lines => Lines(lines).Add(new JObject { ["id"] = "player_extra", ["slot"] = slot }),
            langs => Fixtures.LangLines(langs["en"])["player_extra"] = "x");
        Assert.Contains(slot, SingleSharedError(result, IssueCodes.UnknownSlot).Message);
    }

    [Fact]
    public void Player_ids_must_be_unique_and_snake_case()
    {
        var dup = Load(lines => Lines(lines).Add(new JObject { ["id"] = "player_ask_about", ["slot"] = "ask_about" }));
        SingleSharedError(dup, IssueCodes.DuplicateLineId);

        var bad = Load(lines => Lines(lines).Add(new JObject { ["id"] = "Player-Bad", ["slot"] = "ask_about" }),
            langs => Fixtures.LangLines(langs["en"])["Player-Bad"] = "x");
        SingleSharedError(bad, IssueCodes.InvalidLineId);
    }

    [Fact]
    public void Player_id_colliding_with_a_companion_full_id_is_an_error()
    {
        // Companion "player_ask" + local id "hire_offer_01" → full id "player_ask_hire_offer_01"; a player line with that id collides.
        var result = Load(lines => Lines(lines).Add(new JObject { ["id"] = "player_ask_hire_offer_01", ["slot"] = "ask_about" }),
            langs => Fixtures.LangLines(langs["en"])["player_ask_hire_offer_01"] = "x",
            "player_ask");

        var error = Assert.Single(result.Issues.Errors());
        Assert.Equal(IssueCodes.DuplicateLineId, error.Code);
        Assert.Contains("player_ask_hire_offer_01", error.Message);
    }

    [Fact]
    public void Player_lines_follow_the_same_text_rules()
    {
        var missingEn = Load(mutateLangs: langs => Fixtures.LangLines(langs["en"]).Remove("player_hire_later"));
        Assert.Contains("player_hire_later", SingleSharedError(missingEn, IssueCodes.MissingEnglishText).Message);

        var partialTr = Load(mutateLangs: langs => langs["tr"] = JObject.Parse("""{ "schemaVersion": 1, "lines": { "player_ask_about": "Bana kendinden bahset." } }"""));
        Assert.Empty(partialTr.Issues.Errors());
        Assert.Equal(4, partialTr.Issues.Count(i => i.Code == IssueCodes.MissingTranslation && i.Folder == Shared));
        Assert.Equal("Bana kendinden bahset.", partialTr.Registry.PlayerLines!.Text("player_ask_about", "tr"));
        Assert.Equal("Not now.", partialTr.Registry.PlayerLines!.Text("player_hire_later", "tr")); // en fallback
    }

    [Fact]
    public void Player_lines_may_use_tokens_since_they_are_never_voiced() =>
        Assert.Empty(Load(mutateLangs: langs => Fixtures.LangLines(langs["en"])["player_ask_about"] = "{PLAYER.NAME} asks: who are you?").Issues);

    [Fact]
    public void Shared_errors_do_not_skip_companions()
    {
        var result = Load(lines => lines.Remove("schemaVersion"));
        Assert.Equal(new[] { "test_companion" }, result.Registry.Ids);
    }
}
