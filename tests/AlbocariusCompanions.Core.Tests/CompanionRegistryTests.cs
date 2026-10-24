using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;
using Newtonsoft.Json;

namespace AlbocariusCompanions.Core.Tests;

public class CompanionRegistryTests
{
    private static CompanionPackage Pkg(string id) =>
        new(id, "/tmp/" + id,
            JsonConvert.DeserializeObject<CompanionDefinition>($$"""{ "id": "{{id}}", "name": "N" }""")!,
            JsonConvert.DeserializeObject<DialogFile>("""{ "lines": [] }""")!);

    [Fact]
    public void Duplicate_message_names_the_offending_id()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CompanionRegistry(new[] { Pkg("a"), Pkg("other").WithId("a") }));

        Assert.Contains("a", ex.Message);
    }

    [Fact]
    public void All_is_ordered_ordinal_not_culture_or_cased()
    {
        var reg = new CompanionRegistry(new[] { Pkg("beta"), Pkg("Alpha"), Pkg("alpha") });

        Assert.Equal(new[] { "Alpha", "alpha", "beta" }, reg.All.Select(p => p.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_and_empty_id_are_not_found(string? id)
    {
        var reg = new CompanionRegistry(new[] { Pkg("a") });

        Assert.False(reg.Contains(id!));
        Assert.False(reg.TryGet(id!, out var pkg));
        Assert.Null(pkg);
    }

    [Fact]
    public void TryGet_returns_the_same_package_instance_that_was_given_in()
    {
        var pkg = Pkg("a");
        var reg = new CompanionRegistry(new[] { pkg });

        Assert.True(reg.TryGet("a", out var found));
        Assert.Same(pkg, found);
    }

    [Fact]
    public void Ids_are_unique_and_match_All()
    {
        var reg = new CompanionRegistry(new[] { Pkg("zulu"), Pkg("alpha") });

        Assert.Equal(new[] { "alpha", "zulu" }, reg.Ids);
        Assert.Equal(reg.All.Select(p => p.Id), reg.Ids);
    }
}

file static class PackageTestExtensions
{
    /// <summary>Same folder as the original but with a different companion id (to exercise registry keying by id).</summary>
    public static CompanionPackage WithId(this CompanionPackage package, string id) =>
        new(package.FolderName, package.FolderPath,
            new CompanionDefinition { Id = id, Name = package.Companion.Name }, package.Dialogs);
}
