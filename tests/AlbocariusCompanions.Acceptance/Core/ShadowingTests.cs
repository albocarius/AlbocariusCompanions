using AlbocariusCompanions.Cli;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 92 (M9 I2): in one selection set, a candidate without conditions and with a strictly higher priority than
// another hides that one → warning naming both. Equal priority → nothing. Hub conversations are not a selection set.
public class ShadowingTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        mutate(d);
        foreach (var line in Fixtures.Lines(d).Cast<JObject>())
            Fixtures.LangLines(langs["en"])[(string)line["id"]!] ??= "Text.";
        foreach (var c in (d["conversations"] as JArray ?? new JArray()).Cast<JObject>())
            foreach (var n in Fixtures.Nodes(c).Cast<JObject>())
                Fixtures.LangLines(langs["en"])[(string)n["id"]!] = "Text.";
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static List<ValidationIssue> Shadowed(IReadOnlyList<ValidationIssue> issues)
    {
        Assert.Empty(issues.Errors());
        return issues.Where(i => i.Code == IssueCodes.ShadowedCandidate).ToList();
    }

    static JObject Backstory(string id, int? priority, JObject? when = null)
    {
        var line = new JObject { ["id"] = id, ["slot"] = "backstory" };
        if (priority != null) line["priority"] = priority;
        if (when != null) line["when"] = when;
        return line;
    }

    [Fact]
    public void Unconditioned_higher_priority_slot_line_hides_the_others()
    {
        var warning = Assert.Single(Shadowed(Validate(d => Fixtures.Lines(d).Add(Backstory("backstory_02", 1)))));
        Assert.Contains("backstory_02", warning.Message);
        Assert.Contains("backstory_01", warning.Message);
        Assert.Contains("backstory", warning.Message);
    }

    [Fact]
    public void Equal_priority_is_fine()
    {
        Assert.Empty(Shadowed(Validate(d =>
        {
            ((JObject)Fixtures.Lines(d).First(l => (string?)l["id"] == "backstory_01"))["priority"] = 3;
            Fixtures.Lines(d).Add(Backstory("backstory_02", 3));
        })));
    }

    [Fact]
    public void A_higher_priority_line_with_a_condition_is_fine()
    {
        Assert.Empty(Shadowed(Validate(d => Fixtures.Lines(d).Add(Backstory("backstory_02", 5, new JObject { ["hired"] = true })))));
    }

    static JObject Conversation(string json) => JObject.Parse(json);

    [Fact]
    public void Unconditioned_higher_priority_npc_reply_hides_its_siblings()
    {
        var issues = Validate(d => d["conversations"] = new JArray(Conversation("""
        { "id": "c", "entry": "hub", "start": ["p1"],
          "nodes": [ { "id": "p1", "speaker": "player", "next": ["n1", "n2", "n3"] },
                     { "id": "n1", "speaker": "npc", "next": ["hub"], "priority": 2 },
                     { "id": "n2", "speaker": "npc", "next": ["hub"], "when": { "minRelation": 10 }, "priority": 1 },
                     { "id": "n3", "speaker": "npc", "next": ["hub"], "priority": 2 } ] }
        """)));
        var warnings = Shadowed(issues);
        // n1 and n3 (no conditions, priority 2) each hide n2; n1/n3 between themselves are equal.
        Assert.Equal(2, warnings.Count);
        Assert.All(warnings, w => Assert.Contains("'n2'", w.Message));
        Assert.Contains(warnings, w => w.Message.Contains("'n1'"));
        Assert.Contains(warnings, w => w.Message.Contains("'n3'"));
    }

    [Fact]
    public void A_reply_with_an_implied_condition_does_not_shadow()
    {
        Assert.Empty(Shadowed(Validate(d => d["conversations"] = new JArray(Conversation("""
        { "id": "c", "entry": "hub", "start": ["p1"],
          "nodes": [ { "id": "p1", "speaker": "player", "next": ["n1", "n2"] },
                     { "id": "n1", "speaker": "npc", "next": ["hub"], "priority": 2,
                       "effects": { "transfer": { "gold": 10, "from": "self", "to": "player" } } },
                     { "id": "n2", "speaker": "npc", "next": ["hub"] } ] }
        """)))).Where(w => w.Message.Contains("'n1'")));
    }

    [Fact]
    public void Greeting_start_nodes_are_a_set()
    {
        var warning = Assert.Single(Shadowed(Validate(d => d["conversations"] = new JArray(Conversation("""
        { "id": "g", "entry": "greeting", "once": true, "start": ["s1", "s2"],
          "nodes": [ { "id": "s1", "speaker": "npc", "next": ["hub"], "priority": 1 },
                     { "id": "s2", "speaker": "npc", "next": ["hub"] } ] }
        """)))));
        Assert.Contains("'s1'", warning.Message);
        Assert.Contains("'s2'", warning.Message);
    }

    [Fact]
    public void An_always_offered_greeting_with_higher_priority_hides_other_greetings()
    {
        var issues = Validate(d => d["conversations"] = new JArray(
            Conversation("""{ "id": "big", "entry": "greeting", "priority": 1, "start": ["b1"], "nodes": [ { "id": "b1", "speaker": "npc", "next": ["hub"] } ] }"""),
            Conversation("""{ "id": "small", "entry": "greeting", "start": ["s1"], "nodes": [ { "id": "s1", "speaker": "npc", "next": ["hub"] } ] }"""),
            Conversation("""{ "id": "once", "entry": "greeting", "once": true, "priority": 5, "start": ["o1"], "nodes": [ { "id": "o1", "speaker": "npc", "next": ["hub"] } ] }""")));
        // "once" can stop holding, so it hides nothing; "big" always holds and hides "small".
        var warning = Assert.Single(Shadowed(issues));
        Assert.Contains("greeting 'big'", warning.Message);
        Assert.Contains("greeting 'small'", warning.Message);
    }

    [Fact]
    public void Hub_conversations_are_never_shadowed()
    {
        Assert.Empty(Shadowed(Validate(d => d["conversations"] = new JArray(
            Conversation("""{ "id": "a", "entry": "hub", "priority": 9, "start": ["pa"], "nodes": [ { "id": "pa", "speaker": "player", "next": ["hub"] } ] }"""),
            Conversation("""{ "id": "b", "entry": "hub", "start": ["pb"], "nodes": [ { "id": "pb", "speaker": "player", "next": ["hub"] } ] }""")))));
    }

    [Fact]
    public void Cli_reports_the_shadowing_fixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "AlbocariusCompanions.sln"))) root = root.Parent;
        var fixture = Path.Combine(root!.FullName, "tests", "fixtures", "shadowing", "shadow_companion");
        var stdout = new StringWriter();
        var code = CliProgram.Run(new[] { "validate", fixture }, stdout, new StringWriter());
        Assert.Equal(0, code);
        Assert.Contains("WARNING shadowed_candidate [shadow_companion]", stdout.ToString());
        Assert.Contains("0 error(s), 1 warning(s).", stdout.ToString());
    }
}
