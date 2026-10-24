using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 20b-core-catalog: loader + validation + registry together.
public class CatalogTests
{
    [Fact]
    public void Valid_companions_end_up_in_the_registry()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "a_comp");
        Fixtures.WriteValid(tmp.Path, "b_comp");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Empty(result.Issues);
        Assert.Equal(new[] { "a_comp", "b_comp" }, result.Registry.All.Select(p => p.Id));
    }

    [Fact]
    public void Broken_folder_does_not_break_the_valid_one()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        Fixtures.WriteValid(tmp.Path, "broken");
        File.WriteAllText(Path.Combine(tmp.Path, "broken", "companion.json"), "{ not json");

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "good" }, result.Registry.Ids);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidJson && i.Folder == "broken");
    }

    [Fact]
    public void Validation_error_skips_only_that_companion()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "good");
        var bad = Fixtures.Companion("bad");
        bad["voice"] = "angry";
        Fixtures.Write(tmp.Path, "bad", bad, Fixtures.Dialogs("bad"));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "good" }, result.Registry.Ids);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.InvalidVoice && i.Folder == "bad");
    }

    [Fact]
    public void Folder_with_mismatched_id_does_not_take_down_the_real_owner()
    {
        using var tmp = new TempDir();
        Fixtures.WriteValid(tmp.Path, "real");
        Fixtures.Write(tmp.Path, "impostor", Fixtures.Companion("real"), Fixtures.Dialogs("real"));

        var result = CompanionCatalog.Load(tmp.Path);

        // Both folders claim id "real": duplicate ids skip every folder involved, the mismatch is also reported.
        Assert.Empty(result.Registry.Ids);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.IdFolderMismatch && i.Folder == "impostor");
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.DuplicateId && i.Folder == "real");
    }

    [Fact]
    public void Warnings_do_not_skip_a_companion()
    {
        using var tmp = new TempDir();
        var c = Fixtures.Companion("a");
        c["relations"] = new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject { ["companion"] = "future_one", ["attitude"] = "friend" });
        Fixtures.Write(tmp.Path, "a", c, Fixtures.Dialogs("a"));

        var result = CompanionCatalog.Load(tmp.Path);

        Assert.Equal(new[] { "a" }, result.Registry.Ids);
        Assert.Contains(result.Issues, i => i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void Missing_root_gives_empty_registry_and_an_error()
    {
        var result = CompanionCatalog.Load(Path.Combine(Path.GetTempPath(), "albocarius-missing-" + Guid.NewGuid()));

        Assert.Equal(0, result.Registry.Count);
        Assert.Contains(result.Issues, i => i.Code == IssueCodes.RootNotFound);
    }
}
