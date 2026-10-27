using System.Text.RegularExpressions;
using AlbocariusCompanions.Core.Triggers;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Tasks 113/114 (M11): the registry matches orchestra/triggers.tsv and every entry is well formed.
public class TriggerRegistryTests
{
    record TsvRow(string Event, string Q, string Name, string Category, bool Common);

    static readonly List<TsvRow> Tsv = File.ReadAllLines(Path.Combine(RepoPaths.Root, "orchestra", "triggers.tsv"))
        .Where(l => !l.StartsWith('#') && !l.StartsWith("event\t"))
        .Select(l => l.Split('\t'))
        .Where(c => c[4] != "-")
        .Select(c => new TsvRow(c[0], c[1], c[4], c[5], c[6] == "1"))
        .ToList();

    static readonly string CampaignEvents = File.ReadAllText(Path.Combine(RepoPaths.Root,
        "decomp", "TaleWorlds.CampaignSystem", "TaleWorlds.CampaignSystem", "CampaignEvents.cs"));

    // One line per finished batch (task 114x adds its Q here when it is assigned).
    [Theory]
    [InlineData("polling")]
    [InlineData("Q35")]
    [InlineData("Q36")]
    [InlineData("Q37")]
    [InlineData("Q38")]
    [InlineData("Q39")]
    [InlineData("Q40")]
    public void Registry_has_exactly_the_tsv_triggers_of_the_batch(string q)
    {
        var expected = Tsv.Where(r => q == "polling" ? r.Event.StartsWith("poll:") : r.Q == q).ToList();
        Assert.NotEmpty(expected);
        foreach (var row in expected)
        {
            Assert.True(TriggerRegistry.TryGet(row.Name, out var t), $"missing trigger '{row.Name}' ({row.Event})");
            Assert.Equal(row.Event, t.Source);
            Assert.Equal(row.Category, t.Category);
            Assert.True(row.Common == t.Common, $"{row.Name}: common should be {row.Common}");
        }
    }

    [Fact]
    public void Registry_has_no_trigger_outside_the_tsv()
    {
        var names = Tsv.Select(r => r.Name).ToHashSet();
        Assert.All(TriggerRegistry.All, t => Assert.Contains(t.Name, names));
    }

    [Fact]
    public void Names_are_unique_camel_case()
    {
        Assert.Equal(TriggerRegistry.All.Count, TriggerRegistry.All.Select(t => t.Name).Distinct().Count());
        Assert.All(TriggerRegistry.All, t => Assert.Matches("^[a-z][a-zA-Z]+$", t.Name));
    }

    [Fact]
    public void Every_entry_is_well_formed()
    {
        foreach (var t in TriggerRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Description), t.Name);
            Assert.False(string.IsNullOrWhiteSpace(t.Category), t.Name);
            if (!t.IsPolling)
                Assert.True(Regex.IsMatch(CampaignEvents, $@"public static IMbEvent(<[^\n]*>)? {Regex.Escape(t.Source)}\b"),
                    $"{t.Name}: '{t.Source}' is not a CampaignEvents member");
            Assert.Equal(t.Fields.Count, t.Fields.Select(f => f.Name).Distinct().Count());
            Assert.All(t.Fields, f =>
            {
                Assert.Matches("^[a-z][a-zA-Z]*$", f.Name);
                Assert.False(string.IsNullOrWhiteSpace(f.Description), $"{t.Name}.{f.Name}");
                Assert.False(string.IsNullOrWhiteSpace(f.From), $"{t.Name}.{f.Name}");
            });
            Assert.All(t.SubjectFields, s =>
            {
                var f = t.Field(s);
                Assert.True(f != null, $"{t.Name}: subject field '{s}' is not a field");
                Assert.True(EntityProperties.IsEntity(f!.Kind) && Subjects.KindsFor(f.Kind).Count > 0, $"{t.Name}: subject field '{s}' cannot be a subject");
            });
            Assert.True(Subjects.IsValidFor(t, t.DefaultSubject), $"{t.Name}: default subject '{t.DefaultSubject}' is not valid");
            // World-wide events (they have subject fields) never default to "any" (spec T3).
            if (t.SubjectFields.Count > 0)
                Assert.True(t.DefaultSubject != Subjects.Any, $"{t.Name}: world-wide trigger defaults to any");
            if (!t.IsPolling)
                Assert.Empty(t.Parameters);
        }
    }

    [Fact]
    public void Excluded_events_are_not_triggers()
    {
        var excluded = File.ReadAllLines(Path.Combine(RepoPaths.Root, "orchestra", "triggers.tsv"))
            .Select(l => l.Split('\t')).Where(c => c.Length > 4 && c[4] == "-").Select(c => c[0]).ToList();
        Assert.Contains("OnRansomOfferedToPlayerEvent", excluded);
        Assert.All(excluded, e => Assert.DoesNotContain(TriggerRegistry.All, t => t.Source == e));
    }

    [Fact]
    public void Roles_are_the_spec_roles() =>
        Assert.Equal(new[] { "governor", "partyLeader", "caravanLeader", "alleyLeader", "questAlternative" }, TriggerRegistry.Roles);
}
