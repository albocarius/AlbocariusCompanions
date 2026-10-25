using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 77 (M7, SPEC_BEHAVIOR_FOUNDATION I6): conversations as Mermaid flowcharts.
public class MermaidExportTests
{
    const string Id = "test_companion";

    static CompanionPackage Package(Action<JObject>? mutate = null, JObject? second = null, Dictionary<string, string>? secondTexts = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        mutate?.Invoke(conversation);
        Fixtures.AddConversation(d, langs, conversation);
        if (second != null)
            Fixtures.AddConversation(d, langs, second, secondTexts);
        return Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs);
    }

    const string FixtureChart = """
        flowchart TD
          old_wound__entry(["old_wound (hub, once)<br/>when: hired"])
          old_wound__p_wound_ask[/"p_wound_ask: Where did that wound come from?"/]
          old_wound__wound_story["wound_story: A border skirmish, Captain."]
          old_wound__p_wound_sorry[/"p_wound_sorry: I'm sorry.<br/>effects: relation +2, told_wound = true"/]
          old_wound__p_wound_skip[/"p_wound_skip: Let's move on."/]
          old_wound__wound_thanks["wound_thanks: Thank you, Captain."]
          old_wound__entry --> old_wound__p_wound_ask
          old_wound__p_wound_ask --> old_wound__wound_story
          old_wound__wound_story --> old_wound__p_wound_sorry
          old_wound__wound_story --> old_wound__p_wound_skip
          old_wound__p_wound_sorry --> old_wound__wound_thanks
          old_wound__p_wound_skip --> old_wound__hub
          old_wound__wound_thanks --> old_wound__end
          old_wound__hub(("hub"))
          old_wound__end(("end"))
          class old_wound__p_wound_ask,old_wound__p_wound_sorry,old_wound__p_wound_skip player
          class old_wound__wound_story,old_wound__wound_thanks npc
          classDef player fill:#e8f0fe,stroke:#4a6fa5
          classDef npc fill:#fdf2e3,stroke:#b07a2a

        """;

    [Fact]
    public void Fixture_conversation_renders_exactly() =>
        Assert.Equal(FixtureChart.Replace("\r\n", "\n"), MermaidExport.Render(Package()));

    [Fact]
    public void Selecting_the_conversation_by_id_gives_the_same_chart() =>
        Assert.Equal(MermaidExport.Render(Package()), MermaidExport.Render(Package(), "old_wound"));

    [Fact]
    public void Unknown_conversation_id_throws() =>
        Assert.Throws<ArgumentException>(() => MermaidExport.Render(Package(), "nope"));

    [Fact]
    public void Node_conditions_label_the_edges_into_them()
    {
        var chart = MermaidExport.Render(Package(c =>
        {
            Fixtures.Node(c, "p_wound_skip")["when"] = new JObject { ["minRelation"] = 5 };
            Fixtures.Node(c, "p_wound_ask")["when"] = new JObject { ["flags"] = new JArray("told_wound") };
        }));

        Assert.Contains("  old_wound__wound_story -->|\"relation >= 5\"| old_wound__p_wound_skip\n", chart);
        Assert.Contains("  old_wound__entry -->|\"told_wound\"| old_wound__p_wound_ask\n", chart);
    }

    static readonly JObject Greeting = JObject.Parse("""
        {
          "id": "hello", "entry": "greeting",
          "start": ["hello_npc"],
          "nodes": [ { "id": "hello_npc", "speaker": "npc", "next": ["end"] } ]
        }
        """);

    [Fact]
    public void Several_conversations_follow_file_order_and_share_one_class_definition()
    {
        var chart = MermaidExport.Render(Package(second: Greeting, secondTexts: new Dictionary<string, string> { ["hello_npc"] = "Say \"hi\"." }));

        Assert.StartsWith("flowchart TD\n", chart);
        Assert.True(chart.IndexOf("old_wound__entry", StringComparison.Ordinal) < chart.IndexOf("hello__entry", StringComparison.Ordinal));
        Assert.Contains("  hello__entry([\"hello (greeting)\"])\n", chart);
        Assert.Contains("  hello__hello_npc[\"hello_npc: Say #quot;hi#quot;.\"]\n", chart);
        Assert.Contains("  class hello__hello_npc npc\n", chart);
        Assert.DoesNotContain("hello__hub", chart);
        Assert.DoesNotContain("class  player", chart);
        Assert.Equal(1, CountOf(chart, "classDef player"));
        Assert.Equal(1, CountOf(chart, "classDef npc"));
        Assert.EndsWith("  classDef npc fill:#fdf2e3,stroke:#b07a2a\n", chart);

        var only = MermaidExport.Render(Package(second: Greeting, secondTexts: new Dictionary<string, string> { ["hello_npc"] = "Hi." }), "hello");
        Assert.DoesNotContain("old_wound", only);
    }

    [Fact]
    public void Missing_text_is_shown_as_a_placeholder()
    {
        var package = Package(second: Greeting, secondTexts: new Dictionary<string, string>());
        Assert.Contains("  hello__hello_npc[\"hello_npc: (no text)\"]\n", MermaidExport.Render(package, "hello"));
    }

    static int CountOf(string text, string part) => (text.Length - text.Replace(part, "").Length) / part.Length;

    // ---- condition and effect descriptions ----

    [Theory]
    [InlineData("""{ "met": true, "hired": false, "inParty": true }""", "met and !hired and inParty")]
    [InlineData("""{ "minRelation": -5, "maxRelation": 20, "minGold": 100 }""", "relation >= -5 and relation <= 20 and gold >= 100")]
    [InlineData("""{ "flags": ["a", "jeremus.b"], "notFlags": ["c"] }""", "a and jeremus.b and !c")]
    [InlineData("""{ "var": "seen", "min": 1, "max": 3 }""", "seen >= 1 and seen <= 3")]
    [InlineData("""{ "var": "told", "eq": false }""", "told = false")]
    [InlineData("""{ "var": "seen", "eq": 4 }""", "seen = 4")]
    [InlineData("""{ "all": [ { "flags": ["a"] }, { "flags": ["b"] } ] }""", "(a and b)")]
    [InlineData("""{ "any": [ { "flags": ["a"] }, { "hired": true, "flags": ["b"] } ] }""", "(a or hired and b)")]
    [InlineData("""{ "hired": true, "not": { "flags": ["a"] } }""", "hired and not (a)")]
    [InlineData("""{ "hasGold": { "who": "self", "min": 100 }, "hasItem": { "who": "player", "id": "grain", "min": 2 } }""",
        "self gold >= 100 and player has 2 grain")]
    [InlineData("""{ "hasItem": { "who": "player", "id": "grain" } }""", "player has 1 grain")]
    [InlineData("""{ }""", "")]
    public void Conditions_are_described_compactly(string when, string expected) =>
        Assert.Equal(expected, MermaidExport.Describe(JObject.Parse(when).ToObject<ConditionSet>()!));

    [Theory]
    [InlineData("""{ "relation": -3, "gold": 50 }""", "relation -3, gold +50")]
    [InlineData("""{ "gold": -100, "join": true }""", "gold -100, join")]
    [InlineData("""{ "setFlags": ["a"], "clearFlags": ["b"], "set": { "seen": 2, "told": true }, "add": { "mood": -1 } }""",
        "a = true, b = false, seen = 2, told = true, mood += -1")]
    [InlineData("""{ "relation": 1, "transfer": { "gold": 100, "from": "player", "to": "self" }, "join": true }""",
        "relation +1, 100 gold player -> self, join")]
    [InlineData("""{ "transfer": { "item": "grain", "from": "world", "to": "player" } }""", "1 grain world -> player")]
    [InlineData("""{ "transfer": { "item": "grain", "count": 3, "from": "player", "to": "world" } }""", "3 grain player -> world")]
    [InlineData("""{ }""", "")]
    public void Effects_are_described_compactly(string effects, string expected) =>
        Assert.Equal(expected, MermaidExport.Describe(JObject.Parse(effects).ToObject<EffectSet>()!));
}
