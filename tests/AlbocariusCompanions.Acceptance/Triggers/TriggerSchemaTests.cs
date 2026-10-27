using AlbocariusCompanions.Acceptance.Core;
using AlbocariusCompanions.Core.Loading;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Triggers;

// Task 116 (M11 T3, G5/G6): `trigger` on conversations and lines — model, loader, validation.
public class TriggerSchemaTests
{
    const string Id = "trig_one";

    static JObject TriggeredConversation(JObject trigger, string delivery = "") => JObject.Parse($$"""
    {
      "id": "after_battle",
      "trigger": {{trigger.ToString()}},
      "start": ["ab_hail"],
      "nodes": [
        { "id": "ab_hail", "speaker": "npc", "next": ["p_ab_yes", "p_ab_no"] },
        { "id": "p_ab_yes", "speaker": "player", "next": ["end"], "effects": { "relation": 1 } },
        { "id": "p_ab_no", "speaker": "player", "next": ["end"] }
      ]
    }
    """);

    static readonly Dictionary<string, string> Texts = new()
    {
        ["ab_hail"] = "A fine fight, Captain.", ["p_ab_yes"] = "It was.", ["p_ab_no"] = "Not now.",
    };

    static IReadOnlyList<ValidationIssue> Validate(JObject? conversation = null, JObject? line = null, Action<JObject>? dialogs = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        if (conversation != null)
            Fixtures.AddConversation(d, langs, conversation, Texts);
        if (line != null)
        {
            Fixtures.Lines(d).Add(line);
            Fixtures.LangLines(langs["en"])[(string)line["id"]!] = "Smoke on the horizon.";
        }
        dialogs?.Invoke(d);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static void AssertClean(IReadOnlyList<ValidationIssue> issues) =>
        Assert.True(issues.Errors().Count() == 0, string.Join("\n", issues.Errors().Select(i => i.Message)));

    static ValidationIssue AssertTriggerError(IReadOnlyList<ValidationIssue> issues, string contains, string code = IssueCodes.InvalidTrigger)
    {
        var hit = issues.Errors().FirstOrDefault(i => i.Code == code && i.Message.Contains(contains));
        Assert.True(hit != null, $"expected {code} containing '{contains}', got:\n" + string.Join("\n", issues.Select(i => $"{i.Code}: {i.Message}")));
        return hit!;
    }

    static JObject T(string json) => JObject.Parse(json);

    [Fact]
    public void Triggered_conversation_loads_from_disk_with_every_field()
    {
        using var tmp = new TempDir();
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        Fixtures.AddConversation(d, langs, TriggeredConversation(T("""
            { "on": "heroKilled", "subject": "playerClan", "where": { "victim.culture": "vlandia" }, "chance": 0.5,
              "delivery": "conversation", "expiresAfterHours": 12, "cooldownDays": 7, "allowAbsent": true }
            """)), Texts);
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d, langs);
        var result = CompanionLoader.LoadAll(tmp.Path);
        Assert.Empty(result.Issues);
        var spec = Assert.Single(result.Packages).Conversations.Single(c => c.Id == "after_battle").Trigger!;
        Assert.Equal("heroKilled", spec.On);
        Assert.Equal("playerClan", spec.Subject);
        Assert.Equal("vlandia", (string)spec.Where!["victim.culture"]!);
        Assert.Equal(0.5, spec.Chance);
        Assert.Equal(12, spec.ExpiresAfterHours);
        Assert.Equal(7, spec.CooldownDays);
        Assert.True(spec.AllowAbsent);
    }

    [Fact]
    public void Valid_triggered_conversation_has_no_errors() =>
        AssertClean(Validate(TriggeredConversation(T("""{ "on": "playerBattleEnded" }"""))));

    [Fact]
    public void Unknown_trigger_is_an_error() =>
        AssertTriggerError(Validate(TriggeredConversation(T("""{ "on": "dragonSighted" }"""))), "dragonSighted", IssueCodes.UnknownTrigger);

    [Fact]
    public void Missing_on_is_an_error() =>
        AssertTriggerError(Validate(TriggeredConversation(T("""{ "subject": "any" }"""))), "'on'");

    [Theory]
    [InlineData("settlement:town_V1")] // heroKilled has no settlement subject field
    [InlineData("nobody")]
    [InlineData("hero")]
    public void Invalid_subject_for_the_trigger_is_an_error(string subject) =>
        AssertTriggerError(Validate(TriggeredConversation(T($$"""{ "on": "heroKilled", "subject": "{{subject}}" }"""))), "subject");

    [Fact]
    public void Where_on_an_unknown_field_is_an_error() =>
        AssertTriggerError(Validate(TriggeredConversation(T("""{ "on": "heroKilled", "where": { "village.culture": "x" } }"""))), "no field 'village.culture'");

    [Fact]
    public void Where_with_the_wrong_value_type_is_an_error() =>
        AssertTriggerError(Validate(TriggeredConversation(T("""{ "on": "heroKilled", "where": { "victim.isPlayer": "yes" } }"""))), "true/false");

    [Theory]
    [InlineData("\"chance\": 1.5", "chance")]
    [InlineData("\"chance\": -0.1", "chance")]
    [InlineData("\"delivery\": \"carrier_pigeon\"", "delivery")]
    [InlineData("\"expiresAfterHours\": -1", "expiresAfterHours")]
    [InlineData("\"cooldownDays\": -2", "cooldownDays")]
    [InlineData("\"params\": { \"radius\": 3 }", "takes no 'params'")]
    public void Out_of_range_or_unknown_values_are_errors(string field, string contains) =>
        AssertTriggerError(Validate(TriggeredConversation(T($$"""{ "on": "heroKilled", {{field}} }"""))), contains);

    [Fact]
    public void Triggered_conversation_must_not_have_an_entry()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded" }"""));
        c["entry"] = "greeting";
        AssertTriggerError(Validate(c), "no entry");
    }

    [Fact]
    public void Triggered_conversation_starts_with_an_npc_node()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded" }"""));
        c["start"] = new JArray("p_ab_yes");
        var issues = Validate(c);
        Assert.Contains(issues.Errors(), i => i.Code == IssueCodes.InvalidConversation && i.Message.Contains("is triggered"));
    }

    [Fact]
    public void Untriggered_conversation_without_entry_is_still_an_error()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded" }"""));
        c.Remove("trigger");
        Assert.Contains(Validate(c).Errors(), i => i.Code == IssueCodes.InvalidConversation && i.Message.Contains("entry"));
    }

    [Fact]
    public void Inquiry_with_one_npc_text_and_player_options_ending_is_valid() =>
        AssertClean(Validate(TriggeredConversation(T("""{ "on": "playerBattleEnded", "delivery": "inquiry" }"""))));

    [Fact]
    public void Inquiry_option_that_continues_is_an_error()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded", "delivery": "inquiry" }"""));
        Fixtures.Node(c, "p_ab_no")["next"] = new JArray("ab_hail");
        AssertTriggerError(Validate(c), "option 'p_ab_no' must end");
    }

    [Fact]
    public void Inquiry_needs_exactly_one_start_node()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded", "delivery": "inquiry" }"""));
        Fixtures.Nodes(c).Add(JObject.Parse("""{ "id": "ab_alt", "speaker": "npc", "next": ["p_ab_yes"] }"""));
        c["start"] = new JArray("ab_hail", "ab_alt");
        AssertTriggerError(Validate(c), "exactly one start");
    }

    [Fact]
    public void Inquiry_whose_text_ends_at_once_has_no_options()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded", "delivery": "inquiry" }"""));
        Fixtures.Node(c, "ab_hail")["next"] = new JArray("end");
        AssertTriggerError(Validate(c), "player nodes");
    }

    [Fact]
    public void Notification_conversation_start_must_end()
    {
        var c = TriggeredConversation(T("""{ "on": "playerBattleEnded", "delivery": "notification" }"""));
        AssertTriggerError(Validate(c), "notification");
        Fixtures.Node(c, "ab_hail")["next"] = new JArray("end");
        // The player nodes are then unreachable (a warning), but no trigger error.
        Assert.DoesNotContain(Validate(c).Errors(), i => i.Code == IssueCodes.InvalidTrigger);
    }

    static JObject TriggeredLine(string trigger) =>
        JObject.Parse($$"""{ "id": "smoke_01", "trigger": {{trigger}} }""");

    [Fact]
    public void Triggered_line_without_slot_is_valid() =>
        AssertClean(Validate(line: TriggeredLine("""{ "on": "villageLooted", "subject": "settlement:village_ES1" }""")));

    [Fact]
    public void Triggered_line_with_a_slot_is_an_error()
    {
        var line = TriggeredLine("""{ "on": "villageLooted" }""");
        line["slot"] = "intro";
        AssertTriggerError(Validate(line: line), "both 'slot' and 'trigger'");
    }

    [Theory]
    [InlineData("conversation")]
    [InlineData("inquiry")]
    public void Triggered_line_can_only_be_a_notification(string delivery) =>
        AssertTriggerError(Validate(line: TriggeredLine($$"""{ "on": "villageLooted", "delivery": "{{delivery}}" }""")), "can only be");

    [Fact]
    public void Triggered_line_does_not_count_for_required_slots()
    {
        // Fixture dialogs already have every required slot; adding a triggered line changes nothing about that.
        var issues = Validate(line: TriggeredLine("""{ "on": "daily", "chance": 0.2 }"""));
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.MissingRequiredSlot || i.Code == IssueCodes.UnknownSlot);
    }

    [Fact]
    public void Near_settlement_params_are_checked()
    {
        AssertClean(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 5, "rearmRadius": 8 } }""")));
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 5 } }""")), "rearmRadius");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 5, "rearmRadius": 5 } }""")), "rearmRadius > radius");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 0, "rearmRadius": 2 } }""")), "radius > 0");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": "far", "rearmRadius": 8 } }""")), "number");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "nearSettlement", "params": { "settlement": "town_V1", "radius": 5, "rearmRadius": 8, "x": 1 } }""")), "no parameter 'x'");
    }

    [Fact]
    public void Var_crossed_needs_an_own_int_variable_and_a_direction()
    {
        AssertClean(Validate(line: TriggeredLine("""{ "on": "varCrossed", "params": { "var": "mood", "value": 10, "direction": "up" } }""")));
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "varCrossed", "params": { "var": "mood", "value": 10, "direction": "sideways" } }""")), "'up' or 'down'");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "varCrossed", "params": { "var": "gold_seen", "value": 10, "direction": "up" } }""")), "not an int variable");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "varCrossed", "params": { "var": "mood", "value": 1.5, "direction": "up" } }""")), "whole-number");
        AssertClean(Validate(line: TriggeredLine("""{ "on": "varCrossed", "params": { "var": "trust", "value": 3, "direction": "down" } }"""),
            dialogs: d => d["variables"] = JObject.Parse("""{ "trust": { "type": "int" } }""")));
    }

    [Fact]
    public void Role_triggers_need_a_known_role()
    {
        AssertClean(Validate(line: TriggeredLine("""{ "on": "roleStarted", "params": { "role": "governor" } }""")));
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "roleEnded", "params": { "role": "jester" } }""")), "role 'jester'");
        AssertTriggerError(Validate(line: TriggeredLine("""{ "on": "roleEnded" }""")), "needs the parameter 'role'");
    }
}
