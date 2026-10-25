using AlbocariusCompanions.Core.Audio;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 56 (M6): authored conversations in dialogs.json. Each test breaks exactly one rule of a valid conversation.
public class ConversationTests
{
    const string Id = "test_companion";

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject>? mutate = null, Action<Dictionary<string, JObject>>? mutateLangs = null,
        Action<JObject>? mutateDialogs = null)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        mutate?.Invoke(conversation);
        Fixtures.AddConversation(d, langs, conversation);
        // Bool variables the condition/effect tests below use (M7: every flag is a declared variable).
        foreach (var name in new[] { "a", "b", "x", "asked" })
            d["variables"]![name] = new JObject { ["type"] = "bool" };
        mutateDialogs?.Invoke(d);
        mutateLangs?.Invoke(langs);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static JObject EnLines(Dictionary<string, JObject> langs) => Fixtures.LangLines(langs["en"]);

    static ValidationIssue SingleError(IReadOnlyList<ValidationIssue> issues, string code)
    {
        var error = Assert.Single(issues.Errors());
        Assert.Equal(code, error.Code);
        Assert.Equal(Id, error.Folder);
        return error;
    }

    static ValidationIssue SingleWarning(IReadOnlyList<ValidationIssue> issues, string code)
    {
        Assert.Empty(issues.Errors());
        var warning = Assert.Single(issues);
        Assert.Equal(code, warning.Code);
        Assert.Equal(IssueSeverity.Warning, warning.Severity);
        return warning;
    }

    static void AddNode(JObject conversation, string id, string speaker, params string[] next) =>
        Fixtures.Nodes(conversation).Add(new JObject { ["id"] = id, ["speaker"] = speaker, ["next"] = new JArray(next) });

    [Fact]
    public void Valid_conversation_has_no_issues() => Assert.Empty(Validate());

    [Fact]
    public void Conversations_are_optional() =>
        Assert.Empty(DialogChecks.Validate(new[] { Fixtures.Package(Id) }));

    [Fact]
    public void Conversation_loads_from_disk_with_full_ids_and_texts()
    {
        using var tmp = new TempDir();
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        Fixtures.AddConversation(d, langs);
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d, langs);

        var result = AlbocariusCompanions.Core.Registry.CompanionCatalog.Load(tmp.Path);

        Assert.Empty(result.Issues);
        result.Registry.TryGet(Id, out var package);
        var conversation = Assert.Single(package!.Conversations);
        Assert.Equal("old_wound", conversation.Id);
        var story = conversation.Nodes!.Single(n => n.Id == "wound_story");
        Assert.Equal("test_companion_wound_story", story.FullId);
        Assert.Equal("A border skirmish, Captain.", package.Text("wound_story", "en"));
    }

    [Fact]
    public void Unknown_field_in_a_node_is_a_load_error()
    {
        using var tmp = new TempDir();
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        var conversation = Fixtures.Conversation();
        Fixtures.Node(conversation, "wound_story")["mood"] = "sad";
        Fixtures.AddConversation(d, langs, conversation);
        Fixtures.Write(tmp.Path, Id, Fixtures.Companion(Id), d, langs);

        var result = AlbocariusCompanions.Core.Registry.CompanionCatalog.Load(tmp.Path);
        Assert.Equal(IssueCodes.InvalidJson, Assert.Single(result.Issues).Code);
        Assert.Empty(result.Registry.Ids);
    }

    // ---- conversation level ----

    [Theory]
    [InlineData(null)]
    [InlineData("Old-Wound")]
    public void Conversation_needs_a_snake_case_id(string? id) =>
        SingleError(Validate(c => c["id"] = id), IssueCodes.InvalidConversation);

    [Fact]
    public void Conversation_ids_are_unique_per_companion()
    {
        var second = Fixtures.Conversation();
        foreach (var node in Fixtures.Nodes(second).Cast<JObject>())
        {
            node["id"] = "x_" + node["id"];
            if (node["next"] is JArray next)
                node["next"] = new JArray(next.Select(n => (string)n! is "hub" or "end" ? (string)n! : "x_" + n));
        }
        second["start"] = new JArray("x_p_wound_ask");
        var texts = Fixtures.ConversationEn().ToDictionary(kv => "x_" + kv.Key, kv => kv.Value);

        var issues = Validate(mutateDialogs: d => ((JArray)d["conversations"]!).Add(second),
            mutateLangs: langs => { foreach (var (k, v) in texts) EnLines(langs)[k] = v; });
        Assert.Contains("same id", SingleError(issues, IssueCodes.InvalidConversation).Message);
    }

    [Fact]
    public void Unknown_entry_is_an_error() =>
        Assert.Contains("talk", SingleError(Validate(c => c["entry"] = "talk"), IssueCodes.InvalidConversation).Message);

    [Fact]
    public void Missing_start_is_an_error() =>
        SingleError(Validate(c => c.Remove("start")), IssueCodes.InvalidConversation);

    [Fact]
    public void Start_must_name_a_node() =>
        Assert.Contains("nowhere", SingleError(Validate(c => c["start"] = new JArray("nowhere")), IssueCodes.UnknownNode).Message);

    [Fact]
    public void Menu_conversation_starts_with_a_player_line() =>
        SingleError(Validate(c => c["start"] = new JArray("wound_story")), IssueCodes.InvalidConversation);

    [Fact]
    public void Greeting_starts_with_a_companion_line()
    {
        var issues = Validate(c => c["entry"] = "greeting");
        Assert.Contains("greeting", SingleError(issues, IssueCodes.InvalidConversation).Message);
    }

    [Fact]
    public void Valid_greeting_is_accepted() => Assert.Empty(Validate(c =>
    {
        c["entry"] = "greeting";
        c["start"] = new JArray("wound_story");
        Fixtures.Nodes(c).Where(n => (string?)n["id"] == "p_wound_ask").ToList().ForEach(n => n.Remove());
    }, langs => EnLines(langs).Remove("p_wound_ask")));

    [Fact]
    public void Greeting_for_a_companion_not_met_is_an_error() => SingleError(Validate(c =>
    {
        c["entry"] = "greeting";
        c["start"] = new JArray("wound_story");
        c["when"] = new JObject { ["met"] = false };
        Fixtures.Nodes(c).Where(n => (string?)n["id"] == "p_wound_ask").ToList().ForEach(n => n.Remove());
    }, langs => EnLines(langs).Remove("p_wound_ask")), IssueCodes.InvalidCondition);

    // ---- nodes ----

    [Fact]
    public void Node_ids_share_the_namespace_of_lines()
    {
        var issues = Validate(c => Fixtures.Node(c, "wound_thanks")["id"] = "intro_01",
            mutateDialogs: d => ((JArray)((JObject)((JArray)d["conversations"]!)[0])["nodes"]!).OfType<JObject>()
                .Where(n => n["next"] is JArray next && next.Any(x => (string?)x == "wound_thanks")).ToList()
                .ForEach(n => n["next"] = new JArray("intro_01")));
        Assert.Contains("intro_01", SingleError(issues, IssueCodes.DuplicateLineId).Message);
    }

    [Theory]
    [InlineData("hub")]
    [InlineData("end")]
    [InlineData("Bad Id")]
    public void Reserved_or_invalid_node_ids_are_errors(string id)
    {
        var issues = Validate(c => AddNode(c, id, "npc", "hub"));
        Assert.Contains(issues.Errors(), i => i.Code == IssueCodes.InvalidLineId);
    }

    [Fact]
    public void Unknown_speaker_is_an_error() =>
        Assert.Contains("narrator", SingleError(Validate(c => Fixtures.Node(c, "wound_thanks")["speaker"] = "narrator"),
            IssueCodes.InvalidConversation).Message);

    [Fact]
    public void Player_nodes_are_never_voiced() =>
        SingleError(Validate(c => Fixtures.Node(c, "p_wound_sorry")["voiced"] = true), IssueCodes.VoicedPlayerLine);

    [Fact]
    public void Node_needs_next() =>
        Assert.Contains("wound_thanks", SingleError(Validate(c => Fixtures.Node(c, "wound_thanks").Remove("next")),
            IssueCodes.InvalidConversation).Message);

    [Fact]
    public void Hub_and_end_stand_alone_in_next() =>
        SingleError(Validate(c => Fixtures.Node(c, "wound_thanks")["next"] = new JArray("end", "p_wound_skip")), IssueCodes.InvalidConversation);

    [Fact]
    public void Next_must_name_a_node_of_the_same_conversation() =>
        Assert.Contains("elsewhere", SingleError(Validate(c => Fixtures.Node(c, "wound_thanks")["next"] = new JArray("elsewhere")),
            IssueCodes.UnknownNode).Message);

    [Fact]
    public void Next_does_not_mix_companion_and_player_lines() => SingleError(
        Validate(c => Fixtures.Node(c, "wound_story")["next"] = new JArray("p_wound_sorry", "wound_thanks")),
        IssueCodes.InvalidConversation);

    [Fact]
    public void Player_line_is_answered_by_the_companion() => SingleError(
        Validate(c => Fixtures.Node(c, "p_wound_ask")["next"] = new JArray("p_wound_skip")),
        IssueCodes.InvalidConversation);

    [Fact]
    public void Companion_lines_may_follow_each_other() => Assert.Empty(Validate(c =>
    {
        Fixtures.Node(c, "wound_thanks")["next"] = new JArray("wound_more");
        AddNode(c, "wound_more", "npc", "end");
    }, langs => EnLines(langs)["wound_more"] = "And that is all, Captain."));

    [Fact]
    public void Unreachable_node_is_a_warning() => Assert.Contains("orphan", SingleWarning(
        Validate(c => AddNode(c, "orphan", "npc", "end"), langs => EnLines(langs)["orphan"] = "Never said."),
        IssueCodes.UnreachableNode).Message);

    [Fact]
    public void Follow_ups_that_all_have_conditions_may_stall() => SingleWarning(Validate(c =>
    {
        Fixtures.Node(c, "p_wound_sorry")["when"] = new JObject { ["minRelation"] = 5 };
        Fixtures.Node(c, "p_wound_skip")["when"] = new JObject { ["flags"] = new JArray("asked") };
    }), IssueCodes.ConversationMayStall);

    [Fact]
    public void Loops_back_to_an_earlier_node_are_allowed() =>
        Assert.Empty(Validate(c => Fixtures.Node(c, "wound_thanks")["next"] = new JArray("p_wound_sorry", "p_wound_skip")));

    // ---- texts and audio ----

    [Fact]
    public void Every_node_needs_an_english_text() =>
        Assert.Contains("wound_story", SingleError(Validate(mutateLangs: langs => EnLines(langs).Remove("wound_story")),
            IssueCodes.MissingEnglishText).Message);

    [Fact]
    public void Node_missing_from_another_language_is_a_warning()
    {
        var issues = Validate(mutateLangs: langs => langs["tr"] = JObject.Parse("""{ "schemaVersion": 1, "lines": { "intro_01": "Selam, Kaptan." } }"""));
        Assert.Empty(issues.Errors());
        Assert.Contains(issues, i => i.Code == IssueCodes.MissingTranslation && i.Message.Contains("wound_story"));
    }

    [Fact]
    public void Voiced_node_must_not_use_player_name_tokens() => SingleError(
        Validate(mutateLangs: langs => EnLines(langs)["wound_story"] = "A skirmish, {PLAYER.NAME}."), IssueCodes.PlayerTokenInVoicedLine);

    [Fact]
    public void Node_audio_follows_the_line_rules() =>
        SingleError(Validate(c => Fixtures.Node(c, "wound_story")["audio"] = "audio/story.wav"), IssueCodes.InvalidAudioExtension);

    [Fact]
    public void Voiced_nodes_get_voice_over_entries()
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        Fixtures.AddConversation(d, langs);
        var package = Fixtures.Package(Id, Fixtures.Companion(Id), d, folderPath: "/m/Companions/" + Id, langs: langs);

        var doc = VoicedLinesXml.Build(new[] { package }, path => path.Replace('\\', '/').EndsWith("audio/wound_story.ogg"));

        var entry = Assert.Single(doc.Root!.Element("VoiceOvers")!.Elements("VoiceOver"));
        Assert.Equal("test_companion_wound_story", entry.Attribute("id")!.Value);
        Assert.Equal("../Companions/test_companion/audio/wound_story", entry.Element("Voice")!.Attribute("path")!.Value);
    }

    // ---- conditions and effects ----

    [Theory]
    [InlineData("minRelation", 101)]
    [InlineData("maxRelation", -101)]
    [InlineData("minGold", -1)]
    public void Condition_values_out_of_range(string field, int value) =>
        SingleError(Validate(c => c["when"] = new JObject { [field] = value }), IssueCodes.InvalidCondition);

    [Fact]
    public void Min_relation_above_max_relation_is_an_error() =>
        SingleError(Validate(c => Fixtures.Node(c, "p_wound_skip")["when"] = new JObject { ["minRelation"] = 10, ["maxRelation"] = 5 }),
            IssueCodes.InvalidCondition);

    [Fact]
    public void Flag_in_flags_and_notFlags_is_an_error() =>
        SingleError(Validate(c => c["when"] = new JObject { ["flags"] = new JArray("a"), ["notFlags"] = new JArray("a") }),
            IssueCodes.InvalidCondition);

    [Fact]
    public void Flag_names_are_snake_case() =>
        SingleError(Validate(c => c["when"] = new JObject { ["flags"] = new JArray("Told-Wound") }), IssueCodes.InvalidCondition);

    [Theory]
    [InlineData("relation", 0)]
    [InlineData("relation", 150)]
    public void Effect_values_out_of_range(string field, int value) =>
        SingleError(Validate(c => Fixtures.Node(c, "p_wound_skip")["effects"] = new JObject { [field] = value }), IssueCodes.InvalidEffect);

    [Fact]
    public void Flag_set_and_cleared_by_one_node_is_an_error() =>
        SingleError(Validate(c => Fixtures.Node(c, "p_wound_skip")["effects"] =
            new JObject { ["setFlags"] = new JArray("x"), ["clearFlags"] = new JArray("x") }), IssueCodes.InvalidEffect);

    [Fact]
    public void All_effects_and_conditions_together_are_valid() => Assert.Empty(Validate(c =>
    {
        c["when"] = new JObject { ["met"] = true, ["hired"] = false, ["inParty"] = false, ["minRelation"] = -20, ["maxRelation"] = 50,
            ["minGold"] = 0, ["flags"] = new JArray("a"), ["notFlags"] = new JArray("b") };
        Fixtures.Node(c, "p_wound_sorry")["effects"] = new JObject { ["relation"] = -3,
            ["transfer"] = new JObject { ["gold"] = 200, ["from"] = "player", ["to"] = "self" }, ["setFlags"] = new JArray("b"),
            ["clearFlags"] = new JArray("a"), ["join"] = true };
    }));
}
