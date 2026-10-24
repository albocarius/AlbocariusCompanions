using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 16-core-loader
public class LoaderTests
{
    [Fact]
    public void Loads_a_valid_folder()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "test_companion");

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        var pkg = Assert.Single(result.Packages);
        Assert.Equal("test_companion", pkg.FolderName);
        Assert.Equal(Path.Combine(tmp.Path, "test_companion"), pkg.FolderPath);
        Assert.Equal("test_companion", pkg.Companion.Id);
        Assert.Equal(5, pkg.Dialogs.Lines!.Count);
        Assert.Equal("Selam, Kaptan.", pkg.Dialogs.Lines[0].Text!["tr"]);
    }

    [Fact]
    public void Missing_root_is_reported_not_thrown()
    {
        var result = CompanionLoader.LoadAll(Path.Combine(Path.GetTempPath(), "albocarius-does-not-exist-" + Guid.NewGuid()));

        Assert.Empty(result.Packages);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.RootNotFound, issue.Code);
        Assert.True(issue.IsError);
    }

    [Fact]
    public void Missing_companion_json_skips_only_that_folder()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.Write(tmp.Path, "broken", null, Fixtures.Dialogs("broken"));

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Equal("good", Assert.Single(result.Packages).FolderName);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.MissingCompanionFile, issue.Code);
        Assert.Equal("broken", issue.Folder);
    }

    [Fact]
    public void Missing_dialogs_json_skips_only_that_folder()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.Write(tmp.Path, "broken", Fixtures.Companion("broken"), null);

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Equal("good", Assert.Single(result.Packages).FolderName);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.MissingDialogsFile, issue.Code);
        Assert.Equal("broken", issue.Folder);
    }

    [Theory]
    [InlineData("companion.json", "{ \"id\": \"broken\", ")]          // truncated
    [InlineData("companion.json", "null")]                             // literal null
    [InlineData("companion.json", "{ \"id\": \"broken\", \"age\": \"old\" }")] // wrong type
    [InlineData("companion.json", "{ \"id\": \"broken\", \"skils\": {} }")]    // unknown property (typo) is an error
    [InlineData("dialogs.json", "{ \"lines\": [ { \"id\": 1, ")]
    [InlineData("dialogs.json", "{ \"lines\": [ { \"id\": \"x\", \"txt\": {} } ] }")]
    public void Invalid_json_skips_only_that_folder(string file, string content)
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.WriteValid(tmp.Path, "broken");
        File.WriteAllText(Path.Combine(tmp.Path, "broken", file), content);

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Equal("good", Assert.Single(result.Packages).FolderName);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        Assert.Equal("broken", issue.Folder);
        Assert.True(issue.IsError);
        Assert.Contains(file, issue.Message);
    }

    [Fact]
    public void Dialogs_without_lines_load_as_empty_list()
    {
        using var tmp = new TempDir();
        Fixtures.Write(tmp.Path, "a", Fixtures.Companion("a"), new Newtonsoft.Json.Linq.JObject());

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.NotNull(Assert.Single(result.Packages).Dialogs.Lines);
        Assert.Empty(result.Packages[0].Dialogs.Lines!);
    }

    [Fact]
    public void Packages_are_ordered_by_folder_name_and_root_files_are_ignored()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "zeta");
        Fixtures.WriteValid(tmp.Path, "alpha");
        Fixtures.WriteValid(tmp.Path, "mid");
        File.WriteAllText(Path.Combine(tmp.Path, "README.txt"), "not a companion");

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal(new[] { "alpha", "mid", "zeta" }, result.Packages.Select(p => p.FolderName));
    }

    [Fact]
    public void Loader_does_not_validate()
    {
        using var tmp = new TempDir();
        var c = Fixtures.Companion("other_id");
        c["voice"] = "not_a_voice";
        Fixtures.Write(tmp.Path, "folder", c, Fixtures.Dialogs("other_id"));

        var result = CompanionLoader.LoadAll(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Single(result.Packages);
    }
}
