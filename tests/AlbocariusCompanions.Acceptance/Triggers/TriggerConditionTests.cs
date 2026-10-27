using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Triggers;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 117 (M11 G4, T4): event.<path> conditions and the role condition.
public class TriggerConditionTests
{
    sealed class Context : IConversationContext, ITriggerContext
    {
        public TriggerPayload? Event { get; init; }
        public string? Role { get; init; }
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => true;
        public int Relation => 0;
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => reference == "mood" ? 0 : null;
        public int? GoldOf(string actor) => 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    sealed class PlainContext : IConversationContext
    {
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => true;
        public int Relation => 0;
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => 0;
        public int? GoldOf(string actor) => 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    static readonly TriggerPayload Payload = new(new Dictionary<string, PayloadValue>
    {
        ["victim"] = PayloadValue.Entity(FieldKind.Hero, new Dictionary<string, object> { ["id"] = "lord_1", ["culture"] = "vlandia", ["isPlayer"] = false }),
        ["detail"] = PayloadValue.Text("DiedInBattle"),
        ["days"] = PayloadValue.Int(5),
    });

    static ConditionSet When(string json) => JObject.Parse(json).ToObject<ConditionSet>()!;

    [Theory]
    [InlineData("""{ "var": "event.victim.culture", "eq": "vlandia" }""", true)]
    [InlineData("""{ "var": "event.victim.culture", "eq": "empire" }""", false)]
    [InlineData("""{ "var": "event.victim", "eq": "lord_1" }""", true)]
    [InlineData("""{ "var": "event.victim.isPlayer", "eq": false }""", true)]
    [InlineData("""{ "var": "event.detail", "eq": "DiedInBattle" }""", true)]
    [InlineData("""{ "var": "event.days", "min": 3, "max": 5 }""", true)]
    [InlineData("""{ "var": "event.days", "min": 6 }""", false)]
    [InlineData("""{ "var": "event.missing", "eq": "x" }""", false)]
    [InlineData("""{ "not": { "var": "event.victim.culture", "eq": "empire" } }""", true)]
    [InlineData("""{ "any": [ { "var": "event.days", "eq": 1 }, { "var": "event.days", "eq": 5 } ] }""", true)]
    public void Event_conditions_read_the_payload(string json, bool holds) =>
        Assert.Equal(holds, ConversationConditions.Holds(When(json), new Context { Event = Payload }));

    [Fact]
    public void Event_condition_without_a_payload_fails()
    {
        Assert.False(ConversationConditions.Holds(When("""{ "var": "event.days", "min": 0 }"""), new Context()));
        Assert.False(ConversationConditions.Holds(When("""{ "var": "event.days", "min": 0 }"""), new PlainContext()));
    }

    [Fact]
    public void Event_references_are_not_variables() =>
        Assert.Empty(VariableUses.InCondition(When("""{ "var": "event.days", "min": 0 }""")));

    [Theory]
    [InlineData("governor", "governor", true)]
    [InlineData("governor", "partyLeader", false)]
    [InlineData("governor", null, false)]
    public void Role_condition_compares_the_current_role(string wanted, string? current, bool holds) =>
        Assert.Equal(holds, ConversationConditions.Holds(new ConditionSet { Role = wanted }, new Context { Role = current }));

    [Fact]
    public void Role_condition_on_a_context_without_roles_fails() =>
        Assert.False(ConversationConditions.Holds(new ConditionSet { Role = "governor" }, new PlainContext()));

    // Validation

    const string Id = "cond_one";

    static IReadOnlyList<ValidationIssue> ValidateLine(string lineJson)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var line = JObject.Parse(lineJson);
        Fixtures.Lines(d).Add(line);
        Fixtures.LangLines(langs["en"])[(string)line["id"]!] = "Text.";
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static void AssertConditionError(IReadOnlyList<ValidationIssue> issues, string contains) =>
        Assert.True(issues.Errors().Any(i => i.Code == IssueCodes.InvalidCondition && i.Message.Contains(contains)),
            string.Join("\n", issues.Select(i => $"{i.Code}: {i.Message}")));

    [Fact]
    public void Valid_event_conditions_pass()
    {
        var issues = ValidateLine("""
            { "id": "rip_01", "trigger": { "on": "heroKilled" },
              "when": { "all": [ { "var": "event.victim.culture", "eq": "vlandia" }, { "var": "event.detail", "eq": "Executed" } ] } }
            """);
        Assert.Empty(issues.Errors());
    }

    [Fact]
    public void Event_condition_outside_triggered_content_is_an_error() =>
        AssertConditionError(ValidateLine("""{ "id": "x_01", "slot": "idle", "when": { "var": "event.victim", "eq": "a" } }"""), "has no 'trigger'");

    [Fact]
    public void Event_condition_on_an_unknown_field_is_an_error() =>
        AssertConditionError(ValidateLine("""{ "id": "x_01", "trigger": { "on": "heroKilled" }, "when": { "var": "event.village", "eq": "a" } }"""), "not a field");

    [Fact]
    public void Event_condition_with_the_wrong_eq_type_is_an_error() =>
        AssertConditionError(ValidateLine("""{ "id": "x_01", "trigger": { "on": "heroKilled" }, "when": { "var": "event.victim.isPlayer", "eq": "no" } }"""), "true/false");

    [Fact]
    public void Event_condition_min_on_text_is_an_error() =>
        AssertConditionError(ValidateLine("""{ "id": "x_01", "trigger": { "on": "heroKilled" }, "when": { "var": "event.detail", "min": 1 } }"""), "not a number");

    [Fact]
    public void Event_condition_in_a_conversation_node_is_checked()
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        Fixtures.AddConversation(d, langs, JObject.Parse("""
            { "id": "rip", "trigger": { "on": "heroKilled" }, "start": ["rip_a"],
              "nodes": [ { "id": "rip_a", "speaker": "npc", "next": ["end"], "when": { "var": "event.nope", "eq": 1 } } ] }
            """), new() { ["rip_a"] = "Gone." });
        AssertConditionError(DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) }), "not a field");
    }

    [Fact]
    public void Unknown_role_is_an_error() =>
        AssertConditionError(ValidateLine("""{ "id": "x_01", "slot": "idle", "when": { "role": "jester" } }"""), "role 'jester'");

    [Fact]
    public void Known_role_is_valid() =>
        Assert.Empty(ValidateLine("""{ "id": "x_01", "slot": "idle", "when": { "role": "governor" } }""").Errors());
}

// Task 120 (M11 T6): partyMorale effect.
public class PartyMoraleTests
{
    const string Id = "morale_one";

    static IReadOnlyList<ValidationIssue> Validate(int morale)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var c = Fixtures.Conversation();
        Fixtures.Node(c, "p_wound_sorry")["effects"] = JObject.Parse($$"""{ "partyMorale": {{morale}} }""");
        Fixtures.AddConversation(d, langs, c);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-20)]
    [InlineData(20)]
    public void In_range_is_valid(int morale) =>
        Assert.DoesNotContain(Validate(morale).Errors(), i => i.Code == IssueCodes.InvalidEffect);

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-25)]
    public void Zero_or_out_of_range_is_an_error(int morale) =>
        Assert.Contains(Validate(morale).Errors(), i => i.Code == IssueCodes.InvalidEffect && i.Message.Contains("partyMorale"));

    [Fact]
    public void Game_applies_it_to_recent_events_morale()
    {
        var source = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "AlbocariusCompanions.Game", "CompanionDialogs.cs"));
        Assert.Contains("MobileParty.MainParty.RecentEventsMorale += morale", source);
    }
}
