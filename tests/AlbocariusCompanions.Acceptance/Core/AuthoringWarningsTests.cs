using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 74 (M7, SPEC_BEHAVIOR_FOUNDATION I5): authoring warnings over the loaded companions (CompanionCatalog).
public class AuthoringWarningsTests
{
    /// <summary>
    /// Writes a companion with the fixture conversation (old_wound: p_wound_ask → wound_story → p_wound_sorry | p_wound_skip;
    /// p_wound_sorry → wound_thanks → end; p_wound_skip → hub). <paramref name="nodes"/>: node id → JSON merged into it.
    /// </summary>
    static void Write(string root, string id, JObject? variables = null, Dictionary<string, string>? nodes = null, JObject? when = null)
    {
        var d = Fixtures.Dialogs(id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        if (when != null)
            conversation["when"] = when;
        foreach (var (node, json) in nodes ?? new Dictionary<string, string>())
            Fixtures.Node(conversation, node).Merge(JObject.Parse(json));
        Fixtures.AddConversation(d, langs, conversation);
        foreach (var pair in variables ?? new JObject())
            d["variables"]![pair.Key] = pair.Value;
        Fixtures.Write(root, id, Fixtures.Companion(id), d, langs);
    }

    static IReadOnlyList<ValidationIssue> Load(Action<string> write)
    {
        using var tmp = new TempDir();
        write(tmp.Path);
        var result = CompanionCatalog.Load(tmp.Path);
        Assert.Empty(result.Issues.Errors());
        return result.Issues;
    }

    static IEnumerable<ValidationIssue> Of(IReadOnlyList<ValidationIssue> issues, string code) => issues.Where(i => i.Code == code);

    // ---- variable never changes ----

    [Fact]
    public void Fixture_has_no_authoring_warnings() => Assert.Empty(Load(root => Write(root, "alpha")));

    [Fact]
    public void Variable_never_written_with_the_type_default_is_a_warning()
    {
        var issues = Load(root => Write(root, "alpha", JObject.Parse("""{ "asked": { "type": "bool" }, "count": { "type": "int", "default": 0 } }"""),
            new() { ["p_wound_skip"] = """{ "when": { "notFlags": ["asked"], "any": [ { "var": "count", "max": 3 } ] } }""" }));

        var warnings = Of(issues, IssueCodes.VariableNeverChanges).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Equal("alpha", w.Folder));
        Assert.Contains(warnings, w => w.Message.Contains("'asked'"));
        Assert.Contains(warnings, w => w.Message.Contains("'count'"));
        Assert.All(issues, i => Assert.False(i.IsError));
    }

    [Theory]
    [InlineData("""{ "angry": { "type": "bool", "default": true } }""", null)]
    [InlineData("""{ "count": { "type": "int", "default": 2 } }""", null)]
    [InlineData("""{ "angry": { "type": "bool" } }""", """{ "effects": { "setFlags": ["angry"] } }""")]
    [InlineData("""{ "angry": { "type": "bool" } }""", """{ "effects": { "clearFlags": ["angry"] } }""")]
    [InlineData("""{ "count": { "type": "int" } }""", """{ "effects": { "add": { "count": 1 } } }""")]
    [InlineData("""{ "count": { "type": "int" } }""", """{ "effects": { "set": { "count": 0 } } }""")]
    public void Written_or_non_default_variable_is_fine(string variables, string? skipNode) =>
        Assert.Empty(Of(Load(root => Write(root, "alpha", JObject.Parse(variables),
            skipNode == null ? null : new() { ["p_wound_skip"] = skipNode })), IssueCodes.VariableNeverChanges));

    [Fact]
    public void Shared_variable_written_by_another_companion_is_fine()
    {
        var issues = Load(root =>
        {
            Write(root, "jeremus", JObject.Parse("""{ "told_scar": { "type": "bool", "scope": "shared" } }"""));
            Write(root, "ahmet", nodes: new() { ["p_wound_skip"] = """{ "effects": { "setFlags": ["jeremus.told_scar"] } }""" });
        });
        Assert.Empty(Of(issues, IssueCodes.VariableNeverChanges));
    }

    [Fact]
    public void Shared_variable_only_read_by_another_companion_still_never_changes()
    {
        var issues = Load(root =>
        {
            Write(root, "jeremus", JObject.Parse("""{ "told_scar": { "type": "bool", "scope": "shared" } }"""));
            Write(root, "ahmet", when: JObject.Parse("""{ "flags": ["jeremus.told_scar"] }"""));
        });
        var warning = Assert.Single(Of(issues, IssueCodes.VariableNeverChanges));
        Assert.Equal("jeremus", warning.Folder);
    }

    [Fact]
    public void Built_in_mood_never_warns() => Assert.Empty(Of(Load(root => Write(root, "alpha",
        nodes: new() { ["p_wound_skip"] = """{ "when": { "var": "mood", "min": 0 } }""" })), IssueCodes.VariableNeverChanges));

    // ---- transfer without a matching check ----

    [Theory]
    [InlineData("""{ "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""")]
    [InlineData("""{ "effects": { "transfer": { "gold": 100, "from": "self", "to": "player" } } }""")]
    [InlineData("""{ "effects": { "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } } }""")]
    [InlineData("""{ "effects": { "gold": -100 } }""")]
    [InlineData("""{ "when": { "hasGold": { "who": "player", "min": 99 } }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""")]
    [InlineData("""{ "when": { "hasGold": { "who": "self", "min": 100 } }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""")]
    [InlineData("""{ "when": { "any": [ { "hasGold": { "who": "player", "min": 100 } } ] }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""")]
    [InlineData("""{ "when": { "not": { "hasGold": { "who": "player", "min": 100 } } }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""")]
    [InlineData("""{ "when": { "hasItem": { "who": "player", "id": "grain" } }, "effects": { "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } } }""")]
    [InlineData("""{ "when": { "hasItem": { "who": "player", "id": "wine", "min": 5 } }, "effects": { "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } } }""")]
    [InlineData("""{ "when": { "minGold": 50 }, "effects": { "gold": -100 } }""")]
    public void Transfer_from_an_actor_without_a_matching_check_is_a_warning(string skipNode)
    {
        var issues = Load(root => Write(root, "alpha", nodes: new() { ["p_wound_skip"] = skipNode }));
        var warning = Assert.Single(Of(issues, IssueCodes.TransferNotChecked));
        Assert.Equal("alpha", warning.Folder);
        Assert.Contains("p_wound_skip", warning.Message);
    }

    [Theory]
    [InlineData("""{ "effects": { "transfer": { "gold": 100, "from": "world", "to": "player" } } }""", null, null)]
    [InlineData("""{ "effects": { "gold": 100 } }""", null, null)]
    [InlineData("""{ "when": { "hasGold": { "who": "player", "min": 100 } }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""", null, null)]
    [InlineData("""{ "when": { "hasGold": { "who": "alpha", "min": 150 } }, "effects": { "transfer": { "gold": 100, "from": "self", "to": "player" } } }""", null, null)]
    [InlineData("""{ "when": { "all": [ { "hasGold": { "who": "player", "min": 100 } } ] }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""", null, null)]
    [InlineData("""{ "when": { "hasItem": { "who": "player", "id": "grain", "min": 2 } }, "effects": { "transfer": { "item": "grain", "count": 2, "from": "player", "to": "world" } } }""", null, null)]
    [InlineData("""{ "when": { "minGold": 100 }, "effects": { "gold": -100 } }""", null, null)]
    [InlineData("""{ "when": { "minGold": 100 }, "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""", null, null)]
    // The check sits on the conversation or on a node earlier on the path.
    [InlineData("""{ "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""", """{ "hasGold": { "who": "player", "min": 100 } }""", null)]
    [InlineData("""{ "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""", null, """{ "when": { "hasGold": { "who": "player", "min": 100 } } }""")]
    public void Transfer_with_a_matching_check_or_from_world_is_fine(string skipNode, string? conversationWhen, string? storyNode)
    {
        var nodes = new Dictionary<string, string> { ["p_wound_skip"] = skipNode };
        if (storyNode != null)
            nodes["wound_story"] = storyNode;
        var issues = Load(root => Write(root, "alpha", nodes: nodes, when: conversationWhen == null ? null : JObject.Parse(conversationWhen)));
        Assert.Empty(Of(issues, IssueCodes.TransferNotChecked));
    }

    [Fact]
    public void Check_on_a_sibling_branch_does_not_count()
    {
        // p_wound_sorry carries the check; p_wound_skip (its sibling, not on its path) pays.
        var issues = Load(root => Write(root, "alpha", nodes: new()
        {
            ["p_wound_sorry"] = """{ "when": { "hasGold": { "who": "player", "min": 100 } } }""",
            ["p_wound_skip"] = """{ "effects": { "transfer": { "gold": 100, "from": "player", "to": "self" } } }""",
        }));
        Assert.Single(Of(issues, IssueCodes.TransferNotChecked));
    }
}
