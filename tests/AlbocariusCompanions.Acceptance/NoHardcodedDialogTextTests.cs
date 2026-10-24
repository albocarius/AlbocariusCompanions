using System.Text.RegularExpressions;

namespace AlbocariusCompanions.Acceptance;

// Task 50 (SPEC_CORE_GAPS T6.1): no player-facing dialog text in C#. Dialog lines are registered as "{=!}{VAR}" and get
// their text from JSON at runtime; a localized literal "{=id}Text" in code would be text that never reaches the lang files.
public class NoHardcodedDialogTextTests
{
    static readonly string GameSources = Path.Combine(RepoPaths.Root, "src", "AlbocariusCompanions.Game");

    // "{=" followed by anything but "!}" or the closing quote of a concatenation ("{=" + id + "}").
    static readonly Regex LocalizedLiteral = new(@"""\{=(?!!\}|"")", RegexOptions.Compiled);

    [Fact]
    public void Game_code_has_no_localized_text_literals()
    {
        var hits = Directory.GetFiles(GameSources, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(l => LocalizedLiteral.IsMatch(l.Text))
            .Select(l => $"{l.File}:{l.Line}: {l.Text.Trim()}")
            .ToList();

        Assert.True(hits.Count == 0, "Dialog text belongs in JSON lang files:\n" + string.Join("\n", hits));
    }

    [Fact]
    public void Dialog_registration_uses_only_variable_lines()
    {
        var source = File.ReadAllText(Path.Combine(GameSources, "CompanionDialogs.cs"));
        // Every AddDialogLine/AddPlayerLine text argument goes through Text(Variable(...)).
        var calls = Regex.Matches(source, @"\.Add(Dialog|Player)Line\((?<args>[^;]*?)\n\s*(Condition|null)", RegexOptions.Singleline);
        Assert.NotEmpty(calls);
        Assert.All(calls, c => Assert.Contains("Text(Variable(", c.Groups["args"].Value));
    }
}
