using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 93 (M9 I4): optional companion.json "noble" (bool, default false). Schema, model, validation only.
public class NobleTests
{
    static LoadResult Load(JToken? noble)
    {
        using var tmp = new TempDir();
        var companion = Fixtures.Companion("noble_one");
        if (noble != null)
            companion["noble"] = noble;
        Fixtures.Write(tmp.Path, "noble_one", companion, Fixtures.Dialogs("noble_one"));
        return CompanionLoader.LoadAll(tmp.Path);
    }

    [Fact]
    public void Absent_is_false()
    {
        var result = Load(null);
        Assert.Empty(result.Issues);
        Assert.False(Assert.Single(result.Packages).Companion.Noble);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_bool_is_read(bool value)
    {
        var result = Load(value);
        Assert.Empty(result.Issues);
        Assert.Equal(value, Assert.Single(result.Packages).Companion.Noble);
    }

    [Fact]
    public void A_valid_noble_companion_passes_validation()
    {
        using var tmp = new TempDir();
        var companion = Fixtures.Companion("noble_one");
        companion["noble"] = true;
        Fixtures.Write(tmp.Path, "noble_one", companion, Fixtures.Dialogs("noble_one"));
        Assert.Empty(CompanionCatalog.Load(tmp.Path).Issues);
    }

    [Fact]
    public void A_non_bool_is_an_error()
    {
        foreach (var bad in new JToken[] { "yes", new JObject(), new JArray(true) })
        {
            var result = Load(bad);
            Assert.Empty(result.Packages);
            var issue = Assert.Single(result.Issues);
            Assert.True(issue.IsError);
            Assert.Equal(IssueCodes.InvalidJson, issue.Code);
        }
    }

    [Fact]
    public void Template_declares_noble_false()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AlbocariusCompanions.sln"))) root = root.Parent;
        var template = JObject.Parse(File.ReadAllText(Path.Combine(root!.FullName, "tools", "companion-template", "companion.json")));
        Assert.Equal(JTokenType.Boolean, template["noble"]!.Type);
        Assert.False((bool)template["noble"]!);
    }
}
