using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Registry;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 17-core-registry
public class RegistryTests
{
    static CompanionRegistry Build(params string[] ids) => new(ids.Select(Fixtures.Package));

    [Fact]
    public void Lookup_by_id()
    {
        var reg = Build("b_one", "a_two");

        Assert.Equal(2, reg.Count);
        Assert.True(reg.Contains("a_two"));
        Assert.True(reg.TryGet("b_one", out var pkg));
        Assert.Equal("b_one", pkg!.Id);
    }

    [Fact]
    public void Unknown_id_is_not_found()
    {
        var reg = Build("a");

        Assert.False(reg.Contains("missing"));
        Assert.False(reg.TryGet("missing", out var pkg));
        Assert.Null(pkg);
    }

    [Fact]
    public void Ids_are_case_sensitive()
    {
        var reg = Build("test_companion");

        Assert.False(reg.Contains("Test_Companion"));
    }

    [Fact]
    public void Exposes_id_set_and_ordered_list()
    {
        var reg = Build("zeta", "alpha", "mid");

        Assert.Equal(new[] { "alpha", "mid", "zeta" }, reg.Ids.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(new[] { "alpha", "mid", "zeta" }, reg.All.Select(p => p.Id));
    }

    [Fact]
    public void Empty_registry_is_valid()
    {
        var reg = new CompanionRegistry(Array.Empty<CompanionPackage>());

        Assert.Equal(0, reg.Count);
        Assert.Empty(reg.Ids);
        Assert.Empty(reg.All);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        var a1 = Fixtures.Package("a");
        var a2 = Fixtures.Package("a_copy", Fixtures.Companion("a"), Fixtures.Dialogs("a"));

        Assert.Throws<ArgumentException>(() => new CompanionRegistry(new[] { a1, a2 }));
    }

    [Fact]
    public void Registry_is_a_snapshot_of_its_input()
    {
        var list = new List<CompanionPackage> { Fixtures.Package("a") };
        var reg = new CompanionRegistry(list);
        list.Add(Fixtures.Package("b"));

        Assert.Equal(1, reg.Count);
        Assert.False(reg.Contains("b"));
    }
}
