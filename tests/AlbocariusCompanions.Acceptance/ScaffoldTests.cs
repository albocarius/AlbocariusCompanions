using System.Diagnostics;
using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Registry;

namespace AlbocariusCompanions.Acceptance;

// Task 52 (SPEC_CORE_GAPS T3.3): tools/new-companion.sh creates a companion that passes validation as it is.
// new-companion.bat mirrors the script for Windows and is not run here.
public class ScaffoldTests
{
    static readonly string Script = Path.Combine(RepoPaths.Root, "tools", "new-companion.sh");

    static (int Exit, string Output) Run(params string[] args)
    {
        var info = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(Script);
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    [Fact]
    public void Scaffolded_companion_passes_validation()
    {
        using var tmp = new TempDir();
        Fixtures.WriteShared(tmp.Path);

        var (exit, output) = Run("new_one", tmp.Path);

        Assert.True(exit == 0, output);
        var result = CompanionCatalog.Load(tmp.Path);
        Assert.Empty(result.Issues.Errors());
        Assert.Equal(new[] { "new_one" }, result.Registry.Ids);
    }

    [Theory]
    [InlineData("_shared")]
    [InlineData("New_One")]
    [InlineData("new-one")]
    [InlineData("")]
    public void Invalid_or_reserved_ids_are_refused(string id)
    {
        using var tmp = new TempDir();
        var (exit, _) = Run(id, tmp.Path);

        Assert.NotEqual(0, exit);
        Assert.Empty(Directory.GetDirectories(tmp.Path));
    }

    [Fact]
    public void Existing_folder_is_never_overwritten()
    {
        using var tmp = new TempDir();
        var existing = Directory.CreateDirectory(Path.Combine(tmp.Path, "taken")).FullName;
        File.WriteAllText(Path.Combine(existing, "keep.txt"), "mine");

        var (exit, output) = Run("taken", tmp.Path);

        Assert.NotEqual(0, exit);
        Assert.Contains("already exists", output);
        Assert.Equal(new[] { "keep.txt" }, Directory.GetFiles(existing).Select(Path.GetFileName));
    }

    [Fact]
    public void Template_contains_every_vanilla_dialog_point_with_its_vanilla_english_text()
    {
        // SPEC_CORE_GAPS T4.0: the template is a working starting point and a reference of every dialog point.
        var template = Path.Combine(RepoPaths.Root, "tools", "companion-template");
        var dialogs = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(template, "dialogs.json")));
        var en = (Newtonsoft.Json.Linq.JObject)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(template, "lang", "en.json")))["lines"]!;
        var slots = dialogs["lines"]!.Select(l => ((string)l["slot"]!, (string)l["id"]!)).ToList();

        Assert.All(AlbocariusCompanions.Core.Rules.DialogSlots.Required, s => Assert.Contains(slots, x => x.Item1 == s));
        foreach (var point in AlbocariusCompanions.Core.Rules.VanillaDialogPoints.All)
        {
            var (_, id) = Assert.Single(slots, x => x.Item1 == point.Slot);
            Assert.Equal(point.EnglishText, (string?)en[id]);
        }
    }

    [Fact]
    public void Batch_script_exists_next_to_the_shell_script() =>
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "tools", "new-companion.bat")));
}
