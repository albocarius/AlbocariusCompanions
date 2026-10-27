using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Tasks 122a–h (M11): every trigger of a GAME_MAP batch has an adapter. Staged: moved into the acceptance project when
// the first adapter batch is assigned; each batch adds its Q.
public class TriggerAdapterBatchTests
{
    // One line per finished adapter batch (task 122x adds its Q when it is assigned).
    [Theory]
    [InlineData("Q35")]
    [InlineData("Q36")]
    [InlineData("Q37")]
    [InlineData("Q38")]
    [InlineData("Q39")]
    [InlineData("Q40")]
    [InlineData("Q41")]
    [InlineData("Q42")]
    public void Every_trigger_of_the_batch_has_an_adapter(string q)
    {
        var names = TriggerGameWiringTests.Entries().Select(e => e.Name).ToHashSet();
        var expected = File.ReadAllLines(Path.Combine(RepoPaths.Root, "orchestra", "triggers.tsv")).Select(l => l.Split('\t'))
            .Where(c => c.Length > 4 && c[1] == q && c[4] != "-").Select(c => c[4]).ToList();
        Assert.NotEmpty(expected);
        var missing = expected.Where(n => !names.Contains(n)).ToList();
        Assert.True(missing.Count == 0, $"{q}: no adapter for {string.Join(", ", missing)}");
    }

}
