using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Rules;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Tests;

public class CompanionCatalogTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    private static string CompanionJson(string id, string voice = "earnest") => $$"""
    { "schemaVersion": 1, "name": "Test Companion", "isFemale": false, "voice": "{{voice}}", "age": 32,
      "culture": "empire",
      "attributes": { "vigor": 4, "control": 3, "endurance": 4, "cunning": 2, "social": 2, "intelligence": 2 },
      "skills": { "OneHanded": 100 }, "equipment": { "templateId": "npc_wanderer_equipment_template_empire" },
      "hireCost": 500 }
    """;

    private static string DialogsJson(string id)
    {
        var slots = DialogSlots.Required;
        var lines = string.Join(",", slots.Select(slot =>
            $$"""{ "id": "{{slot}}_01", "slot": "{{slot}}", "voiced": false }"""));
        return $$"""{ "schemaVersion": 1, "lines": [{{lines}}] }""";
    }

    private static string EnJson() =>
        $$"""{ "schemaVersion": 1, "lines": { {{string.Join(", ", DialogSlots.Required.Select(slot => $"\"{slot}_01\": \"Greetings, Captain.\""))}} } }""";

    /// <summary>Writes a companion folder (with lang/en.json) and, once per root, a valid _shared folder.</summary>
    private static void Write(string root, string folder, string? companionJson, string? dialogsJson)
    {
        var dir = Directory.CreateDirectory(Path.Combine(root, folder)).FullName;
        if (companionJson != null) File.WriteAllText(Path.Combine(dir, "companion.json"), companionJson);
        if (dialogsJson != null) File.WriteAllText(Path.Combine(dir, "dialogs.json"), dialogsJson);
        Directory.CreateDirectory(Path.Combine(dir, "lang"));
        File.WriteAllText(Path.Combine(dir, "lang", "en.json"), EnJson());
        WriteShared(root);
    }

    private static void WriteShared(string root)
    {
        var dir = Path.Combine(root, "_shared");
        if (Directory.Exists(dir)) return;
        Directory.CreateDirectory(Path.Combine(dir, "lang"));
        File.WriteAllText(Path.Combine(dir, "player_lines.json"), $$"""{ "schemaVersion": 1, "lines": [{{string.Join(",",
            PlayerSlots.All.Select(slot => $$"""{ "id": "player_{{slot}}", "slot": "{{slot}}" }"""))}}] }""");
        File.WriteAllText(Path.Combine(dir, "lang", "en.json"), $$"""{ "schemaVersion": 1, "lines": { {{string.Join(", ",
            PlayerSlots.All.Select(slot => $"\"player_{slot}\": \"Player says.\""))}} } }""");
    }

    private static void WriteValid(string root, string id) => Write(root, id, CompanionJson(id), DialogsJson(id));

    [Fact]
    public void Loader_issues_come_before_validation_issues()
    {
        using var tmp = new TempDir();
        Write(tmp.Path, "badjson", "{ not json", DialogsJson("badjson"));
        Write(tmp.Path, "badvoice", CompanionJson("badvoice", voice: "angry"), DialogsJson("badvoice"));
        WriteValid(tmp.Path, "good");

        var codes = CompanionCatalog.Load(tmp.Path).Issues.Select(i => i.Code).ToList();

        Assert.Equal(new[] { IssueCodes.InvalidJson, IssueCodes.InvalidVoice }, codes);
    }

    [Fact]
    public void Warning_only_companion_is_in_the_registry_and_its_warning_is_reported()
    {
        using var tmp = new TempDir();
        Write(tmp.Path, "a", CompanionJson("a").TrimEnd('}') + ", \"relations\": [ { \"companion\": \"future_one\", \"attitude\": \"friend\" } ] }", DialogsJson("a"));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "a" }, result.Registry.Ids);
        var warning = Assert.Single(result.Issues);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Equal(IssueCodes.UnknownRelationTarget, warning.Code);
        Assert.Equal("a", warning.Folder);
    }

    [Fact]
    public void Missing_root_and_null_root_give_an_empty_registry_without_throwing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "albocarius-no-" + Guid.NewGuid());

        Assert.Equal(0, CompanionCatalog.Load(missing).Registry.Count);
        Assert.Equal(0, CompanionCatalog.Load(null!).Registry.Count);
        Assert.Equal(IssueCodes.RootNotFound, Assert.Single(CompanionCatalog.Load(missing).Issues).Code);
    }

    [Fact]
    public void Empty_root_gives_empty_registry_and_only_the_missing_shared_folder()
    {
        using var tmp = new TempDir();

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(0, result.Registry.Count);
        Assert.Equal(IssueCodes.MissingSharedFile, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Folder_without_dialogs_json_is_skipped_and_reported()
    {
        using var tmp = new TempDir();
        Write(tmp.Path, "nodialogs", CompanionJson("nodialogs"), null);
        WriteValid(tmp.Path, "good");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "good" }, result.Registry.Ids);
        Assert.Equal(new[] { IssueCodes.MissingDialogsFile }, result.Issues.Select(i => i.Code));
    }

    [Fact]
    public void Every_error_for_a_skipped_folder_is_reported_and_others_are_untouched()
    {
        using var tmp = new TempDir();
        Write(tmp.Path, "bad",
            """{ "schemaVersion": 1, "name": "Bad", "isFemale": false, "voice": "curt", "age": 5, """
            + """ "culture": "empire", "attributes": { "vigor": 4, "control": 3, "endurance": 4, """
            + """ "cunning": 2, "social": 2, "intelligence": 2, "luck": 1 }, "skills": {}, """
            + """ "equipment": { "templateId": "t" }, "hireCost": 0 }""",
            DialogsJson("bad"));
        WriteValid(tmp.Path, "good");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "good" }, result.Registry.Ids);
        var badIssues = result.Issues.Where(i => i.Folder == "bad").ToList();
        Assert.Equal(2, badIssues.Count);
        Assert.Contains(badIssues, i => i.Code == IssueCodes.OutOfRange && i.Message.Contains("age"));
        Assert.Contains(badIssues, i => i.Code == IssueCodes.UnknownAttribute && i.Message.Contains("luck"));
    }

    [Fact]
    public void Registry_packages_are_the_loaded_ones_with_folder_paths()
    {
        using var tmp = new TempDir();
        WriteValid(tmp.Path, "b_comp");
        WriteValid(tmp.Path, "a_comp");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.True(result.Registry.TryGet("a_comp", out var package));
        Assert.Equal("a_comp", package!.FolderName);
        Assert.Equal(Path.Combine(tmp.Path, "a_comp"), package.FolderPath);
        Assert.Equal(new[] { "a_comp", "b_comp" }, result.Registry.All.Select(p => p.Id));
    }

    [Fact]
    public void Broken_folder_does_not_skip_a_companion_it_references()
    {
        using var tmp = new TempDir();
        Write(tmp.Path, "broken", null, null);
        Write(tmp.Path, "lone", CompanionJson("lone"), DialogsJson("lone"));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "lone" }, result.Registry.Ids);
        Assert.Equal(new[] { IssueCodes.MissingCompanionFile, IssueCodes.MissingDialogsFile },
            result.Issues.Select(i => i.Code));
        Assert.All(result.Issues, i => Assert.Equal("broken", i.Folder));
    }
}
