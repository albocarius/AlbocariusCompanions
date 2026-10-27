using AlbocariusCompanions.Core.Dialogs;
using AlbocariusCompanions.Core.Registry;
using AlbocariusCompanions.Core.Simulation;
using Newtonsoft.Json.Linq;

namespace AlbocariusCompanions.Acceptance.Core;

// Task 94 (M9 I3): the simulator engine over the sample companion, step by step.
public class SimulationTests
{
    const string Id = "test_companion";

    static readonly CompanionRegistry Registry = CompanionCatalog.Load(Path.Combine(RepoRoot(), "module", "ModuleData", "Companions")).Registry;

    static SimSession Session(int gold = 1000, Action<SimState>? edit = null)
    {
        var state = new SimState { Player = new SimPlayer { Gold = gold } };
        edit?.Invoke(state);
        return new SimSession(Registry, Id, state, new SystemRandomSource(1));
    }

    static SimOption Option(SimStep step, string node) => step.Options.Single(o => o.Node == node);

    [Fact]
    public void Missing_state_fields_take_the_documented_defaults()
    {
        var state = new SimSession(Registry, Id, null, new SystemRandomSource(1)).State;
        Assert.Equal(0, state.Player.Gold);
        Assert.False(state.ClanIsFull);
        Assert.Empty(state.Started);
        Assert.Equal(false, (bool)state.Variables["test_companion.told_scar"]);
        Assert.Equal(0, (int)state.Variables["test_companion.mood"]);
    }

    [Fact]
    public void Hire_pays_and_joins_and_is_refused_without_gold()
    {
        var session = Session(gold: 1000);
        Assert.Null(session.Hire());
        var c = session.State.Companions[Id];
        Assert.True(c.Met && c.Hired && c.InParty);
        Assert.Equal(500, session.State.Player.Gold);
        Assert.Equal(500, c.Gold);
        Assert.Equal("already hired", session.Hire());

        Assert.Contains("gold", Session(gold: 10).Hire());
        Assert.Contains("limit", Session(edit: s => s.ClanIsFull = true).Hire());
    }

    [Fact]
    public void Sample_sequence_hire_the_scar_spare_coin()
    {
        var session = Session(gold: 1000);
        Assert.Null(session.Hire());

        // Hub: the_scar is offered; spare_coin is listed but hidden (told_scar is false).
        var hub = session.Start("hub");
        Assert.Equal(SimStep.KindOptions, hub.Kind);
        Assert.Equal("hub", hub.At);
        Assert.True(Option(hub, "p_scar_ask").Shown);
        Assert.False(Option(hub, "p_coin_offer").Shown);
        Assert.Contains(Option(hub, "p_coin_offer").Conditions, r => r.Field == "flags.told_scar" && !r.Holds);

        // the_scar: the player asks, the companion answers (one candidate, it wins), two options follow.
        var story = session.Choose("the_scar", "p_scar_ask");
        Assert.Equal(new[] { "p_scar_ask", "scar_story" }, story.Said.Select(s => s.Node));
        Assert.Null(story.Said[0].Selection);
        Assert.Equal("scar_story", story.Said[1].Selection!.Winner);
        Assert.Equal(new[] { "scar_story" }, story.Said[1].Selection!.Group);
        Assert.Equal(new[] { "p_scar_sorry", "p_scar_enough" }, story.Options.Select(o => o.Node));
        Assert.Contains("the_scar", story.Said[0].Conversation);
        Assert.Contains($"{Id}:the_scar", story.State.Started);

        var sorry = session.Choose("the_scar", "p_scar_sorry");
        Assert.Equal(new[] { "p_scar_sorry", "scar_thanks" }, sorry.Said.Select(s => s.Node));
        Assert.Equal(2, sorry.State.Companions[Id].Relation);
        Assert.Equal(true, (bool)sorry.State.Variables["test_companion.told_scar"]);
        // Back at the hub: the_scar is used up (once), spare_coin's offer is shown, the ask needs relation 10.
        Assert.Equal("hub", sorry.At);
        Assert.False(Option(sorry, "p_scar_ask").Shown);
        Assert.True(Option(sorry, "p_coin_offer").Shown);
        Assert.False(Option(sorry, "p_coin_ask").Shown);

        var coin = session.Choose("spare_coin", "p_coin_offer");
        Assert.Equal(new[] { "p_coin_offer", "coin_thanks" }, coin.Said.Select(s => s.Node));
        Assert.Contains("gold 100 player -> self", coin.Said[0].Effects);
        Assert.Equal(SimStep.KindEnd, coin.Kind);
        Assert.Equal(400, coin.State.Player.Gold);
        Assert.Equal(600, coin.State.Companions[Id].Gold);
        Assert.Equal(3, coin.State.Companions[Id].Relation);
        Assert.Equal(true, (bool)coin.State.Variables["test_companion.got_coin"]);
    }

    [Fact]
    public void Npc_reply_selection_reports_candidates_conditions_group_and_winner()
    {
        // Relation 15: coin_refuse (maxRelation 20, priority 1) and coin_fine (priority 0) both hold → coin_refuse.
        var session = Session(edit: s =>
        {
            s.Companions[Id] = new SimCompanion { Met = true, Hired = true, InParty = true, Relation = 15 };
            s.Variables["test_companion.told_scar"] = true;
        });
        session.Start("hub");
        var step = session.Choose("spare_coin", "p_coin_ask");
        var selection = step.Said[1].Selection!;
        Assert.Equal("npc", selection.Set);
        Assert.Equal(new[] { "coin_refuse", "coin_fine" }, selection.Candidates.Select(c => c.Id));
        Assert.All(selection.Candidates, c => Assert.True(c.Passes));
        Assert.Equal(1, selection.TopPriority);
        Assert.Equal(new[] { "coin_refuse" }, selection.Group);
        Assert.Equal("coin_refuse", selection.Winner);
        var refuse = selection.Candidates[0].Conditions.Single();
        Assert.Equal("maxRelation", refuse.Field);
        Assert.Equal(20, (int)refuse.Expected!);
        Assert.Equal(15, (int)refuse.Actual!);
        Assert.True(refuse.Holds);
        Assert.Equal("hub", step.At);
    }

    [Fact]
    public void Greeting_selection_opens_the_talk()
    {
        var session = Session(edit: s =>
        {
            s.Companions[Id] = new SimCompanion { Met = true, Hired = true, InParty = true };
            s.Variables["test_companion.told_scar"] = true;
        });
        var step = session.Start("greeting");
        var greeting = Assert.Single(step.Selections);
        Assert.Equal("greeting", greeting.Set);
        Assert.Equal("after_scar", greeting.Winner);
        Assert.Equal("greet_scar", Assert.Single(step.Said).Node);
        Assert.Equal(new[] { "p_greet_glad", "p_greet_busy" }, step.Options.Select(o => o.Node));
        Assert.Equal("after_scar/greet_scar", step.At);
        Assert.Contains($"{Id}:after_scar", step.State.Started);

        // once: the next talk has no greeting and opens at the hub.
        var next = session.Start("greeting");
        Assert.Null(Assert.Single(next.Selections).Winner);
        Assert.Empty(next.Said);
        Assert.Equal("hub", next.At);
    }

    [Fact]
    public void Only_shown_options_of_the_last_step_can_be_chosen()
    {
        var session = Session();
        session.Start("hub");
        Assert.Throws<SimulationException>(() => session.Choose("spare_coin", "p_coin_offer"));
        Assert.Throws<SimulationException>(() => session.Choose("the_scar", "scar_story"));
    }

    [Fact]
    public void Unknown_variables_in_the_state_are_rejected()
    {
        Assert.Throws<SimulationException>(() => Session(edit: s => s.Variables["test_companion.nope"] = true));
        Assert.Throws<SimulationException>(() => Session(edit: s => s.Variables["test_companion.told_scar"] = 3));
    }

    [Fact]
    public void Condition_verdicts_match_the_game_code()
    {
        var context = new Ctx { Relation = 5 };
        var when = JObject.Parse("""{ "minRelation": 10, "any": [ { "hired": true }, { "not": { "inParty": false } } ] }""")
            .ToObject<AlbocariusCompanions.Core.Models.ConditionSet>()!;
        var results = ConditionExplain.When(when, context);
        Assert.Equal(ConversationConditions.Holds(when, context), results.All(r => r.Holds));
        Assert.False(results.Single(r => r.Field == "minRelation").Holds);
        Assert.True(results.Single(r => r.Field == "any").Holds);
    }

    sealed class Ctx : IConversationContext
    {
        public bool HasMet => true;
        public bool IsHired => true;
        public bool IsInPlayerParty => false;
        public int Relation { get; set; }
        public int PlayerGold => 0;
        public bool ClanIsFull => false;
        public int? GetVariable(string reference) => 0;
        public int? GoldOf(string actor) => 0;
        public int PlayerItemCount(string itemId) => 0;
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AlbocariusCompanions.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
