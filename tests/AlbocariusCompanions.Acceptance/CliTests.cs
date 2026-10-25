using System.Xml.Linq;
using AlbocariusCompanions.Cli;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Registry;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance;

// Task 78 (M7, SPEC_BEHAVIOR_FOUNDATION I6): the authoring CLI.
public class CliTests
{
    static readonly string RepoRoot = FindRoot();

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    static (int Code, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        var code = CliProgram.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    static void WriteBroken(string root, string id)
    {
        var c = Core.Fixtures.Companion(id);
        c["age"] = 5;
        Core.Fixtures.Write(root, id, c, Core.Fixtures.Dialogs(id));
    }

    static void WriteWithWarning(string root, string id)
    {
        // A complete en file plus a tr file missing lines → missing-translation warnings only.
        var langs = Core.Fixtures.Langs();
        langs["tr"] = Core.Fixtures.Tr();
        Core.Fixtures.Write(root, id, Core.Fixtures.Companion(id), Core.Fixtures.Dialogs(id), langs);
    }

    [Fact]
    public void Project_targets_net8_and_references_core_only()
    {
        var csproj = XDocument.Load(Path.Combine(RepoRoot, "tools", "AlbocariusCompanions.Cli", "AlbocariusCompanions.Cli.csproj"));
        var props = csproj.Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup").ToDictionary(e => e.Name.LocalName, e => e.Value);
        Assert.Equal("net8.0", props["TargetFramework"]);
        Assert.Equal("Exe", props["OutputType"]);
        Assert.Equal("Major", props["RollForward"]);
        var refs = csproj.Descendants().Where(e => e.Name.LocalName is "ProjectReference" or "Reference")
            .Select(e => e.Attribute("Include")!.Value).ToList();
        Assert.Equal(new[] { @"..\..\src\AlbocariusCompanions.Core\AlbocariusCompanions.Core.csproj" }, refs.Select(r => r.Replace('/', '\\')));
        var sln = File.ReadAllText(Path.Combine(RepoRoot, "AlbocariusCompanions.sln"));
        Assert.Contains("AlbocariusCompanions.Cli.csproj", sln);
    }

    [Fact]
    public void Validate_clean_directory_exits_zero()
    {
        using var tmp = new Core.TempDir();
        Core.Fixtures.WriteValid(tmp.Path, "alpha");
        Core.Fixtures.WriteValid(tmp.Path, "beta");

        var (code, output, _) = Run("validate", tmp.Path);

        Assert.Equal(0, code);
        Assert.EndsWith("0 error(s), 0 warning(s).\n", output);
    }

    [Fact]
    public void Validate_directory_with_a_broken_companion_prints_the_error_and_exits_one()
    {
        using var tmp = new Core.TempDir();
        Core.Fixtures.WriteValid(tmp.Path, "alpha");
        WriteBroken(tmp.Path, "broken");

        var (code, output, _) = Run("validate", tmp.Path);

        Assert.Equal(1, code);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.StartsWith("ERROR ") && l.Contains(" [broken] "));
        Assert.DoesNotContain(lines, l => l.Contains("[alpha]"));
        Assert.Matches(@"^[1-9]\d* error\(s\), 0 warning\(s\)\.$", lines[^1]);
    }

    [Fact]
    public void Validate_warnings_only_exits_zero()
    {
        using var tmp = new Core.TempDir();
        WriteWithWarning(tmp.Path, "alpha");

        var (code, output, _) = Run("validate", tmp.Path);

        Assert.Equal(0, code);
        Assert.Contains(output.Split('\n'), l => l.StartsWith("WARNING ") && l.Contains(" [alpha] "));
    }

    [Fact]
    public void Validate_one_companion_folder_reports_only_that_companion()
    {
        using var tmp = new Core.TempDir();
        Core.Fixtures.WriteValid(tmp.Path, "alpha");
        WriteBroken(tmp.Path, "broken");

        var good = Run("validate", Path.Combine(tmp.Path, "alpha"));
        Assert.Equal(0, good.Code);
        Assert.DoesNotContain("[broken]", good.Out);
        Assert.EndsWith("0 error(s), 0 warning(s).\n", good.Out);

        var bad = Run("validate", Path.Combine(tmp.Path, "broken"));
        Assert.Equal(1, bad.Code);
        Assert.Contains("[broken]", bad.Out);
    }

    [Theory]
    [InlineData()]
    [InlineData("frobnicate")]
    [InlineData("validate")]
    [InlineData("mermaid")]
    public void Bad_usage_exits_two_with_usage_on_stderr(params string[] args)
    {
        var (code, output, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains("usage", error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", output);
    }

    [Fact]
    public void Missing_path_exits_two()
    {
        var (code, _, error) = Run("validate", Path.Combine(Path.GetTempPath(), "albocarius-does-not-exist-" + Guid.NewGuid()));
        Assert.Equal(2, code);
        Assert.NotEqual("", error);
    }

    static string WriteConversationCompanion(string root)
    {
        var d = Core.Fixtures.Dialogs("alpha");
        var langs = Core.Fixtures.Langs();
        Core.Fixtures.AddConversation(d, langs);
        Core.Fixtures.Write(root, "alpha", Core.Fixtures.Companion("alpha"), d, langs);
        return Path.Combine(root, "alpha");
    }

    [Fact]
    public void Mermaid_prints_the_chart_of_a_companion()
    {
        using var tmp = new Core.TempDir();
        var folder = WriteConversationCompanion(tmp.Path);
        CompanionCatalog.Load(tmp.Path).Registry.TryGet("alpha", out var package);

        var all = Run("mermaid", folder);
        Assert.Equal(0, all.Code);
        Assert.Equal(MermaidExport.Render(package!), all.Out);

        var one = Run("mermaid", folder, "old_wound");
        Assert.Equal(0, one.Code);
        Assert.Equal(MermaidExport.Render(package!, "old_wound"), one.Out);
    }

    [Fact]
    public void Mermaid_unknown_conversation_exits_two()
    {
        using var tmp = new Core.TempDir();
        var (code, output, error) = Run("mermaid", WriteConversationCompanion(tmp.Path), "nope");
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.Contains("nope", error);
    }

    [Fact]
    public void Mermaid_of_an_invalid_companion_prints_its_errors_and_exits_one()
    {
        using var tmp = new Core.TempDir();
        WriteBroken(tmp.Path, "broken");
        var (code, output, error) = Run("mermaid", Path.Combine(tmp.Path, "broken"));
        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Contains("[broken]", error);
    }

    // Task 80 (M7 I8): the shipped sample content validates with zero errors and zero warnings.
    [Fact]
    public void Repository_companions_validate_without_errors_or_warnings()
    {
        var (code, output, _) = Run("validate", Path.Combine(RepoRoot, "module", "ModuleData", "Companions"));
        Assert.Equal(0, code);
        Assert.Equal("0 error(s), 0 warning(s).\n", output);
    }
}
