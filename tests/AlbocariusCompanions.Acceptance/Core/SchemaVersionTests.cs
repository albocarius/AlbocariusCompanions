using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 32-schema-version-ogg. schemaVersion is checked by the loader, in one place; a bad version skips the folder.
public class SchemaVersionTests
{
    [Fact]
    public void Current_version_is_1() => Assert.Equal(1, SchemaVersions.Current);

    [Fact]
    public void Valid_files_with_version_1_load_clean()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        var pkg = Assert.Single(result.Packages);
        Assert.Equal(1, pkg.Companion.SchemaVersion);
        Assert.Equal(1, pkg.Dialogs.SchemaVersion);
    }

    public static TheoryData<string, string> Files => new() { { "companion.json", "companion" }, { "dialogs.json", "dialogs" } };

    static (LoadResult Result, string File) LoadWith(string which, Action<JObject> mutate)
    {
        var tmp = new TempDir();
        var companion = Fixtures.Companion("broken");
        var dialogs = Fixtures.Dialogs("broken");
        mutate(which == "companion" ? companion : dialogs);
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.Write(tmp.Path, "broken", companion, dialogs);
        var result = CompanionLoader.LoadAll(tmp.Path);
        tmp.Dispose();
        return (result, which + ".json");
    }

    static void AssertSingleError(LoadResult result, string code, string file, params string[] messageParts)
    {
        Assert.Equal("good", Assert.Single(result.Packages).FolderName);
        var issue = Assert.Single(result.Issues);
        Assert.True(issue.IsError);
        Assert.Equal(code, issue.Code);
        Assert.Equal("broken", issue.Folder);
        Assert.Contains(file, issue.Message);
        Assert.Contains("schemaVersion", issue.Message);
        foreach (var part in messageParts)
            Assert.Contains(part, issue.Message);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Missing_version_is_an_error(string file, string which)
    {
        var (result, _) = LoadWith(which, o => o.Remove("schemaVersion"));
        AssertSingleError(result, IssueCodes.MissingSchemaVersion, file);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Null_version_is_missing(string file, string which)
    {
        var (result, _) = LoadWith(which, o => o["schemaVersion"] = JValue.CreateNull());
        AssertSingleError(result, IssueCodes.MissingSchemaVersion, file);
    }

    [Theory]
    [InlineData("companion.json", "companion", "\"1\"")]
    [InlineData("dialogs.json", "dialogs", "1.5")]
    [InlineData("companion.json", "companion", "true")]
    [InlineData("dialogs.json", "dialogs", "[1]")]
    public void Non_integer_version_is_an_error(string file, string which, string json)
    {
        var (result, _) = LoadWith(which, o => o["schemaVersion"] = JToken.Parse(json));
        AssertSingleError(result, IssueCodes.InvalidSchemaVersion, file);
    }

    [Theory]
    [InlineData("companion.json", "companion", 2)]
    [InlineData("dialogs.json", "dialogs", 99)]
    [InlineData("companion.json", "companion", 0)]
    [InlineData("dialogs.json", "dialogs", -1)]
    public void Unsupported_version_is_an_error_naming_found_and_supported(string file, string which, int version)
    {
        var (result, _) = LoadWith(which, o => o["schemaVersion"] = version);
        AssertSingleError(result, IssueCodes.UnsupportedSchemaVersion, file, version.ToString(), "1");
    }

    [Fact]
    public void Version_check_does_not_hide_invalid_json()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(Path.Combine(tmp.Path, "broken"));
        File.WriteAllText(Path.Combine(tmp.Path, "broken", "companion.json"), "{ not json");
        File.WriteAllText(Path.Combine(tmp.Path, "broken", "dialogs.json"), Fixtures.Dialogs("broken").ToString());

        var issue = Assert.Single(CompanionLoader.LoadAll(tmp.Path).Issues);
        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
    }

    [Fact]
    public void Placeholder_companion_in_the_repo_declares_version_1()
    {
        var folder = Path.Combine(RepoRoot(), "module", "ModuleData", "Companions", "test_companion");
        foreach (var file in new[] { "companion.json", "dialogs.json" })
            Assert.Equal(1, (int)JObject.Parse(File.ReadAllText(Path.Combine(folder, file)))["schemaVersion"]!);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
