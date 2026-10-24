using System.Text;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Core.Tests;

public class CompanionLoaderTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("albocarius-unit-").FullName;
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    private static string WriteFolder(string root, string name, string? companionJson, string? dialogsJson)
    {
        var dir = Directory.CreateDirectory(Path.Combine(root, name)).FullName;
        if (companionJson != null) File.WriteAllText(Path.Combine(dir, "companion.json"), companionJson);
        if (dialogsJson != null) File.WriteAllText(Path.Combine(dir, "dialogs.json"), dialogsJson);
        return dir;
    }

    private const string MiniCompanion = """{ "id": "a_companion" }""";
    private const string MiniDialogs = """{ "lines": [] }""";

    [Fact]
    public void Missing_root_issue_has_empty_folder_and_mentions_the_root()
    {
        var missing = Path.Combine(Path.GetTempPath(), "albocarius-no-" + Guid.NewGuid());
        var issue = Assert.Single(CompanionLoader.LoadAll(missing).Issues);

        Assert.Equal(IssueCodes.RootNotFound, issue.Code);
        Assert.Equal("", issue.Folder);
        Assert.Contains(missing, issue.Message);
    }

    [Fact]
    public void Null_root_is_reported_not_thrown() =>
        Assert.Equal(IssueCodes.RootNotFound, Assert.Single(CompanionLoader.LoadAll(null!).Issues).Code);

    [Fact]
    public void Empty_folder_reports_both_missing_files_and_skips()
    {
        using var tmp = new TempDir();
        WriteFolder(tmp.Path, "empty", null, null);

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Packages);
        Assert.Equal(new[] { IssueCodes.MissingCompanionFile, IssueCodes.MissingDialogsFile }, result.Issues.Select(i => i.Code));
        Assert.All(result.Issues, i => Assert.Equal("empty", i.Folder));
    }

    [Fact]
    public void Unknown_property_in_nested_object_is_an_invalid_json_error()
    {
        using var tmp = new TempDir();
        WriteFolder(tmp.Path, "c",
            """{ "id": "c", "equipment": { "templteId": "x" } }""", MiniDialogs);

        var issue = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Issues);

        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        Assert.Contains("companion.json", issue.Message);
        Assert.Contains("templteId", issue.Message);
    }

    [Fact]
    public void Empty_file_is_invalid_json_not_a_null_document_pass()
    {
        using var tmp = new TempDir();
        WriteFolder(tmp.Path, "c", "", MiniDialogs);

        var issue = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Issues);

        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        Assert.Contains("companion.json", issue.Message);
    }

    [Fact]
    public void Utf8_bom_and_non_ascii_text_are_read_correctly()
    {
        using var tmp = new TempDir();
        var dir = WriteFolder(tmp.Path, "c", MiniCompanion, null);
        File.WriteAllText(Path.Combine(dir, "dialogs.json"),
            """{ "lines": [ { "id": "c_intro_01", "slot": "intro", "text": { "tr": "Selam, Kaptan." }, "voiced": false } ] }""",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal("Selam, Kaptan.", Assert.Single(Assert.Single(result.Packages).Dialogs.Lines!).Text!["tr"]);
    }

    [Fact]
    public void Ordering_is_ordinal_and_only_direct_subdirectories_are_packages()
    {
        using var tmp = new TempDir();
        WriteFolder(tmp.Path, "alpha", MiniCompanion, MiniDialogs);
        WriteFolder(tmp.Path, "Beta", MiniCompanion, MiniDialogs);
        Directory.CreateDirectory(Path.Combine(tmp.Path, "alpha", "subfolder")); // nested: not a companion

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal(new[] { "Beta", "alpha" }, result.Packages.Select(p => p.FolderName));
    }

    [Fact]
    public void Id_missing_still_loads_and_package_id_falls_back_to_folder_name()
    {
        using var tmp = new TempDir();
        WriteFolder(tmp.Path, "folder_name", """{ "name": "No Id" }""", MiniDialogs);

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal("folder_name", Assert.Single(result.Packages).Id);
    }
}
