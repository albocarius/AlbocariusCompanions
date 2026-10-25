using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 75 (M7, SPEC_BEHAVIOR_FOUNDATION I5): a non-"once" conversation that can reward the player every time.
// Fixture: old_wound: p_wound_ask → wound_story → p_wound_sorry (relation +2, setFlags told_wound) → wound_thanks → end;
// wound_story → p_wound_skip → hub.
public class RepeatableRewardTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(bool once, Dictionary<string, string>? nodes = null, string? conversationWhen = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        conversation["once"] = once;
        if (conversationWhen != null)
            conversation["when"] = JObject.Parse(conversationWhen);
        foreach (var (node, json) in nodes ?? new Dictionary<string, string>())
        {
            var target = Fixtures.Node(conversation, node);
            foreach (var pair in JObject.Parse(json))
                target[pair.Key] = pair.Value;
        }
        Fixtures.AddConversation(d, langs, conversation);
        d["variables"]!["count"] = new JObject { ["type"] = "int" };
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static IEnumerable<ValidationIssue> Warnings(IReadOnlyList<ValidationIssue> issues)
    {
        Assert.Empty(issues.Errors());
        return issues.Where(i => i.Code == IssueCodes.RepeatableReward);
    }

    [Fact]
    public void Once_conversation_never_warns() => Assert.Empty(Warnings(Validate(once: true)));

    [Fact]
    public void Unguarded_relation_reward_warns_naming_the_node()
    {
        var warning = Assert.Single(Warnings(Validate(once: false)));
        Assert.Equal(Id, warning.Folder);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        Assert.Contains("'old_wound'", warning.Message);
        Assert.Contains("'p_wound_sorry'", warning.Message);
        Assert.Contains("relation +2", warning.Message);
    }

    [Theory]
    // The reward node checks the flag it sets.
    [InlineData("p_wound_sorry", """{ "when": { "notFlags": ["told_wound"] } }""", null)]
    // The conversation checks it.
    [InlineData(null, null, """{ "notFlags": ["told_wound"] }""")]
    // A node earlier on the path checks it; nested "all" counts; the own-id form is the same variable.
    [InlineData("wound_story", """{ "when": { "all": [ { "notFlags": ["test_companion.told_wound"] } ] } }""", null)]
    [InlineData("p_wound_ask", """{ "when": { "var": "told_wound", "eq": false } }""", null)]
    public void Guard_that_the_path_makes_false_stops_the_warning(string? node, string? json, string? conversationWhen) =>
        Assert.Empty(Warnings(Validate(false, node == null ? null : new() { [node] = json! }, conversationWhen)));

    [Theory]
    // A guard under "any" or "not" guarantees nothing.
    [InlineData("""{ "when": { "any": [ { "notFlags": ["told_wound"] }, { "hired": true } ] } }""")]
    [InlineData("""{ "when": { "not": { "flags": ["told_wound"] } } }""")]
    // A guard on another variable than the one written.
    [InlineData("""{ "when": { "notFlags": ["other"] } }""")]
    public void Guard_that_does_not_cover_the_write_still_warns(string sorryNode) =>
        Assert.Single(Warnings(ValidateWithOther(new() { ["p_wound_sorry"] = sorryNode })));

    static IReadOnlyList<ValidationIssue> ValidateWithOther(Dictionary<string, string> nodes)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        conversation["once"] = false;
        foreach (var (node, json) in nodes)
            foreach (var pair in JObject.Parse(json))
                Fixtures.Node(conversation, node)[pair.Key] = pair.Value;
        Fixtures.AddConversation(d, langs, conversation);
        d["variables"]!["other"] = new JObject { ["type"] = "bool" };
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    [Fact]
    public void Write_after_the_reward_does_not_count()
    {
        var issues = Validate(false, new()
        {
            ["p_wound_sorry"] = """{ "when": { "notFlags": ["told_wound"] }, "effects": { "relation": 2 } }""",
            ["wound_thanks"] = """{ "effects": { "setFlags": ["told_wound"] } }""",
        });
        Assert.Single(Warnings(issues));
    }

    [Theory]
    [InlineData("""{ "when": { "var": "count", "max": 2 }, "effects": { "relation": 1, "add": { "count": 1 } } }""", false)]
    [InlineData("""{ "when": { "var": "count", "min": 0 }, "effects": { "relation": 1, "add": { "count": 1 } } }""", true)]
    [InlineData("""{ "when": { "var": "count", "eq": 0 }, "effects": { "relation": 1, "set": { "count": 1 } } }""", false)]
    [InlineData("""{ "when": { "flags": ["told_wound"] }, "effects": { "relation": 1, "clearFlags": ["told_wound"] } }""", false)]
    [InlineData("""{ "effects": { "relation": -2 } }""", false)]
    [InlineData("""{ "effects": { "transfer": { "gold": 50, "from": "world", "to": "player" } } }""", true)]
    [InlineData("""{ "effects": { "transfer": { "gold": 50, "from": "self", "to": "player" } } }""", true)]
    [InlineData("""{ "effects": { "transfer": { "item": "grain", "from": "world", "to": "player" } } }""", true)]
    [InlineData("""{ "effects": { "transfer": { "gold": 50, "from": "player", "to": "self" } } }""", false)]
    public void Reward_kinds_and_int_guards(string sorryNode, bool warns)
    {
        var count = Warnings(Validate(false, new() { ["p_wound_sorry"] = sorryNode })).Count();
        Assert.Equal(warns ? 1 : 0, count);
    }

    [Fact]
    public void Loops_terminate_and_still_find_the_reward()
    {
        var issues = Validate(false, new() { ["wound_thanks"] = """{ "next": ["p_wound_sorry", "p_wound_skip"] }""" });
        Assert.Single(Warnings(issues));
    }
}
