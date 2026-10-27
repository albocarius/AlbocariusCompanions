using AlbocariusCompanions.Cli;
using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 115 (M11 T2): docs/TRIGGERS.md is generated from the registry.
public class TriggerDocsTests
{
    static readonly string DocsFile = Path.Combine(RepoPaths.Root, "docs", "TRIGGERS.md");

    [Fact]
    public void Committed_file_equals_the_generated_output()
    {
        var committed = File.Exists(DocsFile) ? File.ReadAllText(DocsFile).Replace("\r\n", "\n") : "(missing)";
        Assert.True(committed == TriggerDocs.Render(),
            "docs/TRIGGERS.md is out of date: run the CLI 'docs triggers docs/TRIGGERS.md'.");
    }

    [Fact]
    public void Every_trigger_has_a_section_and_common_ones_are_listed_first()
    {
        var text = TriggerDocs.Render();
        Assert.All(TriggerRegistry.All, t => Assert.Contains($"\n### {t.Name}\n", text));
        var common = text.IndexOf("## Common triggers", StringComparison.Ordinal);
        var firstCategory = text.IndexOf("## Category:", StringComparison.Ordinal);
        Assert.True(common > 0 && common < firstCategory);
        Assert.All(TriggerRegistry.All.Where(t => t.Common), t => Assert.Contains($"[`{t.Name}`]", text[common..firstCategory]));
    }

    [Fact]
    public void Constants_are_documented()
    {
        var text = TriggerDocs.Render();
        Assert.Contains("once every 3 in-game days", text);
        Assert.Contains("At most 1 interruption(s) per in-game day", text);
        Assert.Contains("At most 3 notifications per in-game day", text);
    }

    [Fact]
    public void Cli_writes_the_file_and_prints_to_stdout()
    {
        using var tmp = new AlbocariusCompanions.Acceptance.Core.TempDir();
        var file = Path.Combine(tmp.Path, "T.md");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Assert.Equal(0, CliProgram.Run(new[] { "docs", "triggers", file }, stdout, stderr));
        Assert.Equal(TriggerDocs.Render(), File.ReadAllText(file));

        stdout = new StringWriter();
        Assert.Equal(0, CliProgram.Run(new[] { "docs", "triggers" }, stdout, stderr));
        Assert.Equal(TriggerDocs.Render(), stdout.ToString());
        Assert.Equal(2, CliProgram.Run(new[] { "docs", "nothing" }, new StringWriter(), new StringWriter()));
    }
}
