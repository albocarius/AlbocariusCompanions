using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Acceptance;

// Mechanical checks for docs/GAME_MAP.md sections. They cannot prove an answer is right,
// but they reject unanswered sections, invented file paths and symbols that are not in the cited file.
public class GameMapTests
{
    // One entry per question handed out so far; extended as M1 tasks are assigned.
    public static TheoryData<int> Questions => new() { 1, 2, 3 };

    static readonly Regex EvidenceLine = new(@"^- `(?<path>(decomp|~/bannerlord)/[^`]+)` — `(?<symbol>[^`]+)` — \S", RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(Questions))]
    public void Section_is_answered_with_verifiable_evidence(int q)
    {
        var section = Section(q);
        var status = Regex.Match(section, @"^Status: (\w+)", RegexOptions.Multiline).Groups[1].Value;
        Assert.True(status is "DRAFT" or "VERIFIED", $"Q{q}: Status must be DRAFT or VERIFIED, was '{status}'");

        Assert.True(Body(section, "Answer").Trim().Length >= 80, $"Q{q}: ### Answer is missing or too short");
        Assert.False(string.IsNullOrWhiteSpace(Body(section, "Uncertain")), $"Q{q}: ### Uncertain must list items or say 'None.'");

        var evidence = Body(section, "Evidence").Split('\n').Where(l => l.StartsWith("- ")).ToList();
        Assert.NotEmpty(evidence);
        foreach (var line in evidence)
        {
            var m = EvidenceLine.Match(line);
            Assert.True(m.Success, $"Q{q}: evidence line does not match format: {line}");
            var file = Resolve(m.Groups["path"].Value);
            Assert.True(File.Exists(file) || Directory.Exists(file), $"Q{q}: cited path does not exist: {m.Groups["path"].Value}");
            if (File.Exists(file) && !file.EndsWith(".dll"))
            {
                var ident = LastIdentifier(m.Groups["symbol"].Value);
                Assert.True(File.ReadAllText(file).Contains(ident), $"Q{q}: '{ident}' not found in {m.Groups["path"].Value}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Questions))]
    public void Section_contains_no_copied_code_bodies(int q)
    {
        foreach (Match block in Regex.Matches(Section(q), "```.*?```", RegexOptions.Singleline))
            Assert.True(block.Value.Split('\n').Length <= 8, $"Q{q}: code block longer than 6 lines — signatures only, no method bodies");
    }

    static string LastIdentifier(string symbol)
    {
        var s = symbol.Split('(')[0].Split('<')[0].Trim();
        var parts = s.Split(new[] { '.', ':', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? symbol : parts[^1];
    }

    static string Resolve(string path) => path.StartsWith("~/")
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
        : Path.Combine(RepoRoot, path);

    static string Section(int q)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, "docs", "GAME_MAP.md")).Replace("\r\n", "\n");
        var m = Regex.Match(text, $@"^## Q{q} —.*?(?=^## Q\d+ —|\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(m.Success, $"Q{q}: section header '## Q{q} — ...' not found");
        return m.Value;
    }

    static string Body(string section, string heading)
    {
        var m = Regex.Match(section, $@"^### {heading}\s*\n(.*?)(?=^### |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : "";
    }

    static readonly string RepoRoot = FindRoot();

    static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
