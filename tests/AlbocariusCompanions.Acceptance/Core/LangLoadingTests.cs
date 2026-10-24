using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 49: <folder>/lang/<code>.json = { "schemaVersion": 1, "lines": { "<local id>": "<text>" } }.
public class LangLoadingTests
{
    const string Id = "test_companion";

    [Fact]
    public void Lang_files_are_loaded_by_code_and_text_falls_back_to_english()
    {
        using var tmp = new TempDir();
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), Fixtures.Dialogs(Id),
            new Dictionary<string, JObject> { ["en"] = Fixtures.En(), ["tr"] = Fixtures.Tr() });

        var pkg = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Packages);

        Assert.Equal(new[] { "en", "tr" }, pkg.Languages.Keys.OrderBy(k => k));
        Assert.Equal("Selam, Kaptan.", pkg.Text("intro_01", "tr"));
        Assert.Equal("A placeholder past.", pkg.Text("backstory_01", "tr"));
        Assert.Equal("A placeholder past.", pkg.Text("backstory_01", "de")); // no de.json at all
        Assert.Null(pkg.Text("nope_01", "en"));
    }

    [Fact]
    public void No_lang_folder_loads_with_no_languages()
    {
        using var tmp = new TempDir();
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), Fixtures.Dialogs(Id), new Dictionary<string, JObject>());

        var load = CompanionLoader.LoadAll(tmp.Path);
        Assert.Empty(load.Issues);
        Assert.Empty(Assert.Single(load.Packages).Languages);
    }

    [Fact]
    public void Non_json_files_in_lang_are_ignored()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, Id);
        File.WriteAllText(Path.Combine(tmp.Path, Id, "lang", "notes.txt"), "translator notes");

        var pkg = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Packages);
        Assert.Equal(new[] { "en" }, pkg.Languages.Keys);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "schemaVersion": 1, "lines": { "intro_01": 5 } }""")]
    [InlineData("""{ "schemaVersion": 1, "lines": [] }""")]
    [InlineData("""{ "schemaVersion": 1, "lines": {}, "extra": 1 }""")]
    public void Broken_lang_file_skips_the_companion(string content)
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.WriteValid(tmp.Path, "broken");
        File.WriteAllText(Path.Combine(tmp.Path, "broken", "lang", "en.json"), content);

        var load = CompanionLoader.LoadAll(tmp.Path);

        Assert.Equal("good", Assert.Single(load.Packages).FolderName);
        var issue = Assert.Single(load.Issues);
        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        Assert.Equal("broken", issue.Folder);
        Assert.Contains("lang/en.json", issue.Message);
    }

    [Theory]
    [InlineData(null, "missing_schema_version")]
    [InlineData(2, "unsupported_schema_version")]
    public void Lang_files_carry_a_schema_version(int? version, string code)
    {
        using var tmp = new TempDir();
        var en = Fixtures.En();
        if (version == null) en.Remove("schemaVersion"); else en["schemaVersion"] = version;
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), Fixtures.Dialogs(Id), new Dictionary<string, JObject> { ["en"] = en });

        var issue = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Issues);
        Assert.Equal(code, issue.Code);
        Assert.Contains("lang/en.json", issue.Message);
    }
}
