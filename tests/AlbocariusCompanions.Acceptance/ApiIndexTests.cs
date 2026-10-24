using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Acceptance;

// SPEC rule 1: every game API used in the Game layer is listed in docs/GAME_MAP.md "API index" with its decomp source.
public class ApiIndexTests
{
    static readonly Regex Row = new(@"^\| `(?<symbol>[^`]+)` \| `(?<path>decomp/[^`]+)` \| (?<q>Q\d+) \|", RegexOptions.Compiled);

    static List<Match> Rows()
    {
        var text = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "GAME_MAP.md")).Replace("\r\n", "\n");
        var start = text.IndexOf("\n## API index", StringComparison.Ordinal);
        Assert.True(start >= 0, "GAME_MAP.md has no '## API index' section");
        return text[start..].Split('\n').Select(l => Row.Match(l)).Where(m => m.Success).ToList();
    }

    [Fact]
    public void Index_has_rows() => Assert.NotEmpty(Rows());

    [Fact]
    public void Every_row_points_at_an_existing_file_containing_the_symbol()
    {
        foreach (var row in Rows())
        {
            var file = Path.Combine(RepoPaths.Root, row.Groups["path"].Value);
            Assert.True(File.Exists(file), $"missing source file: {row.Groups["path"].Value}");
            var member = row.Groups["symbol"].Value.Split('.').Last();
            Assert.True(File.ReadAllText(file).Contains(member), $"'{member}' not found in {row.Groups["path"].Value}");
        }
    }

    [Fact]
    public void Every_row_references_a_verified_question()
    {
        var text = File.ReadAllText(Path.Combine(RepoPaths.Root, "docs", "GAME_MAP.md"));
        foreach (var q in Rows().Select(r => r.Groups["q"].Value).Distinct())
            Assert.Matches(new Regex($@"## {q} —[^\n]*\n\nStatus: VERIFIED"), text.Replace("\r\n", "\n"));
    }
}
