using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 73 (M7, SPEC_BEHAVIOR_FOUNDATION I5): a node from which no path leads to "hub" or "end" is an error.
public class NoExitTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate, Dictionary<string, string>? extraTexts = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        mutate(conversation);
        Fixtures.AddConversation(d, langs, conversation);
        foreach (var (id, text) in extraTexts ?? new Dictionary<string, string>())
            Fixtures.LangLines(langs["en"])[id] = text;
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static void Next(JObject c, string node, params string[] next) => Fixtures.Node(c, node)["next"] = new JArray(next);

    static void AddNode(JObject c, string id, string speaker, params string[] next) =>
        Fixtures.Nodes(c).Add(new JObject { ["id"] = id, ["speaker"] = speaker, ["next"] = new JArray(next) });

    [Fact]
    public void Fixture_conversation_has_exits() =>
        Assert.DoesNotContain(Validate(_ => { }), i => i.Code == IssueCodes.ConversationNoExit);

    [Fact]
    public void Loop_with_a_way_out_is_fine() =>
        Assert.Empty(Validate(c => Next(c, "wound_thanks", "p_wound_sorry", "p_wound_skip")));

    [Fact]
    public void Loop_without_a_way_out_is_one_error_naming_its_nodes()
    {
        var issues = Validate(c =>
        {
            Next(c, "p_wound_skip", "wound_thanks");
            Next(c, "wound_thanks", "p_wound_sorry", "p_wound_skip");
        });

        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.ConversationNoExit, error.Code);
        Assert.Equal(Id, error.Folder);
        Assert.Contains("old_wound", error.Message);
        foreach (var node in new[] { "p_wound_ask", "wound_story", "p_wound_sorry", "p_wound_skip", "wound_thanks" })
            Assert.Contains($"'{node}'", error.Message);
    }

    [Fact]
    public void Only_the_trapped_nodes_are_named()
    {
        // wound_story can still leave through p_wound_skip → hub; the companion's self-loop "trap" cannot.
        var issues = Validate(c =>
        {
            Next(c, "p_wound_sorry", "trap");
            AddNode(c, "trap", "npc", "trap");
        }, new Dictionary<string, string> { ["trap"] = "Again and again." });

        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.ConversationNoExit, error.Code);
        Assert.Contains("'trap'", error.Message);
        Assert.Contains("'p_wound_sorry'", error.Message);
        Assert.DoesNotContain("'wound_story'", error.Message);
        Assert.DoesNotContain("'p_wound_skip'", error.Message);
    }

    [Fact]
    public void Unreachable_loop_without_exit_is_also_an_error()
    {
        var issues = Validate(c =>
        {
            AddNode(c, "orphan_a", "npc", "orphan_b");
            AddNode(c, "orphan_b", "npc", "orphan_a");
        }, new Dictionary<string, string> { ["orphan_a"] = "A.", ["orphan_b"] = "B." });

        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.ConversationNoExit, error.Code);
        Assert.Contains("'orphan_a'", error.Message);
        Assert.Contains("'orphan_b'", error.Message);
        Assert.Equal(2, issues.Count(i => i.Code == IssueCodes.UnreachableNode));
    }

    [Fact]
    public void Unknown_next_target_is_reported_once_not_as_a_missing_exit()
    {
        var issues = Validate(c => Next(c, "wound_thanks", "ghost"));

        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.UnknownNode, error.Code);
    }
}
