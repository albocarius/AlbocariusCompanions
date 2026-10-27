using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Models;
using AlbocariusCompanions.Core.Validation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 90 (M9 I1, F1/F2/F4): the selection rule. Highest priority wins, absent = 0, ties at random; hub conversations are
// all listed in priority order; player options are never selected; picks are kept for the talk.
public class SelectionTests
{
    const string Id = "test_companion";

    sealed class Item
    {
        public Item(string name, int? priority) { Name = name; Priority = priority; }
        public string Name { get; }
        public int? Priority { get; }
    }

    sealed class Context : IConversationContext
    {
        public bool HasMet { get; set; } = true;
        public bool IsHired { get; set; } = true;
        public bool IsInPlayerParty { get; set; } = true;
        public int Relation { get; set; }
        public int PlayerGold { get; set; }
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => 0;
        public int? GoldOf(string actor) => actor == "player" ? PlayerGold : 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    static HashSet<string> Winners(Func<IRandomSource, string?> pick, int runs = 200)
    {
        var random = new SystemRandomSource(7);
        var seen = new HashSet<string>();
        for (var i = 0; i < runs; i++)
            if (pick(random) is { } name)
                seen.Add(name);
        return seen;
    }

    [Fact]
    public void Highest_priority_always_wins()
    {
        var items = new[] { new Item("low", 1), new Item("high", 5), new Item("none", null) };
        Assert.Equal(new[] { "high" }, Winners(r => Selection.Pick(items, i => i.Priority, r)?.Name));
    }

    [Fact]
    public void Absent_priority_is_zero()
    {
        var items = new[] { new Item("none", null), new Item("negative", -1) };
        Assert.Equal(new[] { "none" }, Winners(r => Selection.Pick(items, i => i.Priority, r)?.Name));
        var tie = new[] { new Item("none", null), new Item("zero", 0) };
        Assert.Equal(new[] { "none", "zero" }, Winners(r => Selection.Pick(tie, i => i.Priority, r)?.Name).OrderBy(n => n));
    }

    [Fact]
    public void Ties_are_random_and_every_tied_candidate_is_seen()
    {
        var items = new[] { new Item("a", 2), new Item("b", 2), new Item("c", 2), new Item("low", 1) };
        Assert.Equal(new[] { "a", "b", "c" }, Winners(r => Selection.Pick(items, i => i.Priority, r)?.Name).OrderBy(n => n));
    }

    [Fact]
    public void Nothing_passing_picks_nothing()
    {
        Assert.Null(Selection.Pick(Array.Empty<Item>(), i => i.Priority, new SystemRandomSource(1)));
    }

    [Fact]
    public void Ordered_is_priority_descending_and_stable()
    {
        var items = new[] { new Item("a", null), new Item("b", 3), new Item("c", 0), new Item("d", 3) };
        Assert.Equal(new[] { "b", "d", "a", "c" }, Selection.Ordered(items, i => i.Priority).Select(i => i.Name));
    }

    [Fact]
    public void Memory_keeps_the_pick_while_it_passes_and_redecides_when_it_does_not()
    {
        var a = new Item("a", 0);
        var b = new Item("b", 0);
        var memory = new SelectionMemory();
        var random = new SystemRandomSource(3);
        var first = memory.Decide("k", new[] { a, b }, i => i.Priority, random)!;
        for (var i = 0; i < 50; i++)
            Assert.Same(first, memory.Decide("k", new[] { a, b }, x => x.Priority, random));

        var other = ReferenceEquals(first, a) ? b : a;
        Assert.Same(other, memory.Decide("k", new[] { other }, i => i.Priority, random));
        memory.Clear();
        Assert.Equal(new[] { "a", "b" }, Winners(r =>
        {
            memory.Clear();
            return memory.Decide("k", new[] { a, b }, i => i.Priority, r)?.Name;
        }).OrderBy(n => n));
    }

    // ---- Conversation sets over a package ----

    static JObject Hub(string id, int? priority)
    {
        var c = JObject.Parse($$"""
        { "id": "{{id}}", "entry": "hub", "start": ["p_{{id}}"],
          "nodes": [ { "id": "p_{{id}}", "speaker": "player", "next": ["hub"] } ] }
        """);
        if (priority != null) c["priority"] = priority;
        return c;
    }

    static JObject Greeting(string id, int? priority, JObject? when = null)
    {
        var c = JObject.Parse($$"""
        { "id": "{{id}}", "entry": "greeting", "start": ["g_{{id}}_1", "g_{{id}}_2"],
          "nodes": [ { "id": "g_{{id}}_1", "speaker": "npc", "next": ["hub"] },
                     { "id": "g_{{id}}_2", "speaker": "npc", "next": ["hub"], "when": { "minRelation": 50 }, "priority": 1 } ] }
        """);
        if (priority != null) c["priority"] = priority;
        if (when != null) c["when"] = when;
        return c;
    }

    static CompanionPackage Package(params JObject[] conversations)
    {
        var d = Fixtures.Dialogs(Id);
        d["conversations"] = new JArray(conversations);
        return Fixtures.Package(Id, Fixtures.Companion(Id), d);
    }

    [Fact]
    public void Hub_conversations_are_all_listed_highest_priority_first_ties_in_file_order()
    {
        var package = Package(Hub("a", null), Hub("b", 2), Hub("c", null), Greeting("g", 9));
        Assert.Equal(new[] { "b", "a", "c" }, ConversationSelection.HubOrder(package).Select(c => c.Id));
    }

    [Fact]
    public void Higher_priority_greeting_always_opens_the_talk()
    {
        var package = Package(Greeting("low", null), Greeting("high", 3));
        Assert.Equal(new[] { "high" }, Winners(r =>
            ConversationSelection.Greeting(package, new Context(), _ => false, new SelectionMemory(), r)?.Id));
    }

    [Fact]
    public void Equal_priority_greetings_both_open_across_talks_and_one_is_kept_within_a_talk()
    {
        var package = Package(Greeting("a", null), Greeting("b", 0));
        Assert.Equal(new[] { "a", "b" }, Winners(r =>
            ConversationSelection.Greeting(package, new Context(), _ => false, new SelectionMemory(), r)?.Id).OrderBy(n => n));

        var memory = new SelectionMemory();
        var random = new SystemRandomSource(11);
        var kept = ConversationSelection.Greeting(package, new Context(), _ => false, memory, random);
        for (var i = 0; i < 30; i++)
            Assert.Same(kept, ConversationSelection.Greeting(package, new Context(), _ => false, memory, random));
    }

    [Fact]
    public void A_greeting_whose_condition_or_once_fails_is_not_a_candidate()
    {
        var once = Greeting("once", 5);
        once["once"] = true;
        var package = Package(Greeting("fallback", null), once, Greeting("cond", 9, new JObject { ["inParty"] = false }));
        Assert.Equal(new[] { "fallback" }, Winners(r =>
            ConversationSelection.Greeting(package, new Context(), id => id == "once", new SelectionMemory(), r)?.Id));
        Assert.Null(ConversationSelection.Greeting(package, new Context { HasMet = false }, _ => false, new SelectionMemory(),
            new SystemRandomSource(1)));
    }

    [Fact]
    public void Npc_reply_follows_priority_among_passing_nodes()
    {
        var package = Package(Greeting("g", null));
        var conversation = package.Conversations[0];
        // Relation 0: only g_g_1 holds. Relation 60: both hold, g_g_2 has priority 1.
        Assert.Equal(new[] { "g_g_1" }, Winners(r =>
            ConversationSelection.NpcReply(Id, conversation, null, new Context(), new SelectionMemory(), r)?.Id));
        Assert.Equal(new[] { "g_g_2" }, Winners(r =>
            ConversationSelection.NpcReply(Id, conversation, null, new Context { Relation = 60 }, new SelectionMemory(), r)?.Id));
    }

    [Fact]
    public void Npc_replies_of_a_next_list_tie_at_random_and_player_nodes_are_never_candidates()
    {
        var c = JObject.Parse("""
        { "id": "c", "entry": "hub", "start": ["p1"],
          "nodes": [ { "id": "p1", "speaker": "player", "next": ["n1", "n2"] },
                     { "id": "n1", "speaker": "npc", "next": ["p2", "p3"] },
                     { "id": "n2", "speaker": "npc", "next": ["p2", "p3"] },
                     { "id": "p2", "speaker": "player", "next": ["hub"], "priority": 9 },
                     { "id": "p3", "speaker": "player", "next": ["hub"] } ] }
        """);
        var conversation = Package(c).Conversations[0];
        Assert.Equal(new[] { "n1", "n2" }, Winners(r =>
            ConversationSelection.NpcReply(Id, conversation, "p1", new Context(), new SelectionMemory(), r)?.Id).OrderBy(n => n));
        Assert.Empty(ConversationSelection.NpcCandidates(conversation, new[] { "p2", "p3" }, new Context()));
    }

    [Fact]
    public void Sample_coin_refuse_wins_while_both_replies_hold()
    {
        var package = Package(JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "module", "ModuleData", "Companions",
            "test_companion", "dialogs.json")))["conversations"]!.Cast<JObject>().First(c => (string?)c["id"] == "spare_coin"));
        var conversation = package.Conversations[0];
        Assert.Equal(new[] { "coin_refuse" }, Winners(r =>
            ConversationSelection.NpcReply(Id, conversation, "p_coin_ask", new Context { Relation = 15 }, new SelectionMemory(), r)?.Id));
        Assert.Equal(new[] { "coin_fine" }, Winners(r =>
            ConversationSelection.NpcReply(Id, conversation, "p_coin_ask", new Context { Relation = 30 }, new SelectionMemory(), r)?.Id));
    }

    // ---- Slot lines (F4) ----

    static List<DialogLine> SlotLines(params (string Id, int? Priority, JObject? When)[] lines) =>
        lines.Select(l => new JObject { ["id"] = l.Id, ["slot"] = "backstory", ["priority"] = l.Priority, ["when"] = l.When }
            .ToObject<DialogLine>()!).ToList();

    [Fact]
    public void Slot_line_is_filtered_by_when_then_selected_by_priority()
    {
        var lines = SlotLines(("plain", null, null), ("close", 2, new JObject { ["minRelation"] = 30 }));
        Assert.Equal(new[] { "plain" }, Winners(r =>
            ConversationSelection.SlotLine(Id, "backstory", lines, new Context(), new SelectionMemory(), r)?.Id));
        Assert.Equal(new[] { "close" }, Winners(r =>
            ConversationSelection.SlotLine(Id, "backstory", lines, new Context { Relation = 40 }, new SelectionMemory(), r)?.Id));
    }

    [Fact]
    public void Slot_lines_without_priority_stay_uniformly_random()
    {
        var lines = SlotLines(("a", null, null), ("b", null, null));
        Assert.Equal(new[] { "a", "b" }, Winners(r =>
            ConversationSelection.SlotLine(Id, "backstory", lines, new Context(), new SelectionMemory(), r)?.Id).OrderBy(n => n));
    }

    // ---- Validation ----

    static IReadOnlyList<ValidationIssue> Validate(Action<JObject> mutate)
    {
        var d = Fixtures.Dialogs(Id);
        var langs = Fixtures.Langs();
        Fixtures.AddConversation(d, langs);
        mutate(d);
        return DialogChecks.Validate(new[] { Fixtures.Package(Id, Fixtures.Companion(Id), d, langs: langs) });
    }

    static JObject FirstLine(JObject d, string slot) => Fixtures.Lines(d).Cast<JObject>().First(l => (string?)l["slot"] == slot);

    [Fact]
    public void Priority_on_conversations_lines_and_npc_nodes_is_valid()
    {
        var issues = Validate(d =>
        {
            var c = (JObject)d["conversations"]![0]!;
            c["priority"] = 4;
            Fixtures.Node(c, "wound_story")["priority"] = -2;
            FirstLine(d, "backstory")["priority"] = 1;
        });
        Assert.Empty(issues);
    }

    [Fact]
    public void Priority_on_a_player_node_is_a_warning()
    {
        var issues = Validate(d => Fixtures.Node((JObject)d["conversations"]![0]!, "p_wound_sorry")["priority"] = 2);
        Assert.Empty(issues.Errors());
        var warning = Assert.Single(issues);
        Assert.Equal(IssueCodes.PriorityIgnored, warning.Code);
        Assert.Contains("p_wound_sorry", warning.Message);
    }

    [Fact]
    public void A_line_condition_is_validated_like_a_conversation_condition()
    {
        var issues = Validate(d => FirstLine(d, "backstory")["when"] = new JObject { ["minRelation"] = 500 });
        var error = Assert.Single(issues.Errors());
        Assert.Equal(IssueCodes.InvalidCondition, error.Code);
        Assert.Contains("minRelation", error.Message);
    }

    [Fact]
    public void A_line_condition_on_an_undeclared_variable_is_an_error()
    {
        var issues = Validate(d => FirstLine(d, "backstory")["when"] = new JObject { ["flags"] = new JArray("nope") });
        Assert.Contains(issues.Errors(), i => i.Code == IssueCodes.UnknownVariable && i.Message.Contains("nope"));
    }

    [Fact]
    public void A_slot_whose_every_line_has_a_condition_is_a_warning()
    {
        var issues = Validate(d => FirstLine(d, "backstory")["when"] = new JObject { ["minRelation"] = 10 });
        Assert.Empty(issues.Errors());
        var warning = Assert.Single(issues);
        Assert.Equal(IssueCodes.SlotMayHaveNoLine, warning.Code);
        Assert.Contains("backstory", warning.Message);
    }

    [Fact]
    public void A_slot_with_one_unconditioned_line_is_fine()
    {
        var issues = Validate(d =>
        {
            Fixtures.Lines(d).Add(new JObject { ["id"] = "backstory_02", ["slot"] = "backstory", ["when"] = new JObject { ["hired"] = true } });
            Fixtures.LangLines(Fixtures.En());
        });
        Assert.DoesNotContain(issues, i => i.Code == IssueCodes.SlotMayHaveNoLine);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
